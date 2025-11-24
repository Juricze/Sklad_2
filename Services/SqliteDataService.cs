using Microsoft.EntityFrameworkCore;
using Sklad_2.Data;
using Sklad_2.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Sklad_2.Services
{
    public class SqliteDataService : IDataService
    {
        private readonly IDbContextFactory<DatabaseContext> _contextFactory;

        public SqliteDataService(IDbContextFactory<DatabaseContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task AddProductAsync(Product product)
        {
            using var context = _contextFactory.CreateDbContext();
            await context.Products.AddAsync(product);
            await context.SaveChangesAsync();
        }

        public async Task<Product> GetProductAsync(string ean)
        {
            using var context = _contextFactory.CreateDbContext();
            return await context.Products.FirstOrDefaultAsync(p => p.Ean == ean);
        }

        public async Task<List<Product>> GetProductsAsync()
        {
            using var context = _contextFactory.CreateDbContext();
            return await context.Products.ToListAsync();
        }

        public async Task UpdateProductAsync(Product product)
        {
            using var context = _contextFactory.CreateDbContext();
            context.Products.Update(product);
            await context.SaveChangesAsync();
        }

        public async Task<(bool Success, string ErrorMessage)> CompleteSaleAsync(Receipt receipt, List<Product> productsToUpdate, string userName)
        {
            using var context = _contextFactory.CreateDbContext();
            using var transaction = await context.Database.BeginTransactionAsync();
            try
            {
                await context.Receipts.AddAsync(receipt);

                // Track stock changes before updating
                var stockChanges = new Dictionary<string, (Product product, int oldStock)>();
                foreach (var product in productsToUpdate)
                {
                    var originalProduct = await context.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Ean == product.Ean);
                    if (originalProduct != null)
                    {
                        stockChanges[product.Ean] = (product, originalProduct.StockQuantity);
                    }
                    context.Products.Update(product);
                }

                await context.SaveChangesAsync();

                // Create stock movement records for each product sold
                foreach (var kvp in stockChanges)
                {
                    var product = kvp.Value.product;
                    var oldStock = kvp.Value.oldStock;
                    var quantityChange = product.StockQuantity - oldStock;

                    var stockMovement = new StockMovement
                    {
                        ProductEan = product.Ean,
                        ProductName = product.Name,
                        MovementType = StockMovementType.Sale,
                        QuantityChange = quantityChange,
                        StockBefore = oldStock,
                        StockAfter = product.StockQuantity,
                        Timestamp = DateTime.Now,
                        UserName = userName,
                        Notes = $"Prodej (účtenka {receipt.ReceiptYear}/{receipt.ReceiptSequence})",
                        ReferenceId = receipt.ReceiptId
                    };
                    await context.StockMovements.AddAsync(stockMovement);
                }

                await context.SaveChangesAsync();
                await transaction.CommitAsync();

                return (true, null);
            }
            catch (DbUpdateException ex)
            {
                await transaction.RollbackAsync();
                Debug.WriteLine($"Database update error during sale completion: {ex.InnerException?.Message ?? ex.Message}");
                return (false, "Došlo k chybě při ukládání do databáze. Zkuste to prosím znovu.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Debug.WriteLine($"Generic error during sale completion: {ex.Message}");
                return (false, "Došlo k neočekávané chybě.");
            }
        }

        public async Task DeleteProductAsync(string ean)
        {
            using var context = _contextFactory.CreateDbContext();
            var product = await context.Products.FirstOrDefaultAsync(p => p.Ean == ean);
            if (product != null)
            {
                context.Products.Remove(product);
                await context.SaveChangesAsync();
            }
        }

        public async Task SaveReceiptAsync(Receipt receipt)
        {
            using var context = _contextFactory.CreateDbContext();
            await context.Receipts.AddAsync(receipt);
            await context.SaveChangesAsync();
        }

        public async Task<List<Receipt>> GetReceiptsAsync()
        {
            using var context = _contextFactory.CreateDbContext();
            return await context.Receipts.Include(r => r.Items).ToListAsync();
        }

        public async Task<List<Receipt>> GetReceiptsAsync(DateTime startDate, DateTime endDate)
        {
            using var context = _contextFactory.CreateDbContext();
            return await context.Receipts.Include(r => r.Items).Where(r => r.SaleDate >= startDate && r.SaleDate <= endDate).ToListAsync();
        }

        public async Task<Receipt> GetReceiptByIdAsync(int receiptId)
        {
            using var context = _contextFactory.CreateDbContext();
            return await context.Receipts
                                 .Include(r => r.Items)
                                 .FirstOrDefaultAsync(r => r.ReceiptId == receiptId);
        }

        public async Task DeleteReceiptAsync(int receiptId)
        {
            using var context = _contextFactory.CreateDbContext();
            var receipt = await context.Receipts
                                       .Include(r => r.Items)
                                       .FirstOrDefaultAsync(r => r.ReceiptId == receiptId);
            if (receipt != null)
            {
                context.Receipts.Remove(receipt);
                await context.SaveChangesAsync();
            }
        }

        public async Task<int> GetNextReceiptSequenceAsync(int year)
        {
            using var context = _contextFactory.CreateDbContext();
            var maxSequence = await context.Receipts
                                           .Where(r => r.ReceiptYear == year)
                                           .MaxAsync(r => (int?)r.ReceiptSequence);
            return (maxSequence ?? 0) + 1;
        }

        public async Task SaveReturnAsync(Return returnDocument)
        {
            using var context = _contextFactory.CreateDbContext();
            await context.Returns.AddAsync(returnDocument);
            await context.SaveChangesAsync();
        }

        public async Task<List<Return>> GetReturnsAsync()
        {
            using var context = _contextFactory.CreateDbContext();
            return await context.Returns.Include(r => r.Items).ToListAsync();
        }

        public async Task<List<Return>> GetReturnsAsync(DateTime startDate, DateTime endDate)
        {
            using var context = _contextFactory.CreateDbContext();
            return await context.Returns.Include(r => r.Items).Where(r => r.ReturnDate >= startDate && r.ReturnDate <= endDate).ToListAsync();
        }

        public async Task<int> GetTotalReturnedQuantityForProductOnReceiptAsync(int originalReceiptId, string productEan)
        {
            using var context = _contextFactory.CreateDbContext();
            return await context.ReturnItems
                                 .Where(ri => ri.Return.OriginalReceiptId == originalReceiptId && ri.ProductEan == productEan)
                                 .SumAsync(ri => ri.ReturnedQuantity);
        }

        public async Task<List<CashRegisterEntry>> GetCashRegisterEntriesAsync()
        {
            using var context = _contextFactory.CreateDbContext();
            return await context.CashRegisterEntries.ToListAsync();
        }

        public async Task<List<CashRegisterEntry>> GetCashRegisterEntriesAsync(DateTime startDate, DateTime endDate)
        {
            using var context = _contextFactory.CreateDbContext();
            return await context.CashRegisterEntries
                                 .Where(e => e.Timestamp >= startDate && e.Timestamp <= endDate)
                                 .OrderByDescending(e => e.Timestamp)
                                 .ToListAsync();
        }

        public async Task<List<VatConfig>> GetVatConfigsAsync()
        {
            using var context = _contextFactory.CreateDbContext();
            return await context.VatConfigs.ToListAsync();
        }

        public async Task SaveVatConfigsAsync(IEnumerable<VatConfig> vatConfigs)
        {
            using var context = _contextFactory.CreateDbContext();
            foreach (var config in vatConfigs)
            {
                var existing = await context.VatConfigs.FindAsync(config.CategoryName);
                if (existing != null)
                {
                    existing.Rate = config.Rate;
                }
                else
                {
                    context.VatConfigs.Add(config);
                }
            }
            await context.SaveChangesAsync();
        }

        // Category Management
        public async Task<int> GetProductCountByCategoryAsync(string categoryName)
        {
            using var context = _contextFactory.CreateDbContext();
            return await context.Products.CountAsync(p => p.Category == categoryName);
        }

        public async Task UpdateProductsCategoryAsync(string oldCategoryName, string newCategoryName)
        {
            using var context = _contextFactory.CreateDbContext();
            var products = await context.Products.Where(p => p.Category == oldCategoryName).ToListAsync();
            foreach (var product in products)
            {
                product.Category = newCategoryName;
            }
            await context.SaveChangesAsync();
        }

        public async Task DeleteVatConfigAsync(string categoryName)
        {
            using var context = _contextFactory.CreateDbContext();
            var config = await context.VatConfigs.FindAsync(categoryName);
            if (config != null)
            {
                context.VatConfigs.Remove(config);
                await context.SaveChangesAsync();
            }
        }

        // User Management
        public async Task<List<User>> GetAllUsersAsync()
        {
            using var context = _contextFactory.CreateDbContext();
            return await context.Users.ToListAsync();
        }

        public async Task<User> GetUserByUsernameAsync(string username)
        {
            using var context = _contextFactory.CreateDbContext();
            return await context.Users.FirstOrDefaultAsync(u => u.Username == username);
        }

        public async Task CreateUserAsync(User user)
        {
            using var context = _contextFactory.CreateDbContext();
            await context.Users.AddAsync(user);
            await context.SaveChangesAsync();
        }

        public async Task UpdateUserAsync(User user)
        {
            using var context = _contextFactory.CreateDbContext();
            context.Users.Update(user);
            await context.SaveChangesAsync();
        }

        public async Task SetUserActiveAsync(int userId, bool isActive)
        {
            using var context = _contextFactory.CreateDbContext();
            var user = await context.Users.FindAsync(userId);
            if (user != null)
            {
                user.IsActive = isActive;
                await context.SaveChangesAsync();
            }
        }

        // Stock Movements
        public async Task AddStockMovementAsync(StockMovement movement)
        {
            using var context = _contextFactory.CreateDbContext();
            await context.StockMovements.AddAsync(movement);
            await context.SaveChangesAsync();
        }

        public async Task<List<StockMovement>> GetStockMovementsAsync()
        {
            using var context = _contextFactory.CreateDbContext();
            return await context.StockMovements
                .OrderByDescending(sm => sm.Timestamp)
                .ToListAsync();
        }

        public async Task<List<StockMovement>> GetStockMovementsAsync(DateTime startDate, DateTime endDate)
        {
            using var context = _contextFactory.CreateDbContext();
            return await context.StockMovements
                .Where(sm => sm.Timestamp >= startDate && sm.Timestamp <= endDate)
                .OrderByDescending(sm => sm.Timestamp)
                .ToListAsync();
        }

        public async Task<List<StockMovement>> GetStockMovementsByProductAsync(string productEan)
        {
            using var context = _contextFactory.CreateDbContext();
            return await context.StockMovements
                .Where(sm => sm.ProductEan == productEan)
                .OrderByDescending(sm => sm.Timestamp)
                .ToListAsync();
        }

        public async Task<List<StockMovement>> GetStockMovementsByTypeAsync(StockMovementType movementType)
        {
            using var context = _contextFactory.CreateDbContext();
            return await context.StockMovements
                .Where(sm => sm.MovementType == movementType)
                .OrderByDescending(sm => sm.Timestamp)
                .ToListAsync();
        }

        // Gift Cards
        public async Task<GiftCard> GetGiftCardByEanAsync(string ean)
        {
            using var context = _contextFactory.CreateDbContext();
            return await context.GiftCards.FirstOrDefaultAsync(gc => gc.Ean == ean);
        }

        public async Task<List<GiftCard>> GetAllGiftCardsAsync()
        {
            using var context = _contextFactory.CreateDbContext();
            return await context.GiftCards
                .OrderByDescending(gc => gc.Id)
                .ToListAsync();
        }

        public async Task<List<GiftCard>> GetGiftCardsByStatusAsync(GiftCardStatus status)
        {
            using var context = _contextFactory.CreateDbContext();
            return await context.GiftCards
                .Where(gc => gc.Status == status)
                .OrderByDescending(gc => gc.Id)
                .ToListAsync();
        }

        public async Task AddGiftCardAsync(GiftCard giftCard)
        {
            using var context = _contextFactory.CreateDbContext();
            await context.GiftCards.AddAsync(giftCard);
            await context.SaveChangesAsync();
        }

        public async Task UpdateGiftCardAsync(GiftCard giftCard)
        {
            using var context = _contextFactory.CreateDbContext();
            context.GiftCards.Update(giftCard);
            await context.SaveChangesAsync();
        }

        public async Task DeleteGiftCardAsync(string ean)
        {
            using var context = _contextFactory.CreateDbContext();
            var giftCard = await context.GiftCards.FirstOrDefaultAsync(gc => gc.Ean == ean);
            if (giftCard != null)
            {
                context.GiftCards.Remove(giftCard);
                await context.SaveChangesAsync();
            }
        }
    }
}
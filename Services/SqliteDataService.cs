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

        public async Task<(bool Success, string ErrorMessage)> CompleteSaleAsync(Receipt receipt, List<Product> productsToUpdate)
        {
            using var context = _contextFactory.CreateDbContext();
            using var transaction = await context.Database.BeginTransactionAsync();
            try
            {
                await context.Receipts.AddAsync(receipt);

                foreach (var product in productsToUpdate)
                {
                    context.Products.Update(product);
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
    }
}
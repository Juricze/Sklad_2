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
        private readonly DatabaseContext _context;

        public SqliteDataService(DatabaseContext context)
        {
            _context = context;
        }

        public async Task AddProductAsync(Product product)
        {
            await _context.Products.AddAsync(product);
            await _context.SaveChangesAsync();
        }

        public async Task<Product> GetProductAsync(string ean)
        {
            var product = await _context.Products.FirstOrDefaultAsync(p => p.Ean == ean);
            return product;
        }

        public async Task<List<Product>> GetProductsAsync()
        {
            var products = await _context.Products.ToListAsync();
            return products;
        }

        public void UpdateProductAsync(Product product)
        {
            _context.Products.Update(product);
            // await _context.SaveChangesAsync(); // Removed for atomic transactions
        }

        public async Task<(bool Success, string ErrorMessage)> CompleteSaleAsync(Receipt receipt, List<Product> productsToUpdate)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Add the new receipt and its items
                await _context.Receipts.AddAsync(receipt);

                // Mark all products to be updated
                foreach (var product in productsToUpdate)
                {
                    _context.Products.Update(product);
                }

                // Save all changes in a single transaction
                await _context.SaveChangesAsync();
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
            var product = await GetProductAsync(ean);
            if (product != null)
            {
                _context.Products.Remove(product);
                await _context.SaveChangesAsync();
            }
        }

        public async Task SaveReceiptAsync(Receipt receipt)
        {
            await _context.Receipts.AddAsync(receipt);
            await _context.SaveChangesAsync();
        }

        public async Task<List<Receipt>> GetReceiptsAsync()
        {
            return await _context.Receipts.Include(r => r.Items).ToListAsync();
        }

        public async Task<List<Receipt>> GetReceiptsAsync(DateTime startDate, DateTime endDate)
        {
            return await _context.Receipts.Include(r => r.Items).Where(r => r.SaleDate >= startDate && r.SaleDate <= endDate).ToListAsync();
        }

        public async Task<Receipt> GetReceiptByIdAsync(int receiptId)
        {
            return await _context.Receipts
                                 .Include(r => r.Items)
                                 .FirstOrDefaultAsync(r => r.ReceiptId == receiptId);
        }

        public async Task SaveReturnAsync(Return returnDocument)
        {
            await _context.Returns.AddAsync(returnDocument);
            await _context.SaveChangesAsync();
        }

        public async Task<List<Return>> GetReturnsAsync()
        {
            return await _context.Returns.Include(r => r.Items).ToListAsync();
        }

        public async Task<List<Return>> GetReturnsAsync(DateTime startDate, DateTime endDate)
        {
            return await _context.Returns.Include(r => r.Items).Where(r => r.ReturnDate >= startDate && r.ReturnDate <= endDate).ToListAsync();
        }

        public async Task<int> GetTotalReturnedQuantityForProductOnReceiptAsync(int originalReceiptId, string productEan)
        {
            return await _context.ReturnItems
                                 .Where(ri => ri.Return.OriginalReceiptId == originalReceiptId && ri.ProductEan == productEan)
                                 .SumAsync(ri => ri.ReturnedQuantity);
        }

        public async Task<List<CashRegisterEntry>> GetCashRegisterEntriesAsync()
        {
            return await _context.CashRegisterEntries.ToListAsync();
        }
    }
}
using Sklad_2.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Sklad_2.Services
{
    public interface IDataService
    {
        Task AddProductAsync(Product product);
        Task<Product> GetProductAsync(string ean);
        Task<List<Product>> GetProductsAsync();
        void UpdateProductAsync(Product product);
        Task DeleteProductAsync(string ean);
        Task SaveReceiptAsync(Receipt receipt);
        Task<List<Receipt>> GetReceiptsAsync();
        Task<List<Receipt>> GetReceiptsAsync(DateTime startDate, DateTime endDate);
        Task<Receipt> GetReceiptByIdAsync(int receiptId);
        Task SaveReturnAsync(Return returnDocument);
        Task<List<Return>> GetReturnsAsync();
        Task<List<Return>> GetReturnsAsync(DateTime startDate, DateTime endDate);
        Task<int> GetTotalReturnedQuantityForProductOnReceiptAsync(int originalReceiptId, string productEan);
        Task<(bool Success, string ErrorMessage)> CompleteSaleAsync(Receipt receipt, List<Product> productsToUpdate);
        Task<List<CashRegisterEntry>> GetCashRegisterEntriesAsync();
    }
}
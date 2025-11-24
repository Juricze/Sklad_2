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
        Task UpdateProductAsync(Product product);
        Task DeleteProductAsync(string ean);
        Task SaveReceiptAsync(Receipt receipt);
        Task<List<Receipt>> GetReceiptsAsync();
        Task<List<Receipt>> GetReceiptsAsync(DateTime startDate, DateTime endDate);
        Task<Receipt> GetReceiptByIdAsync(int receiptId);
        Task DeleteReceiptAsync(int receiptId);
        Task<int> GetNextReceiptSequenceAsync(int year);
        Task SaveReturnAsync(Return returnDocument);
        Task<List<Return>> GetReturnsAsync();
        Task<List<Return>> GetReturnsAsync(DateTime startDate, DateTime endDate);
        Task<int> GetTotalReturnedQuantityForProductOnReceiptAsync(int originalReceiptId, string productEan);
        Task<(bool Success, string ErrorMessage)> CompleteSaleAsync(Receipt receipt, List<Product> productsToUpdate, string userName);
        Task<List<CashRegisterEntry>> GetCashRegisterEntriesAsync();
        Task<List<CashRegisterEntry>> GetCashRegisterEntriesAsync(DateTime startDate, DateTime endDate);

        // VAT Configs
        Task<List<VatConfig>> GetVatConfigsAsync();
        Task SaveVatConfigsAsync(IEnumerable<VatConfig> vatConfigs);

        // Category Management
        Task<int> GetProductCountByCategoryAsync(string categoryName);
        Task UpdateProductsCategoryAsync(string oldCategoryName, string newCategoryName);
        Task DeleteVatConfigAsync(string categoryName);

        // User Management
        Task<List<User>> GetAllUsersAsync();
        Task<User> GetUserByUsernameAsync(string username);
        Task CreateUserAsync(User user);
        Task UpdateUserAsync(User user);
        Task SetUserActiveAsync(int userId, bool isActive);

        // Stock Movements
        Task AddStockMovementAsync(StockMovement movement);
        Task<List<StockMovement>> GetStockMovementsAsync();
        Task<List<StockMovement>> GetStockMovementsAsync(DateTime startDate, DateTime endDate);
        Task<List<StockMovement>> GetStockMovementsByProductAsync(string productEan);
        Task<List<StockMovement>> GetStockMovementsByTypeAsync(StockMovementType movementType);

        // Gift Cards
        Task<GiftCard> GetGiftCardByEanAsync(string ean);
        Task<List<GiftCard>> GetAllGiftCardsAsync();
        Task<List<GiftCard>> GetGiftCardsByStatusAsync(GiftCardStatus status);
        Task AddGiftCardAsync(GiftCard giftCard);
        Task UpdateGiftCardAsync(GiftCard giftCard);
        Task DeleteGiftCardAsync(string ean);
    }
}
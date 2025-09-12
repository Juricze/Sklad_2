using Sklad_2.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Sklad_2.Services
{
    public interface IDataService
    {
        Task<Product> GetProductAsync(string ean);
        Task<List<Product>> GetProductsAsync();
        Task AddProductAsync(Product product);
        Task UpdateProductAsync(Product product);
        Task DeleteProductAsync(string ean);
        Task SaveReceiptAsync(Models.Receipt receipt);
    }
}

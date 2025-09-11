using Microsoft.EntityFrameworkCore;
using Sklad_2.Data;
using Sklad_2.Models;
using System.Collections.Generic;
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
            return await _context.Products.FirstOrDefaultAsync(p => p.Ean == ean);
        }

        public async Task<List<Product>> GetProductsAsync()
        {
            return await _context.Products.ToListAsync();
        }

        public async Task UpdateProductAsync(Product product)
        {
            _context.Products.Update(product);
            await _context.SaveChangesAsync();
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
    }
}

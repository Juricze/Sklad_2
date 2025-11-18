using Microsoft.EntityFrameworkCore;
using Sklad_2.Models;
using System;
using System.Diagnostics;
using System.IO;

namespace Sklad_2.Data
{
    public class DatabaseContext : DbContext
    {
        public DbSet<Product> Products { get; set; }
        public DbSet<Receipt> Receipts { get; set; } // New
        public DbSet<ReceiptItem> ReceiptItems { get; set; } // New
        public DbSet<Return> Returns { get; set; }
        public DbSet<ReturnItem> ReturnItems { get; set; }
        public DbSet<CashRegisterEntry> CashRegisterEntries { get; set; }
        public DbSet<VatConfig> VatConfigs { get; set; }
        public DbSet<User> Users { get; set; }

        public DatabaseContext()
        {
            try
            {
                Database.EnsureCreated();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Chyba při inicializaci databáze: {ex.Message}");
                throw new InvalidOperationException("Nepodařilo se vytvořit nebo připojit k databázi. Zkontrolujte přístupová práva.", ex);
            }
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // Použití LocalApplicationData místo BaseDirectory (EXE složky)
            var localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dbFolderPath = Path.Combine(localAppDataPath, "Sklad_2_Data");

            try
            {
                Directory.CreateDirectory(dbFolderPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Chyba při vytváření složky pro databázi: {ex.Message}");
                throw;
            }

            var dbPath = Path.Combine(dbFolderPath, "sklad.db");
            Debug.WriteLine($"Cesta k databázi: {dbPath}");
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }
    }
}

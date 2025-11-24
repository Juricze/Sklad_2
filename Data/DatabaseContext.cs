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
        public DbSet<StockMovement> StockMovements { get; set; }
        public DbSet<GiftCard> GiftCards { get; set; }

        public DatabaseContext()
        {
            try
            {
                Debug.WriteLine("DatabaseContext: Creating database...");
                var created = Database.EnsureCreated();
                Debug.WriteLine($"DatabaseContext: Database created = {created}");

                // Log all tables
                var connection = Database.GetDbConnection();
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = "SELECT name FROM sqlite_master WHERE type='table'";
                using (var reader = command.ExecuteReader())
                {
                    Debug.WriteLine("DatabaseContext: Tables in database:");
                    while (reader.Read())
                    {
                        Debug.WriteLine($"  - {reader.GetString(0)}");
                    }
                }
                connection.Close();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Chyba při inicializaci databáze: {ex.Message}");
                throw new InvalidOperationException("Nepodařilo se vytvořit nebo připojit k databázi. Zkontrolujte přístupová práva.", ex);
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Set unique index on GiftCard.Ean
            modelBuilder.Entity<GiftCard>()
                .HasIndex(gc => gc.Ean)
                .IsUnique();
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

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
            // Default constructor for existing code
        }

        public DatabaseContext(DbContextOptions<DatabaseContext> options) : base(options)
        {
            // Constructor for dependency injection and design-time factory
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
            // Only configure if not already configured (for design-time vs runtime)
            if (!optionsBuilder.IsConfigured)
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
}

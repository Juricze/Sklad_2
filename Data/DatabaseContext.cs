using Microsoft.EntityFrameworkCore;
using Sklad_2.Models;
using System;
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

        public DatabaseContext()
        {
            Database.EnsureCreated();
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appFolderPath = Path.Combine(appDataPath, "Sklad_2_Data");
            Directory.CreateDirectory(appFolderPath);
            var dbPath = Path.Combine(appFolderPath, "sklad.db");
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }
    }
}

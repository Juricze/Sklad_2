using Microsoft.EntityFrameworkCore;
using Sklad_2.Models;
using System;
using System.IO;

namespace Sklad_2.Data
{
    public class DatabaseContext : DbContext
    {
        public DbSet<Product> Products { get; set; }

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

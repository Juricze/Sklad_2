using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System;
using System.IO;

namespace Sklad_2.Data
{
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<DatabaseContext>
    {
        public DatabaseContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<DatabaseContext>();
            
            // Use a design-time database path
            string userDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appDataPath = Path.Combine(userDataPath, "Sklad_2_Data");
            Directory.CreateDirectory(appDataPath);
            string dbPath = Path.Combine(appDataPath, "sklad.db");
            
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
            
            return new DatabaseContext(optionsBuilder.Options);
        }
    }
}
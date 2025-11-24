using Microsoft.EntityFrameworkCore;
using Sklad_2.Data;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;
using System.Diagnostics;

namespace Sklad_2.Services
{
    public class DatabaseMigrationService : IDatabaseMigrationService
    {
        private readonly IDbContextFactory<DatabaseContext> _contextFactory;
        
        // Current schema version - increment when adding new migrations
        private const int CURRENT_SCHEMA_VERSION = 5; // Version 5: Add testField to Products
        
        public DatabaseMigrationService(IDbContextFactory<DatabaseContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<bool> MigrateToLatestAsync()
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();
                
                // Ensure database exists
                await context.Database.EnsureCreatedAsync();
                
                // Create schema_versions table if not exists
                await EnsureSchemaVersionTableExistsAsync(context);
                
                var currentVersion = await GetCurrentSchemaVersionAsync();
                Debug.WriteLine($"DatabaseMigrationService: Current schema version: {currentVersion}");
                
                // Apply migrations step by step
                while (currentVersion < CURRENT_SCHEMA_VERSION)
                {
                    var nextVersion = currentVersion + 1;
                    Debug.WriteLine($"DatabaseMigrationService: Migrating to version {nextVersion}");
                    
                    var success = await ApplyMigrationAsync(context, nextVersion);
                    if (!success)
                    {
                        Debug.WriteLine($"DatabaseMigrationService: Migration to version {nextVersion} FAILED");
                        return false;
                    }
                    
                    await UpdateSchemaVersionAsync(context, nextVersion);
                    currentVersion = nextVersion;
                    Debug.WriteLine($"DatabaseMigrationService: Successfully migrated to version {nextVersion}");
                }
                
                Debug.WriteLine($"DatabaseMigrationService: Database is up to date (version {CURRENT_SCHEMA_VERSION})");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DatabaseMigrationService: Migration failed: {ex.Message}");
                return false;
            }
        }

        public async Task<int> GetCurrentSchemaVersionAsync()
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();
                
                // Check if schema_versions table exists
                var connection = context.Database.GetDbConnection();
                await connection.OpenAsync();
                
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='schema_versions'";
                var result = await command.ExecuteScalarAsync();
                
                if (result == null)
                {
                    // Table doesn't exist, this is a new database or version 0
                    return 0;
                }
                
                // Get current version
                command.CommandText = "SELECT MAX(version) FROM schema_versions";
                var versionResult = await command.ExecuteScalarAsync();
                
                if (versionResult == null || versionResult == DBNull.Value)
                {
                    return 0;
                }
                
                return Convert.ToInt32(versionResult);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DatabaseMigrationService: Error getting current version: {ex.Message}");
                return 0;
            }
        }

        public async Task<bool> IsDatabaseUpToDateAsync()
        {
            var currentVersion = await GetCurrentSchemaVersionAsync();
            return currentVersion >= CURRENT_SCHEMA_VERSION;
        }

        private async Task EnsureSchemaVersionTableExistsAsync(DatabaseContext context)
        {
            var connection = context.Database.GetDbConnection();
            await connection.OpenAsync();
            
            using var command = connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS schema_versions (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    version INTEGER NOT NULL UNIQUE,
                    applied_at TEXT NOT NULL,
                    description TEXT NOT NULL
                )";
            
            await command.ExecuteNonQueryAsync();
        }

        private async Task<bool> ApplyMigrationAsync(DatabaseContext context, int version)
        {
            try
            {
                switch (version)
                {
                    case 1:
                        return await ApplyMigration_V1_InitialSchema(context);
                    case 2:
                        return await ApplyMigration_V2_AddDiscountFields(context);
                    case 3:
                        return await ApplyMigration_V3_AddMissingFields(context);
                    case 4:
                        return await ApplyMigration_V4_FixReceiptItemsConstraints(context);
                    case 5:
                        return await ApplyMigration_V5_AddTestField(context);
                    default:
                        Debug.WriteLine($"DatabaseMigrationService: Unknown migration version: {version}");
                        return false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DatabaseMigrationService: Error applying migration v{version}: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> ApplyMigration_V1_InitialSchema(DatabaseContext context)
        {
            // Migration V1: This migration handles existing databases created with EnsureCreated()
            // For new databases, EnsureCreated() will create the full schema including discount fields
            // For existing databases, this migration is a no-op since the schema already exists
            
            Debug.WriteLine("DatabaseMigrationService: Applying V1 - Initial Schema (no-op for existing databases)");
            
            // Check if tables already exist (from EnsureCreated)
            var connection = context.Database.GetDbConnection();
            await connection.OpenAsync();
            
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Products'";
            var tableCount = (long)await command.ExecuteScalarAsync();
            
            if (tableCount > 0)
            {
                Debug.WriteLine("DatabaseMigrationService: Tables already exist, V1 migration is a no-op");
                return true;
            }
            
            // If no tables exist, this should not happen since we call EnsureCreated first
            Debug.WriteLine("DatabaseMigrationService: No tables found, this should not happen");
            return false;
        }

        private async Task<bool> ApplyMigration_V2_AddDiscountFields(DatabaseContext context)
        {
            Debug.WriteLine("DatabaseMigrationService: Applying V2 - Add Discount Fields");
            
            var connection = context.Database.GetDbConnection();
            await connection.OpenAsync();
            
            var migrations = new List<string>
            {
                // Add discount fields to Products table
                "ALTER TABLE Products ADD COLUMN DiscountPercent REAL NULL",
                "ALTER TABLE Products ADD COLUMN DiscountValidFrom TEXT NULL",
                "ALTER TABLE Products ADD COLUMN DiscountValidTo TEXT NULL", 
                "ALTER TABLE Products ADD COLUMN DiscountReason TEXT NULL DEFAULT ''",
                
                // Add discount fields to ReceiptItems table
                "ALTER TABLE ReceiptItems ADD COLUMN DiscountPercent REAL NULL",
                "ALTER TABLE ReceiptItems ADD COLUMN OriginalUnitPrice REAL NULL DEFAULT 0",
                "ALTER TABLE ReceiptItems ADD COLUMN DiscountReason TEXT NULL",
                
                // Add manual discount setting to prevent breaking existing installs
                // Note: AppSettings is JSON-based, so no SQL migration needed for AllowManualDiscounts
            };
            
            foreach (var sql in migrations)
            {
                try
                {
                    using var command = connection.CreateCommand();
                    command.CommandText = sql;
                    await command.ExecuteNonQueryAsync();
                    Debug.WriteLine($"DatabaseMigrationService: Executed: {sql}");
                }
                catch (Exception ex)
                {
                    // Column might already exist (if database was created after discount implementation)
                    if (ex.Message.Contains("duplicate column name"))
                    {
                        Debug.WriteLine($"DatabaseMigrationService: Column already exists, skipping: {sql}");
                        continue;
                    }
                    
                    Debug.WriteLine($"DatabaseMigrationService: Error executing: {sql} - {ex.Message}");
                    throw;
                }
            }
            
            return true;
        }

        private async Task<bool> ApplyMigration_V3_AddMissingFields(DatabaseContext context)
        {
            Debug.WriteLine("DatabaseMigrationService: Applying V3 - Add Missing Fields");
            
            var connection = context.Database.GetDbConnection();
            await connection.OpenAsync();
            
            var migrations = new List<string>
            {
                // Add missing fields to GiftCards table
                "ALTER TABLE GiftCards ADD COLUMN Status INTEGER NULL DEFAULT 0",
                "ALTER TABLE GiftCards ADD COLUMN Notes TEXT NULL DEFAULT ''",
                "ALTER TABLE GiftCards ADD COLUMN IssuedByUser TEXT NULL DEFAULT ''",
                "ALTER TABLE GiftCards ADD COLUMN UsedByUser TEXT NULL DEFAULT ''",
                "ALTER TABLE GiftCards ADD COLUMN CancelReason TEXT NULL DEFAULT ''",
                
                // Add missing fields to Users table
                "ALTER TABLE Users ADD COLUMN IsActive INTEGER NOT NULL DEFAULT 1",
                "ALTER TABLE Users ADD COLUMN CreatedDate TEXT NOT NULL DEFAULT ''",
                
                // Add missing fields to StockMovements table
                "ALTER TABLE StockMovements ADD COLUMN Notes TEXT NULL DEFAULT ''"
            };
            
            foreach (var sql in migrations)
            {
                try
                {
                    using var command = connection.CreateCommand();
                    command.CommandText = sql;
                    await command.ExecuteNonQueryAsync();
                    Debug.WriteLine($"DatabaseMigrationService: Executed: {sql}");
                }
                catch (Exception ex)
                {
                    // Column might already exist
                    if (ex.Message.Contains("duplicate column name"))
                    {
                        Debug.WriteLine($"DatabaseMigrationService: Column already exists, skipping: {sql}");
                        continue;
                    }
                    
                    Debug.WriteLine($"DatabaseMigrationService: Error executing: {sql} - {ex.Message}");
                    throw;
                }
            }
            
            return true;
        }

        private async Task<bool> ApplyMigration_V4_FixReceiptItemsConstraints(DatabaseContext context)
        {
            Debug.WriteLine("DatabaseMigrationService: Applying V4 - Fix ReceiptItems Constraints");
            
            var connection = context.Database.GetDbConnection();
            await connection.OpenAsync();
            
            // SQLite doesn't support ALTER COLUMN, so we need to recreate table structure
            // But since this is about NULL constraints, we can work around it by updating existing NULL values
            var migrations = new List<string>
            {
                // Update any NULL values in OriginalUnitPrice to 0
                "UPDATE ReceiptItems SET OriginalUnitPrice = 0 WHERE OriginalUnitPrice IS NULL",
                
                // Update any NULL values in DiscountReason to empty string
                "UPDATE ReceiptItems SET DiscountReason = '' WHERE DiscountReason IS NULL"
            };
            
            foreach (var sql in migrations)
            {
                try
                {
                    using var command = connection.CreateCommand();
                    command.CommandText = sql;
                    var rowsAffected = await command.ExecuteNonQueryAsync();
                    Debug.WriteLine($"DatabaseMigrationService: Executed: {sql} (affected {rowsAffected} rows)");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"DatabaseMigrationService: Error executing: {sql} - {ex.Message}");
                    throw;
                }
            }
            
            return true;
        }

        private async Task<bool> ApplyMigration_V5_AddTestField(DatabaseContext context)
        {
            Debug.WriteLine("DatabaseMigrationService: Applying V5 - Add TestField to Products");
            
            var connection = context.Database.GetDbConnection();
            await connection.OpenAsync();
            
            var migrations = new List<string>
            {
                "ALTER TABLE Products ADD COLUMN TestField TEXT NULL DEFAULT ''"
            };
            
            foreach (var sql in migrations)
            {
                try
                {
                    using var command = connection.CreateCommand();
                    command.CommandText = sql;
                    await command.ExecuteNonQueryAsync();
                    Debug.WriteLine($"DatabaseMigrationService: Executed: {sql}");
                }
                catch (Exception ex)
                {
                    // Column might already exist
                    if (ex.Message.Contains("duplicate column name"))
                    {
                        Debug.WriteLine($"DatabaseMigrationService: Column already exists, skipping: {sql}");
                        continue;
                    }
                    
                    Debug.WriteLine($"DatabaseMigrationService: Error executing: {sql} - {ex.Message}");
                    throw;
                }
            }
            
            return true;
        }

        private async Task UpdateSchemaVersionAsync(DatabaseContext context, int version)
        {
            var connection = context.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync();
            }
            
            using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO schema_versions (version, applied_at, description) 
                VALUES (@version, @appliedAt, @description)";
            
            var versionParam = command.CreateParameter();
            versionParam.ParameterName = "@version";
            versionParam.Value = version;
            command.Parameters.Add(versionParam);
            
            var appliedAtParam = command.CreateParameter();
            appliedAtParam.ParameterName = "@appliedAt";
            appliedAtParam.Value = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
            command.Parameters.Add(appliedAtParam);
            
            var descriptionParam = command.CreateParameter();
            descriptionParam.ParameterName = "@description";
            descriptionParam.Value = GetMigrationDescription(version);
            command.Parameters.Add(descriptionParam);
            
            await command.ExecuteNonQueryAsync();
        }

        private string GetMigrationDescription(int version)
        {
            return version switch
            {
                1 => "Initial schema with all tables",
                2 => "Add discount fields to Products and ReceiptItems tables", 
                3 => "Add missing fields to GiftCards, Users and StockMovements tables",
                4 => "Fix ReceiptItems NULL constraints for OriginalUnitPrice and DiscountReason",
                5 => "Add TestField to Products table",
                _ => $"Unknown migration version {version}"
            };
        }
    }
}
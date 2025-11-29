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
        private const int CURRENT_SCHEMA_VERSION = 16; // Version 16: Add LoyaltyDiscountAmount to Returns for proper refund calculation
        
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
                var wasCreated = await EnsureDatabaseExistsAsync(context);

                // Create schema_versions table if not exists
                await EnsureSchemaVersionTableExistsAsync(context);

                var currentVersion = await GetCurrentSchemaVersionAsync();
                Debug.WriteLine($"DatabaseMigrationService: Current schema version: {currentVersion}");

                // If database was just created with EnsureCreated, it has the latest schema
                // Set version to CURRENT_SCHEMA_VERSION to skip all migrations
                if (wasCreated && currentVersion == 0)
                {
                    Debug.WriteLine($"DatabaseMigrationService: New database created, setting version to {CURRENT_SCHEMA_VERSION}");
                    await UpdateSchemaVersionAsync(context, CURRENT_SCHEMA_VERSION);
                    Debug.WriteLine($"DatabaseMigrationService: Database is up to date (version {CURRENT_SCHEMA_VERSION})");
                    return true;
                }

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

        private async Task<bool> EnsureDatabaseExistsAsync(DatabaseContext context)
        {
            var connection = context.Database.GetDbConnection();
            await connection.OpenAsync();

            // Check if Products table exists before EnsureCreated
            using var checkCommand = connection.CreateCommand();
            checkCommand.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Products'";
            var tableCount = (long)await checkCommand.ExecuteScalarAsync();

            bool databaseExisted = tableCount > 0;

            // Create database if it doesn't exist
            await context.Database.EnsureCreatedAsync();

            // Return true if database was just created (didn't exist before)
            return !databaseExisted;
        }

        private async Task EnsureSchemaVersionTableExistsAsync(DatabaseContext context)
        {
            var connection = context.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync();
            }

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
                    case 6:
                        return await ApplyMigration_V6_AddRedeemedGiftCardEan(context);
                    case 7:
                        return await ApplyMigration_V7_FixNullRedeemedGiftCardEan(context);
                    case 8:
                        return await ApplyMigration_V8_EnsureRedeemedGiftCardEanNotNull(context);
                    case 9:
                        return await ApplyMigration_V9_AddReturnNumbering(context);
                    case 10:
                        return await ApplyMigration_V10_AddDailyCloseAndPaymentBreakdown(context);
                    case 11:
                        return await ApplyMigration_V11_AddLoyaltyCustomer(context);
                    case 12:
                        return await ApplyMigration_V12_AddLoyaltyDiscountToReceipts(context);
                    case 13:
                        return await ApplyMigration_V13_FixLoyaltyCustomerNulls(context);
                    case 14:
                        return await ApplyMigration_V14_AddLoyaltyCustomerIdToReceipts(context);
                    case 15:
                        return await ApplyMigration_V15_AddLoyaltyCustomerIdToReturns(context);
                    case 16:
                        return await ApplyMigration_V16_AddLoyaltyDiscountToReturns(context);
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

        private async Task<bool> ApplyMigration_V6_AddRedeemedGiftCardEan(DatabaseContext context)
        {
            Debug.WriteLine("DatabaseMigrationService: Applying V6 - Add RedeemedGiftCardEan to Receipts");

            var connection = context.Database.GetDbConnection();
            await connection.OpenAsync();

            var migrations = new List<string>
            {
                "ALTER TABLE Receipts ADD COLUMN RedeemedGiftCardEan TEXT NULL DEFAULT ''"
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

        private async Task<bool> ApplyMigration_V7_FixNullRedeemedGiftCardEan(DatabaseContext context)
        {
            Debug.WriteLine("DatabaseMigrationService: Applying V7 - Fix NULL values in RedeemedGiftCardEan");

            var connection = context.Database.GetDbConnection();
            await connection.OpenAsync();

            var migrations = new List<string>
            {
                "UPDATE Receipts SET RedeemedGiftCardEan = '' WHERE RedeemedGiftCardEan IS NULL"
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

        private async Task<bool> ApplyMigration_V8_EnsureRedeemedGiftCardEanNotNull(DatabaseContext context)
        {
            Debug.WriteLine("DatabaseMigrationService: Applying V8 - Ensure RedeemedGiftCardEan is never NULL (re-run fix)");

            var connection = context.Database.GetDbConnection();
            await connection.OpenAsync();

            // Re-run the fix - some records may have been created between V6 and V7
            var migrations = new List<string>
            {
                "UPDATE Receipts SET RedeemedGiftCardEan = '' WHERE RedeemedGiftCardEan IS NULL"
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

        private async Task<bool> ApplyMigration_V9_AddReturnNumbering(DatabaseContext context)
        {
            Debug.WriteLine("DatabaseMigrationService: Applying V9 - Add ReturnYear and ReturnSequence to Returns");

            var connection = context.Database.GetDbConnection();
            await connection.OpenAsync();

            var migrations = new List<string>
            {
                // Add new columns for return document numbering
                "ALTER TABLE Returns ADD COLUMN ReturnYear INTEGER NOT NULL DEFAULT 0",
                "ALTER TABLE Returns ADD COLUMN ReturnSequence INTEGER NOT NULL DEFAULT 0"
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

            // Update existing returns with proper numbering based on their ReturnDate
            try
            {
                using var updateCommand = connection.CreateCommand();
                // Get all returns ordered by date
                updateCommand.CommandText = "SELECT ReturnId, ReturnDate FROM Returns ORDER BY ReturnDate";
                var yearSequences = new Dictionary<int, int>();

                using var reader = await updateCommand.ExecuteReaderAsync();
                var updates = new List<(int returnId, int year, int sequence)>();

                while (await reader.ReadAsync())
                {
                    var returnId = reader.GetInt32(0);
                    var returnDateStr = reader.GetString(1);
                    if (DateTime.TryParse(returnDateStr, out var returnDate))
                    {
                        var year = returnDate.Year;
                        if (!yearSequences.ContainsKey(year))
                        {
                            yearSequences[year] = 0;
                        }
                        yearSequences[year]++;
                        updates.Add((returnId, year, yearSequences[year]));
                    }
                }
                reader.Close();

                // Apply updates
                foreach (var (returnId, year, sequence) in updates)
                {
                    using var upd = connection.CreateCommand();
                    upd.CommandText = $"UPDATE Returns SET ReturnYear = {year}, ReturnSequence = {sequence} WHERE ReturnId = {returnId}";
                    await upd.ExecuteNonQueryAsync();
                    Debug.WriteLine($"DatabaseMigrationService: Updated Return {returnId} -> D{year}/{sequence:D4}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DatabaseMigrationService: Error updating existing returns: {ex.Message}");
                // Don't fail migration, columns are added, just numbering didn't update
            }

            return true;
        }

        private async Task<bool> ApplyMigration_V10_AddDailyCloseAndPaymentBreakdown(DatabaseContext context)
        {
            Debug.WriteLine("DatabaseMigrationService: Applying migration V10 - Add DailyClose table and payment breakdown");

            var connection = context.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync();
            }

            // 1. Create DailyCloses table
            var createDailyClosesTable = @"
                CREATE TABLE IF NOT EXISTS DailyCloses (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Date TEXT NOT NULL,
                    CashSales REAL NOT NULL DEFAULT 0,
                    CardSales REAL NOT NULL DEFAULT 0,
                    TotalSales REAL NOT NULL DEFAULT 0,
                    VatAmount REAL,
                    SellerName TEXT NOT NULL,
                    ReceiptNumberFrom TEXT NOT NULL,
                    ReceiptNumberTo TEXT NOT NULL,
                    ClosedAt TEXT NOT NULL
                );";

            // 2. Add CashAmount and CardAmount columns to Receipts table
            var addCashAmountColumn = @"
                ALTER TABLE Receipts ADD COLUMN CashAmount REAL NOT NULL DEFAULT 0;";

            var addCardAmountColumn = @"
                ALTER TABLE Receipts ADD COLUMN CardAmount REAL NOT NULL DEFAULT 0;";

            var sqlStatements = new[]
            {
                createDailyClosesTable,
                addCashAmountColumn,
                addCardAmountColumn
            };

            foreach (var sql in sqlStatements)
            {
                try
                {
                    using var command = connection.CreateCommand();
                    command.CommandText = sql;
                    await command.ExecuteNonQueryAsync();
                    Debug.WriteLine($"DatabaseMigrationService: Executed: {sql.Substring(0, Math.Min(50, sql.Length))}...");
                }
                catch (Exception ex)
                {
                    // If column already exists, that's okay (idempotent migration)
                    if (ex.Message.Contains("duplicate column name") || ex.Message.Contains("already exists"))
                    {
                        Debug.WriteLine($"DatabaseMigrationService: Column/Table already exists, continuing...");
                        continue;
                    }

                    Debug.WriteLine($"DatabaseMigrationService: Error executing: {sql} - {ex.Message}");
                    throw;
                }
            }

            // 3. Migrate existing receipts - set CashAmount based on PaymentMethod
            try
            {
                using var migrateCommand = connection.CreateCommand();
                migrateCommand.CommandText = @"
                    UPDATE Receipts
                    SET CashAmount = CASE
                        WHEN PaymentMethod = 'Karta' THEN 0
                        ELSE TotalAmount - CASE WHEN ContainsGiftCardRedemption = 1 THEN GiftCardRedemptionAmount ELSE 0 END
                    END,
                    CardAmount = CASE
                        WHEN PaymentMethod = 'Karta' THEN TotalAmount - CASE WHEN ContainsGiftCardRedemption = 1 THEN GiftCardRedemptionAmount ELSE 0 END
                        ELSE 0
                    END
                    WHERE CashAmount = 0 AND CardAmount = 0;";

                var rowsAffected = await migrateCommand.ExecuteNonQueryAsync();
                Debug.WriteLine($"DatabaseMigrationService: Migrated {rowsAffected} existing receipts with payment breakdown");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DatabaseMigrationService: Error migrating existing receipts: {ex.Message}");
                // Don't fail migration - tables/columns are created, just data migration didn't work
            }

            return true;
        }

        private async Task<bool> ApplyMigration_V11_AddLoyaltyCustomer(DatabaseContext context)
        {
            Debug.WriteLine("DatabaseMigrationService: Applying V11 - Add LoyaltyCustomer table");

            var connection = context.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync();
            }

            // Create LoyaltyCustomers table
            using var command = connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS LoyaltyCustomers (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    FirstName TEXT NOT NULL,
                    LastName TEXT NOT NULL,
                    Email TEXT NOT NULL,
                    CardEan TEXT NULL,
                    DiscountPercent REAL NOT NULL DEFAULT 0,
                    TotalPurchases REAL NOT NULL DEFAULT 0,
                    CreatedAt TEXT NOT NULL
                );

                CREATE UNIQUE INDEX IF NOT EXISTS IX_LoyaltyCustomers_Email ON LoyaltyCustomers(Email);
                CREATE UNIQUE INDEX IF NOT EXISTS IX_LoyaltyCustomers_CardEan ON LoyaltyCustomers(CardEan) WHERE CardEan IS NOT NULL AND CardEan != '';
            ";

            await command.ExecuteNonQueryAsync();
            Debug.WriteLine("DatabaseMigrationService: V11 - LoyaltyCustomers table created");

            return true;
        }

        private async Task<bool> ApplyMigration_V12_AddLoyaltyDiscountToReceipts(DatabaseContext context)
        {
            Debug.WriteLine("DatabaseMigrationService: Applying V12 - Add Loyalty Discount fields to Receipts");

            var connection = context.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync();
            }

            var migrations = new List<string>
            {
                "ALTER TABLE Receipts ADD COLUMN HasLoyaltyDiscount INTEGER NOT NULL DEFAULT 0",
                "ALTER TABLE Receipts ADD COLUMN LoyaltyCustomerEmail TEXT NOT NULL DEFAULT ''",
                "ALTER TABLE Receipts ADD COLUMN LoyaltyDiscountPercent REAL NOT NULL DEFAULT 0",
                "ALTER TABLE Receipts ADD COLUMN LoyaltyDiscountAmount REAL NOT NULL DEFAULT 0"
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

        private async Task<bool> ApplyMigration_V13_FixLoyaltyCustomerNulls(DatabaseContext context)
        {
            Debug.WriteLine("DatabaseMigrationService: Applying V13 - Fix NULL values in LoyaltyCustomers");

            var connection = context.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync();
            }

            var migrations = new List<string>
            {
                "UPDATE LoyaltyCustomers SET CardEan = '' WHERE CardEan IS NULL",
                "UPDATE LoyaltyCustomers SET FirstName = '' WHERE FirstName IS NULL",
                "UPDATE LoyaltyCustomers SET LastName = '' WHERE LastName IS NULL",
                "UPDATE LoyaltyCustomers SET Email = '' WHERE Email IS NULL"
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

        private async Task<bool> ApplyMigration_V14_AddLoyaltyCustomerIdToReceipts(DatabaseContext context)
        {
            Debug.WriteLine("DatabaseMigrationService: Applying V14 - Add LoyaltyCustomerId to Receipts");

            var connection = context.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync();
            }

            // Add LoyaltyCustomerId column to Receipts table (nullable INTEGER for foreign key to LoyaltyCustomers)
            var sql = "ALTER TABLE Receipts ADD COLUMN LoyaltyCustomerId INTEGER NULL";

            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = sql;
                await command.ExecuteNonQueryAsync();
                Debug.WriteLine($"DatabaseMigrationService: Added LoyaltyCustomerId column to Receipts");
            }
            catch (Exception ex)
            {
                // Column might already exist
                if (!ex.Message.Contains("duplicate column"))
                {
                    Debug.WriteLine($"DatabaseMigrationService: Error adding LoyaltyCustomerId: {ex.Message}");
                    throw;
                }
                Debug.WriteLine("DatabaseMigrationService: LoyaltyCustomerId column already exists");
            }

            return true;
        }

        private async Task<bool> ApplyMigration_V15_AddLoyaltyCustomerIdToReturns(DatabaseContext context)
        {
            Debug.WriteLine("DatabaseMigrationService: Applying V15 - Add LoyaltyCustomerId to Returns");

            var connection = context.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync();
            }

            // Add LoyaltyCustomerId column to Returns table (nullable INTEGER for foreign key to LoyaltyCustomers)
            var sql = "ALTER TABLE Returns ADD COLUMN LoyaltyCustomerId INTEGER NULL";

            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = sql;
                await command.ExecuteNonQueryAsync();
                Debug.WriteLine($"DatabaseMigrationService: Added LoyaltyCustomerId column to Returns");
            }
            catch (Exception ex)
            {
                // Column might already exist
                if (!ex.Message.Contains("duplicate column"))
                {
                    Debug.WriteLine($"DatabaseMigrationService: Error adding LoyaltyCustomerId to Returns: {ex.Message}");
                    throw;
                }
                Debug.WriteLine("DatabaseMigrationService: LoyaltyCustomerId column already exists in Returns");
            }

            return true;
        }

        private async Task<bool> ApplyMigration_V16_AddLoyaltyDiscountToReturns(DatabaseContext context)
        {
            Debug.WriteLine("DatabaseMigrationService: Applying V16 - Add LoyaltyDiscountAmount to Returns");

            var connection = context.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync();
            }

            // Add LoyaltyDiscountAmount column to Returns table (REAL with default 0)
            var sql = "ALTER TABLE Returns ADD COLUMN LoyaltyDiscountAmount REAL NOT NULL DEFAULT 0";

            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = sql;
                await command.ExecuteNonQueryAsync();
                Debug.WriteLine($"DatabaseMigrationService: Added LoyaltyDiscountAmount column to Returns");
            }
            catch (Exception ex)
            {
                // Column might already exist
                if (!ex.Message.Contains("duplicate column"))
                {
                    Debug.WriteLine($"DatabaseMigrationService: Error adding LoyaltyDiscountAmount to Returns: {ex.Message}");
                    throw;
                }
                Debug.WriteLine("DatabaseMigrationService: LoyaltyDiscountAmount column already exists in Returns");
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
                6 => "Add RedeemedGiftCardEan to Receipts table",
                7 => "Fix NULL values in RedeemedGiftCardEan column",
                8 => "Ensure RedeemedGiftCardEan is never NULL (re-run fix)",
                9 => "Add ReturnYear and ReturnSequence for return document numbering (D2025/0001)",
                10 => "Add DailyClose table and CashAmount/CardAmount payment breakdown to Receipts",
                11 => "Add LoyaltyCustomer table for loyalty program",
                12 => "Add LoyaltyDiscount fields to Receipts for loyalty program integration",
                13 => "Fix NULL values in LoyaltyCustomers table",
                14 => "Add LoyaltyCustomerId to Receipts for storno TotalPurchases tracking",
                15 => "Add LoyaltyCustomerId to Returns for return TotalPurchases tracking",
                16 => "Add LoyaltyDiscountAmount to Returns for proper refund calculation with loyalty discounts",
                _ => $"Unknown migration version {version}"
            };
        }
    }
}
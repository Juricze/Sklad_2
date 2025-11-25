# Migration Testing PowerShell Script
param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("new", "upgrade", "restore", "version", "menu")]
    [string]$TestType = "menu"
)

$DbPath = Join-Path $env:LOCALAPPDATA "Sklad_2_Data\sklad.db"
$BackupPath = Join-Path $env:LOCALAPPDATA "Sklad_2_Data\sklad_backup.db"
$DataFolder = Join-Path $env:LOCALAPPDATA "Sklad_2_Data"

Write-Host "================================" -ForegroundColor Cyan
Write-Host "   MIGRATION TESTING SCRIPT" -ForegroundColor Cyan  
Write-Host "================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Database path: $DbPath" -ForegroundColor Yellow
Write-Host "Backup path: $BackupPath" -ForegroundColor Yellow
Write-Host ""

function Test-NewDatabase {
    Write-Host "=== TEST 1: New Database ===" -ForegroundColor Green
    Write-Host ""
    
    # Backup current database
    if (Test-Path $DbPath) {
        Write-Host "Backing up current database..." -ForegroundColor Yellow
        Copy-Item $DbPath $BackupPath -Force
        Write-Host "✅ Current database backed up" -ForegroundColor Green
    }
    
    # Delete current database
    Write-Host "Deleting current database..." -ForegroundColor Yellow
    if (Test-Path $DbPath) {
        Remove-Item $DbPath -Force
        Write-Host "✅ Database deleted" -ForegroundColor Green
    } else {
        Write-Host "ℹ️  No database to delete" -ForegroundColor Blue
    }
    
    Write-Host ""
    Write-Host "🚀 NOW RUN THE APPLICATION!" -ForegroundColor Magenta
    Write-Host ""
    Write-Host "Expected results:" -ForegroundColor Cyan
    Write-Host "• New database created automatically" -ForegroundColor White
    Write-Host "• Schema version should be 2" -ForegroundColor White  
    Write-Host "• All discount features work immediately" -ForegroundColor White
    Write-Host "• Debug output shows: 'Successfully migrated to version 2'" -ForegroundColor White
    Write-Host ""
}

function Test-UpgradeDatabase {
    Write-Host "=== TEST 2: Database Upgrade ===" -ForegroundColor Green
    Write-Host ""
    
    # Backup current database
    if (Test-Path $DbPath) {
        Write-Host "Backing up current database..." -ForegroundColor Yellow
        Copy-Item $DbPath $BackupPath -Force  
        Write-Host "✅ Current database backed up" -ForegroundColor Green
    }
    
    # Create simulated old database
    Write-Host "Creating simulated OLD database (without discount fields)..." -ForegroundColor Yellow
    
    $oldDbSql = @"
-- Create old database schema (without discount fields)
CREATE TABLE IF NOT EXISTS Products (
    Ean TEXT PRIMARY KEY,
    Name TEXT NOT NULL,
    Category TEXT NOT NULL, 
    SalePrice REAL NOT NULL,
    PurchasePrice REAL NOT NULL,
    StockQuantity INTEGER NOT NULL,
    VatRate REAL NOT NULL
);

CREATE TABLE IF NOT EXISTS ReceiptItems (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ReceiptId INTEGER NOT NULL,
    ProductEan TEXT NOT NULL,
    ProductName TEXT NOT NULL,
    Quantity INTEGER NOT NULL,
    UnitPrice REAL NOT NULL,
    TotalPrice REAL NOT NULL,
    VatRate REAL NOT NULL,
    PriceWithoutVat REAL NOT NULL,
    VatAmount REAL NOT NULL
);

-- Insert sample data
INSERT OR REPLACE INTO Products (Ean, Name, Category, SalePrice, PurchasePrice, StockQuantity, VatRate)
VALUES ('1234567890123', 'Test Product', 'Test Category', 100.0, 80.0, 10, 21.0);

-- No schema_versions table = version 0
"@
    
    # Remove existing database
    if (Test-Path $DbPath) {
        Remove-Item $DbPath -Force
    }
    
    # Create directory if needed
    if (-not (Test-Path $DataFolder)) {
        New-Item -ItemType Directory -Path $DataFolder -Force | Out-Null
    }
    
    # Create old database
    try {
        # Try to use sqlite3 if available
        $sqliteExe = Get-Command sqlite3 -ErrorAction SilentlyContinue
        if ($sqliteExe) {
            $oldDbSql | & sqlite3 $DbPath
            Write-Host "✅ Old database created with SQLite3" -ForegroundColor Green
        } else {
            # Fallback: Use .NET SQLite
            Add-Type -Path "System.Data.SQLite.dll" -ErrorAction SilentlyContinue
            
            Write-Host "⚠️  SQLite3 not found in PATH" -ForegroundColor Yellow
            Write-Host "Please install SQLite3 or manually create old database" -ForegroundColor Yellow
            Write-Host ""
            Write-Host "Manual steps:" -ForegroundColor Cyan
            Write-Host "1. Download SQLite3 from https://sqlite.org/download.html" -ForegroundColor White
            Write-Host "2. Run: sqlite3 `"$DbPath`"" -ForegroundColor White  
            Write-Host "3. Execute the SQL above" -ForegroundColor White
            Write-Host ""
            return
        }
    } catch {
        Write-Host "❌ Error creating old database: $_" -ForegroundColor Red
        return
    }
    
    Write-Host ""
    Write-Host "🚀 NOW RUN THE APPLICATION!" -ForegroundColor Magenta
    Write-Host ""
    Write-Host "Expected results:" -ForegroundColor Cyan
    Write-Host "• Migration from version 0 to version 2" -ForegroundColor White
    Write-Host "• Discount columns added to existing tables" -ForegroundColor White
    Write-Host "• Existing data preserved (Test Product still there)" -ForegroundColor White
    Write-Host "• Debug output shows: 'Migrating to version 1' then 'Migrating to version 2'" -ForegroundColor White
    Write-Host ""
}

function Restore-Database {
    Write-Host "=== RESTORE: Original Database ===" -ForegroundColor Green
    Write-Host ""
    
    if (Test-Path $BackupPath) {
        Write-Host "Restoring original database from backup..." -ForegroundColor Yellow
        Copy-Item $BackupPath $DbPath -Force
        Write-Host "✅ Database restored from backup" -ForegroundColor Green
    } else {
        Write-Host "❌ No backup found at: $BackupPath" -ForegroundColor Red
    }
    Write-Host ""
}

function Check-SchemaVersion {
    Write-Host "=== CHECK: Current Schema Version ===" -ForegroundColor Green
    Write-Host ""
    Write-Host "Database path: $DbPath" -ForegroundColor Yellow
    
    if (Test-Path $DbPath) {
        Write-Host "✅ Database file exists" -ForegroundColor Green
        
        try {
            # Try to check schema version
            $sqliteExe = Get-Command sqlite3 -ErrorAction SilentlyContinue
            if ($sqliteExe) {
                Write-Host ""
                Write-Host "Current schema version:" -ForegroundColor Cyan
                & sqlite3 $DbPath "SELECT COALESCE(MAX(version), 'No schema_versions table (version 0)') AS version FROM sqlite_master LEFT JOIN schema_versions WHERE sqlite_master.name = 'schema_versions';"
                
                Write-Host ""
                Write-Host "Schema version history:" -ForegroundColor Cyan 
                & sqlite3 $DbPath "SELECT version, applied_at, description FROM schema_versions ORDER BY version;" 2>$null
                
                Write-Host ""
                Write-Host "Tables in database:" -ForegroundColor Cyan
                & sqlite3 $DbPath "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name;"
                
                Write-Host ""
                Write-Host "Products table columns:" -ForegroundColor Cyan
                & sqlite3 $DbPath "PRAGMA table_info(Products);" | ForEach-Object { 
                    if ($_ -match "DiscountPercent|DiscountValid|DiscountReason") {
                        Write-Host $_ -ForegroundColor Green
                    } else {
                        Write-Host $_
                    }
                }
            } else {
                Write-Host "⚠️  SQLite3 not found. Install it to check version automatically." -ForegroundColor Yellow
                Write-Host ""
                Write-Host "Manual check:" -ForegroundColor Cyan
                Write-Host "1. Open database with DB Browser for SQLite" -ForegroundColor White
                Write-Host "2. Run: SELECT MAX(version) FROM schema_versions;" -ForegroundColor White
            }
        } catch {
            Write-Host "❌ Error checking database: $_" -ForegroundColor Red
        }
    } else {
        Write-Host "❌ Database file does not exist" -ForegroundColor Red
    }
    Write-Host ""
}

function Show-Menu {
    while ($true) {
        Write-Host "================================" -ForegroundColor Cyan
        Write-Host "   SELECT TEST SCENARIO:" -ForegroundColor Cyan
        Write-Host "================================" -ForegroundColor Cyan
        Write-Host "1. Test NEW database (delete existing)" -ForegroundColor White
        Write-Host "2. Test UPGRADE (simulate old database)" -ForegroundColor White  
        Write-Host "3. Restore original database" -ForegroundColor White
        Write-Host "4. Check current schema version" -ForegroundColor White
        Write-Host "5. Exit" -ForegroundColor White
        Write-Host ""
        
        $choice = Read-Host "Enter your choice (1-5)"
        
        switch ($choice) {
            "1" { Test-NewDatabase; Read-Host "Press Enter to continue" }
            "2" { Test-UpgradeDatabase; Read-Host "Press Enter to continue" }  
            "3" { Restore-Database; Read-Host "Press Enter to continue" }
            "4" { Check-SchemaVersion; Read-Host "Press Enter to continue" }
            "5" { return }
            default { Write-Host "Invalid choice. Please enter 1-5." -ForegroundColor Red }
        }
        Write-Host ""
    }
}

# Main execution
switch ($TestType.ToLower()) {
    "new" { Test-NewDatabase }
    "upgrade" { Test-UpgradeDatabase }
    "restore" { Restore-Database } 
    "version" { Check-SchemaVersion }
    "menu" { Show-Menu }
}

Write-Host "Done!" -ForegroundColor Green
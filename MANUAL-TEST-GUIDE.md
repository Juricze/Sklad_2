# 🧪 Manual Test Guide - Database Migrations

## Quick Tests (5 minut)

### ✅ **TEST 1: New Database**
1. **Ukončit aplikaci** pokud běží
2. **Smazat DB**: Jdi do `%LOCALAPPDATA%\Sklad_2_Data\` a smaž `sklad.db`
3. **Spustit aplikaci** 
4. **Zkontrolovat Debug output** (Visual Studio Output window):
   ```
   DatabaseMigrationService: Current schema version: 0
   DatabaseMigrationService: Migrating to version 1
   DatabaseMigrationService: Migrating to version 2
   DatabaseMigrationService: Database is up to date (version 2)
   ```
5. **Otestovat discount funkce**: 
   - Databáze → Nový produkt → Zaškrtnout "Zlevněný produkt" → Mělo by fungovat
   - Prodej → Vybrat položku → Tlačítko "Sleva" → Mělo by fungovat

**Expected: ✅ Všechno funguje okamžitě**

---

### ✅ **TEST 2: Database Upgrade (simulace starého zákazníka)**

#### Příprava "staré" databáze:
1. **Backup současné DB**:
   - Kopíruj `%LOCALAPPDATA%\Sklad_2_Data\sklad.db` → `sklad_backup.db`

2. **Vytvoř starou DB** (bez discount fields):
   ```bash
   # Otevři PowerShell v složce projektu
   .\Test-Migrations.ps1 upgrade
   ```
   NEBO ručně:
   - Smaž `sklad.db`
   - Vytvoř novou DB s tímto SQL (pomocí SQLite Browser):
   ```sql
   CREATE TABLE Products (
       Ean TEXT PRIMARY KEY,
       Name TEXT NOT NULL,
       Category TEXT NOT NULL, 
       SalePrice REAL NOT NULL,
       PurchasePrice REAL NOT NULL,
       StockQuantity INTEGER NOT NULL,
       VatRate REAL NOT NULL
   );
   
   INSERT INTO Products VALUES 
   ('1234567890123', 'Test Produkt', 'Test', 100.0, 80.0, 5, 21.0);
   ```

#### Test upgrade:
3. **Spustit aplikaci**
4. **Zkontrolovat Debug output**:
   ```
   DatabaseMigrationService: Current schema version: 0
   DatabaseMigrationService: Migrating to version 1
   DatabaseMigrationService: Migrating to version 2
   DatabaseMigrationService: Executed: ALTER TABLE Products ADD COLUMN DiscountPercent REAL NULL
   DatabaseMigrationService: Successfully migrated to version 2
   ```

5. **Ověřit zachování dat**:
   - Databáze → měl by tam být "Test Produkt"
   - Discount funkce by měly fungovat

6. **Restore original DB**:
   ```bash
   .\Test-Migrations.ps1 restore
   ```

**Expected: ✅ Upgrade proběhl úspěšně, data zachována**

---

### ✅ **TEST 3: Schema Version Check**
```bash
# PowerShell
.\Test-Migrations.ps1 version
```
**Expected: Schema version = 2**

---

## Advanced Tests (10 minut)

### 🔍 **Test Schema Version Tracking**
1. **Check database**:
   - Otevři `sklad.db` v [DB Browser for SQLite](https://sqlitebrowser.org/)
   - Najdi tabulku `schema_versions`
   - Měla by obsahovat:
     ```
     version | applied_at          | description
     1       | 2024-11-24 20:xx:xx | Initial schema with all tables
     2       | 2024-11-24 20:xx:xx | Add discount fields to Products and ReceiptItems tables
     ```

### 🔍 **Test Added Columns**
1. **Check Products table**:
   - Měla by mít nové sloupce: `DiscountPercent`, `DiscountValidFrom`, `DiscountValidTo`, `DiscountReason`

2. **Check ReceiptItems table**:
   - Měla by mít nové sloupce: `DiscountPercent`, `OriginalUnitPrice`, `DiscountReason`

### 🔍 **Test Error Handling**
1. **Simuluj chybu**: Ručně poškozím migraci v kódu
2. **Spustit aplikaci**
3. **Expected**: Error dialog + aplikace se ukončí

---

## PowerShell Helper Commands

```powershell
# Quick test - new database
.\Test-Migrations.ps1 new

# Quick test - upgrade simulation  
.\Test-Migrations.ps1 upgrade

# Check current version
.\Test-Migrations.ps1 version

# Restore backup
.\Test-Migrations.ps1 restore

# Interactive menu
.\Test-Migrations.ps1
```

---

## Debug Output Examples

### ✅ **Successful Migration (New DB)**:
```
DatabaseMigrationService: Creating database...
DatabaseMigrationService: Current schema version: 0
DatabaseMigrationService: Migrating to version 1
DatabaseMigrationService: Applying V1 - Initial Schema (no-op for existing databases)
DatabaseMigrationService: Successfully migrated to version 1
DatabaseMigrationService: Migrating to version 2  
DatabaseMigrationService: Applying V2 - Add Discount Fields
DatabaseMigrationService: Executed: ALTER TABLE Products ADD COLUMN DiscountPercent REAL NULL
DatabaseMigrationService: Successfully migrated to version 2
DatabaseMigrationService: Database is up to date (version 2)
```

### ✅ **Successful Migration (Upgrade)**:
```
DatabaseMigrationService: Current schema version: 0
DatabaseMigrationService: Migrating to version 1
DatabaseMigrationService: Tables already exist, V1 migration is a no-op
DatabaseMigrationService: Successfully migrated to version 1
DatabaseMigrationService: Migrating to version 2
DatabaseMigrationService: Applying V2 - Add Discount Fields  
DatabaseMigrationService: Executed: ALTER TABLE Products ADD COLUMN DiscountPercent REAL NULL
DatabaseMigrationService: Column already exists, skipping: ALTER TABLE Products ADD COLUMN DiscountReason TEXT NULL DEFAULT ''
DatabaseMigrationService: Successfully migrated to version 2
```

### ❌ **Error Example**:
```
DatabaseMigrationService: Migration to version 2 FAILED
DatabaseMigrationService: Migration failed: [error message]
```

---

## 🎯 Success Criteria

| Test | Expected Result | Status |
|------|----------------|--------|
| New DB | Schema v2, discount features work | ⬜ |
| Upgrade | Schema 0→2, data preserved | ⬜ |
| Error handling | Dialog + app exit | ⬜ |
| Schema tracking | `schema_versions` table exists | ⬜ |
| Columns added | Discount fields in tables | ⬜ |

**All tests passing = Production ready! 🚀**
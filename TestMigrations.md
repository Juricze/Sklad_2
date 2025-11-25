# Test Migračního Systému

## 🧪 Testovací Scénáře

### 1. Test nové databáze
1. **Smaž databázi**: `C:\Users\{Username}\AppData\Local\Sklad_2_Data\sklad.db`
2. **Spusť aplikaci** - migration service by měl vytvořit novou DB s nejnovějším schema
3. **Ověř**: Schema version = 2 v tabulce `schema_versions`

### 2. Test starší databáze (simulace upgrade)
1. **Vytvoř "starou" databázi** bez discount fields
2. **Spusť aplikaci** - měla by se automaticky upgradovat
3. **Ověř**: Discount fields byly přidány

### 3. Test rollback ochrany
1. **Zkus spustit starší verzi** aplikace na novější DB
2. **Očekává se**: Aplikace by měla detekovat nekompatibilitu

## 🔧 Implementované funkce

### ✅ DatabaseMigrationService:
- **Migration tracking** - tabulka `schema_versions`
- **Step-by-step migrations** - aplikuje migrace postupně
- **Error handling** - robustní error handling s logováním  
- **Rollback protection** - detekce nekompatibility

### ✅ Migration V1:
- **Initial schema** - no-op pro existing databases
- **Compatibility** - funguje s `Database.EnsureCreated()` výsledky

### ✅ Migration V2:
- **Discount fields** do `Products` table:
  - `DiscountPercent` (REAL NULL)
  - `DiscountValidFrom` (TEXT NULL) 
  - `DiscountValidTo` (TEXT NULL)
  - `DiscountReason` (TEXT DEFAULT '')
- **Discount fields** do `ReceiptItems` table:
  - `DiscountPercent` (REAL NULL)
  - `OriginalUnitPrice` (REAL DEFAULT 0)
  - `DiscountReason` (TEXT DEFAULT '')

### ✅ App Integration:
- **Startup migration** - spuštěno před UI
- **Error handling** - zobrazí dialog a ukončí při chybě
- **DI registration** - správně zaregistrováno v container

## 📊 Schema Version History

| Version | Description | Changes |
|---------|-------------|---------|
| 0 | Pre-migration era | Original `EnsureCreated()` schema |
| 1 | Initial tracking | Add `schema_versions` table |  
| 2 | Discount system | Add discount fields to Products and ReceiptItems |

## 🚀 Pro Produkci:

### **Výhody nového systému:**
- ✅ **Žádné mazání dat** - zachová existující data
- ✅ **Postupné upgrady** - bezpečné step-by-step migrace
- ✅ **Tracking** - vždy víš, jaká verze schema je aktuální
- ✅ **Rollback protection** - detekce nekompatibility
- ✅ **Error handling** - robustní při chybách

### **Další migrace v budoucnu:**
1. **Přidej SQL** do `ApplyMigrationAsync()`
2. **Zvyš** `CURRENT_SCHEMA_VERSION`  
3. **Otestuj** na testovací DB
4. **Nasaď** - automaticky se aplikuje při startu

Systém je připraven pro produkci! 🎯
# 🧪 Test Reálné Migrace - Přidání nového sloupce

## Simulace: Přidání nového sloupce "ProductBarcode"

### Scénář:
Za 3 měsíce rozhodneš přidat support pro dodatečný barcode na produkty. Potřebuješ přidat sloupec `ProductBarcode` do tabulky `Products`.

---

## KROK 1: Přidat test migraci V3

Přidej do `DatabaseMigrationService.cs`:

```csharp
// Změň current version z 2 na 3
private const int CURRENT_SCHEMA_VERSION = 3; // Version 3: Added ProductBarcode

// Přidej case 3 do ApplyMigrationAsync:
case 3:
    return await ApplyMigration_V3_AddProductBarcode(context);

// Přidej novou migration method:
private async Task<bool> ApplyMigration_V3_AddProductBarcode(DatabaseContext context)
{
    Debug.WriteLine("DatabaseMigrationService: Applying V3 - Add ProductBarcode Field");
    
    var connection = context.Database.GetDbConnection();
    await connection.OpenAsync();
    
    var migrations = new List<string>
    {
        "ALTER TABLE Products ADD COLUMN ProductBarcode TEXT NULL"
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

// Přidej description:
private string GetMigrationDescription(int version)
{
    return version switch
    {
        1 => "Initial schema with all tables",
        2 => "Add discount fields to Products and ReceiptItems tables",
        3 => "Add ProductBarcode field to Products table", // <-- NOVÝ
        _ => $"Unknown migration version {version}"
    };
}
```

---

## KROK 2: Přidat property do Product model

V `Models/Product.cs` přidej:

```csharp
[ObservableProperty]
private string productBarcode = string.Empty;
```

---

## KROK 3: Test migrace

1. **Zkompiluj aplikaci**
2. **Spusť aplikaci**  
3. **Sleduj Debug output**:

Expected:
```
DatabaseMigrationService: Current schema version: 2
DatabaseMigrationService: Migrating to version 3
DatabaseMigrationService: Applying V3 - Add ProductBarcode Field
DatabaseMigrationService: Executed: ALTER TABLE Products ADD COLUMN ProductBarcode TEXT NULL
DatabaseMigrationService: Successfully migrated to version 3
DatabaseMigrationService: Database is up to date (version 3)
```

4. **Zkontroluj databázi**:
   - Otevři `sklad.db` v DB Browser for SQLite
   - Tabulka `Products` by měla mít nový sloupec `ProductBarcode`
   - Všechna existující data by měla být zachována
   - `schema_versions` by měla mít nový řádek s version 3

5. **Ověř funkčnost**:
   - Všechny původní funkce fungují
   - Data jsou zachována
   - Můžeš přidat UI pro nové pole později
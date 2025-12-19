# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Projekt: Sklad_2

WinUI 3 aplikace pro správu skladu a prodeje, postavená na .NET 8 s architekturou MVVM.

## Technologie
- **UI Framework**: WinUI 3 (Windows App SDK 1.5.240428000)
- **Runtime**: .NET 8.0 (target: net8.0-windows10.0.19041.0, min: 10.0.17763.0)
- **Database**: SQLite s Entity Framework Core 8.0.4
- **MVVM**: CommunityToolkit.Mvvm 8.2.2
- **DI**: Microsoft.Extensions.DependencyInjection 8.0.0
- **Build Tools**: Windows SDK Build Tools 10.0.22621.3233
- **Platformy**: x86, x64, ARM64

**Poznámka**: Aplikace je určena výhradně pro Windows 10 build 19041+ (verze 2004 a novější). Projekt má unit testy pro kritické finanční výpočty (Receipt/Return models).

## Build a spuštění
```bash
# Build projektu
dotnet build Sklad_2.sln

# Spuštění (nebo F5 ve Visual Studio 2022)
dotnet run --project Sklad_2.csproj
```

**Požadavky**: Visual Studio 2022 s workloads ".NET desktop development" a "Windows App SDK"

## Architektura aplikace

### MVVM Pattern
Projekt striktně dodržuje MVVM pattern:
- **Models** (`Models/`): Datové modely (Product, Receipt, Return, CashRegisterEntry, VatConfig)
- **Views** (`Views/`): XAML stránky a dialogy (`Views/Dialogs/`)
- **ViewModels** (`ViewModels/`): Prezentační logika s CommunityToolkit.Mvvm

### Dependency Injection
Vše je registrováno v `App.xaml.cs` metodě `ConfigureServices()`:
- **Singleton ViewModels**: Většina ViewModelů je singleton (sdílený stav během session)
- **Transient ViewModels**: `LoginViewModel` (pro dialogy a přihlášení)
- **Services**: `IDataService`, `IReceiptService`, `IPrintService`, `IAuthService`, `ISettingsService`, `IDailyCloseService`
- **DbContext**: Registrován jako `DbContextFactory<DatabaseContext>` kvůli workaround pro WinUI binding issues

### Databáze
- **Umístění**: `C:\Users\{Username}\AppData\Local\Sklad_2_Data\sklad.db` (LocalApplicationData)
- **Schema**: Definován v `Data/DatabaseContext.cs`
- **Přístup**: Výhradně přes `SqliteDataService` (implementuje `IDataService`)
- **Migrační strategie**: **ŽÁDNÉ MIGRACE** - při změně schématu se databáze maže a vytváří znovu (`Database.EnsureCreated()`)
- **Nastavení**: `AppSettings.json` uložen také v LocalApplicationData

### Messaging System
Projekt používá `CommunityToolkit.Mvvm.Messaging` (WeakReferenceMessenger) pro komunikaci mezi ViewModels:
- `CashRegisterUpdatedMessage`: Aktualizace stavu pokladny
- `RoleChangedMessage`: Změna role uživatele
- `ShowDepositConfirmationMessage`: Potvrzení vkladu do pokladny
- `VatConfigsChangedMessage`: Změna konfigurace DPH

### Design System
- **Theme**: Mica backdropu (světlý motiv, `ApplicationTheme.Light`)
- **Barvy**: Černobílá paleta
- **Styly**: Centralizovány v `Styles/Controls.xaml`
- **Konvertory**: V `Converters/` (CurrencyConverter, DecimalConverter, BooleanToVisibilityConverter, atd.)

### Navigace
`MainWindow.xaml.cs` obsahuje hlavní `NavigationView` s metodou `NavView_ItemInvoked()`, která řídí navigaci mezi stránkami. Stránky jsou vytvářeny jako nové instance při každém přepnutí.

### Status Bar (Informační panel)
Umístěn v `NavigationView.PaneFooter` (nad tlačítkem Odhlásit), zobrazuje stručný přehled stavu systému:

**Levý sloupec (Nastavení):**
- 🏢 **Firma**: Vyplněno/Nevyplněno (kontroluje `ShopName` a `ShopAddress`)
- ⚙️ **DPH kat**: Nastaveno/Nenastaveno (existence `VatConfig` záznamů)
- 🧾 **DPH**: Plátce/Neplátce (podle `IsVatPayer`)
- 💾 **Databáze**: OK/Chyba (test spojení s databází)

**Pravý sloupec (Hardware & Denní):**
- 🖨️ **Tiskárna**: Připojena/Odpojena (kontroluje `PrinterPath`)
- 📱 **Scanner**: Připojen/Odpojen (placeholder - zatím vždy "Odpojen")
- 💰 **Uzavírka**: Provedena/Neprovedena (kontrola `LastDayCloseDate`)

**Barevné indikátory:**
- Zelená (#34C759): OK stav
- Červená (#FF3B30): Chyba/kritický problém
- Oranžová (#FF9500): Upozornění
- Modrá (#007AFF): Informace (DPH)
- Šedá (#999999): Neutrální/neaktivní

**Auto-refresh**: Status bar se automaticky aktualizuje při startu aplikace a po každé navigaci mezi stránkami (`StatusBarViewModel.RefreshStatusAsync()`).

### Autentizace a role
- **Login flow**: `LoginWindow` → `MainWindow` (po úspěšném přihlášení)
- **Role**: "Prodej" (omezená práva) a "Vlastník" (plná práva)
- **Service**: `AuthService` implementuje `IAuthService`, poskytuje `CurrentRole`
- **UI omezení**: Pro roli "Prodej" je skrytá položka "Přehled prodejů" v menu
- **Denní workflow (role "Prodej")**:
  - První přihlášení nebo nový den → Dialog "Nový den" s počátečním stavem pokladny
  - Během dne → Prodeje, vklady, kontroly
  - Konec dne → Uzavírka dne (lze pouze 1× denně)
  - Ochrana: Detekce změny systémového času (varování při posunu zpět)

### Key Pages
- **ProdejPage**: Prodej produktů, správa košíku, platby (hotovost/karta)
- **DatabazePage**: Seznam produktů s možností editace (ListView), filtrování podle kategorie, řazení podle sloupců
- **NovyProduktPage**: Přidání nového produktu
- **UctenkyPage**: Historie účtenek s filtry (denní/týdenní/měsíční/vlastní)
- **VratkyPage**: Zpracování vratek a dobropisů
- **VratkyPrehledPage**: Přehled vratek s filtry
- **CashRegisterPage**: Správa pokladny (vklady, denní kontrola, uzavírka dne)
- **CashRegisterHistoryPage**: Historie transakcí pokladny s filtry
- **PrehledProdejuPage**: Dashboard prodejů s KPI kartami, top/worst produkty, platební metody, filtry (Celkem/Dnešní/Týdenní/Měsíční/Vlastní)
- **NastaveniPage**: Nastavení aplikace s NavigationView menu (DPH, kategorie, firma)

### DPH (VAT) System
- **Konfigurace**: `VatConfig` tabulka - mapování kategorií produktů na sazby DPH
- **UI**: Nastavení v "Nastavení → Sazby DPH"
- **Auto-fill**: Při vytváření produktu se automaticky předvyplní sazba DPH podle kategorie
- **Účtenky**: Detailní souhrn DPH seskupený podle sazeb
- **Plátce DPH**: Přepínač v nastavení plně implementován

### Kategorie produktů
Centralizovány ve statické třídě `Models/ProductCategories.cs`. Seznam kategorií je hard-coded (zatím není dynamická správa přes UI).

### Pokladna (Cash Register) - Kompletní workflow

#### Entry Types
- **DayStart**: Zahájení dne - nastaví počáteční stav (nepřičítá!)
- **Sale**: Prodej - přičte částku
- **Deposit**: Vklad - přičte částku
- **Withdrawal**: Výběr - odečte částku
- **Return**: Vratka - odečte částku
- **DailyReconciliation**: Denní kontrola - odečte rozdíl
- **DayClose**: Uzavírka dne - nastaví konečný stav

#### Denní workflow (role "Prodej")
1. **Přihlášení**: LoginWindow → MainWindow.OnFirstActivated
2. **Kontrola nového dne**:
   - Pokud `LastSaleLoginDate` je null nebo < Today → Dialog "Nový den"
   - Pokud `LastSaleLoginDate` > Today → Varování o změně času
   - Dialog validuje částku (0-10M Kč, ne záporná)
3. **Zahájení**: `SetDayStartCashAsync()` vytvoří `DayStart` záznam
4. **Během dne**: Prodeje automaticky aktualizují pokladnu
5. **Uzavírka**:
   - Tlačítko "Uzavřít den" v CashRegisterPage
   - Validace: pouze 1× denně (kontrola `LastDayCloseDate`)
   - Vytvoří `DayClose` záznam s napočítanou částkou
   - Vypočítá rozdíl (přebytek/manko)
6. **Zavření aplikace**:
   - Kontrola, zda byla provedena uzavírka dne
   - Pokud ne → Dialog s upozorněním a možností zrušit zavření
   - Ochrana pouze pro roli "Prodej"

#### Timing a robustnost
- **MainWindow dialog**: Čeká na `XamlRoot` (max 20×50ms)
- **CashRegisterPage success dialog**: 800ms delay + retry s 300ms (WinUI dialog bug)
- **Page.Loaded event**: CashRegisterPage načítá data při každém zobrazení

### Známé problémy a workarounds
1. **TwoWay binding issue**: WinUI má problém s TwoWay bindingem na DbContext entity - řešeno přes DbContextFactory
2. **ContentDialog resource access**: Dialogy ztrácejí přístup ke global resources - všechny konvertory musí být explicitně definovány v App.xaml
3. **ListView initialization**: Data musí být načtena před `InitializeComponent()` v konstruktoru stránky
4. **ContentDialog multiple instances**: WinUI nepovoluje více dialogů najednou - řešeno zpožděním (800ms) + retry s 300ms + try-catch
5. **Clean + Rebuild nutnost**: Při změnách XAML/ViewModels **VŽDY** Build → Clean Solution, pak Rebuild Solution (WinUI/XAML projekty cachují sestavení)
6. **XamlRoot timing**: Dialog v MainWindow vyžaduje čekání na `XamlRoot` - robustní while loop s retry (max 20×50ms) místo pevného delay

## Styl práce (z GEMINI.md)
- **Komunikace**: Pouze česky, jasná, stručná, profesionální
- **Vývoj**: Inkrementální (krok za krokem), po každé změně ověřit funkčnost
- **Chyby**: Vždy vyžadovat přesné chybové hlášky z Visual Studio před opravou
- **Design**: Striktně dodržovat Mica design s černobílou paletou

---

## 🔄 DRY Princip (Don't Repeat Yourself)

**KRITICKÉ: Nikdy neduplikovat výpočty, formátování nebo business logiku!**

### Pravidla pro celou aplikaci:

1. **Model jako jediný zdroj pravdy** - computed properties patří do Models, ne do ViewModels
2. **ViewModely pouze delegují** - `ViewModel.Property => Model?.Property ?? default`
3. **Jeden výpočet = jedno místo** - pokud se něco počítá, počítá se jen v jednom souboru
4. **Při změně logiky = jedna úprava** - nemusíš hledat duplikáty po celém projektu

### Příklad - Receipt model:

```csharp
// ❌ ŠPATNĚ - duplikace výpočtu v ViewModel nebo code-behind
public decimal AmountToPay => SelectedReceipt.TotalAmount
    - SelectedReceipt.GiftCardRedemptionAmount;

// ✅ SPRÁVNĚ - delegace na Receipt model (jediný zdroj pravdy)
public decimal AmountToPay => SelectedReceipt?.AmountToPay ?? 0;
```

### Jak aplikovat DRY:

1. **Výpočty částek** → Model (Receipt, Product, CashRegisterEntry...)
2. **Formátování** → Model (`*Formatted` properties)
3. **Validace** → Model nebo centrální ValidationHelper
4. **Business pravidla** → Services nebo Model

**Claude POVINNOST**: Před přidáním nové computed property zkontroluj, zda už neexistuje v Modelu. Pokud ne, přidej ji tam - ne do ViewModelu!

---

## 🔴 KRITICKÉ: Windows 10 Compatibility Requirements

**⚠️ PRODUKČNÍ PC BĚŽÍ NA WINDOWS 10!**

Vývoj probíhá na Win11, ale **PRODUKCE JE WIN10**. Všechen kód MUSÍ být Win10 kompatibilní!

### **Povinná pravidla pro KAŽDÝ nový kód:**

#### **1. File I/O - VŽDY přidat flush**
```csharp
// ❌ ŠPATNĚ (nefunguje spolehlivě na Win10)
await File.WriteAllTextAsync(path, content);

// ✅ SPRÁVNĚ (Win10 + Win11 safe)
await File.WriteAllTextAsync(path, content);
using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
{
    fs.Flush(true); // Force OS buffer flush
}
```

#### **2. Settings/Config save - VŽDY přidat delay před messaging**
```csharp
// ❌ ŠPATNĚ
await _settingsService.SaveSettingsAsync();
_messenger.Send(new SettingsChangedMessage()); // Win10: soubor ještě není na disku!

// ✅ SPRÁVNĚ
await _settingsService.SaveSettingsAsync();
await Task.Delay(100); // Win10 file system flush
_messenger.Send(new SettingsChangedMessage());
await Task.Delay(200); // Win10 UI refresh
```

#### **3. EF Core queries - VŽDY použít AsNoTracking() pro read-only**
```csharp
// ❌ ŠPATNĚ (entity tracking conflict na Win10)
return await context.Products.FirstOrDefaultAsync(p => p.Ean == ean);

// ✅ SPRÁVNĚ (Win10 + Win11 safe + rychlejší)
return await context.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Ean == ean);
```

#### **4. Database write - VŽDY přidat retry logiku pro SQLite**
```csharp
// ✅ SPRÁVNĚ (Win10 má přísnější file locking)
int maxRetries = 3;
int delayMs = 100;
for (int attempt = 0; attempt < maxRetries; attempt++)
{
    try {
        await SaveToDatabase();
        break;
    }
    catch (DbUpdateException) when (attempt < maxRetries - 1)
    {
        await Task.Delay(delayMs);
        delayMs *= 2; // Exponential backoff
    }
}
```

#### **5. Window handles - VŽDY nastavit CurrentWindow**
```csharp
// ❌ ŠPATNĚ (FolderPicker nefunguje na Win10)
var mainWindow = new MainWindow();
mainWindow.Activate();

// ✅ SPRÁVNĚ
var mainWindow = new MainWindow();
var app = Application.Current as App;
app.CurrentWindow = mainWindow; // KRITICKÉ pro Win10!
mainWindow.Activate();
```

#### **6. ObservableCollection refresh - VŽDY poslouchat messaging**
```csharp
// ❌ ŠPATNĚ (staticka inicializace - Win10 nerefreshuje)
public ObservableCollection<string> Items { get; } =
    new ObservableCollection<string>(StaticSource.All);

// ✅ SPRÁVNĚ
public ObservableCollection<string> Items { get; } = new();

// V konstruktoru:
_messenger.Register<DataChangedMessage>(this, async (r, m) =>
{
    await Task.Delay(100); // Win10 file flush
    RefreshItems();
});

private void RefreshItems()
{
    var currentSelection = SelectedItem;
    Items.Clear();
    foreach (var item in StaticSource.All)
        Items.Add(item);
    SelectedItem = Items.Contains(currentSelection) ? currentSelection : Items.FirstOrDefault();
}
```

### **Checklist před každým commitem:**

- [ ] Přidány file flush kde se zapisuje na disk?
- [ ] Přidány delays (100ms file, 200ms UI) po Save + Message?
- [ ] Použit `.AsNoTracking()` pro read-only EF queries?
- [ ] Přidána retry logika pro database write?
- [ ] Nastaven `app.CurrentWindow` při vytváření oken?
- [ ] ObservableCollection má refresh handler?

### **Známé Win10 vs Win11 rozdíly:**

| Oblast | Win10 | Win11 | Řešení |
|--------|-------|-------|--------|
| **File cache** | Pomalý flush | Rychlý flush | `Flush(true)` + delay |
| **SQLite lock** | Přísnější | Uvolněnější | Retry logika |
| **Dispatcher** | Nižší priorita | Vyšší priorita | Delays pro UI |
| **Window handles** | Starší COM model | Nový WinRT | Explicitní `CurrentWindow` |
| **Memory GC** | Konzervativní | Agresivní | `AsNoTracking()` |

### **Testování:**

**VŽDY otestovat na Win10 tyto funkce před release:**
1. ✅ FolderPicker (Nastavení → Systém → Procházet)
2. ✅ Uložení firemních údajů (+ StatusBar refresh)
3. ✅ Prodej produktu (database write)
4. ✅ Správa kategorií (refresh v Nový produkt)
5. ✅ Backup při zavření aplikace

**Win11 development je OK**, ale **NIKDY necommitovat bez mentální kontroly Win10 compatibility!**

---

## ⚠️ KRITICKÉ: Database Schema Version Protocol

**🚨 APLIKACE JE V PRODUKCI - NIKDY NEMAZAT DATABÁZI! 🚨**

**ABSOLUTNÍ ZÁKAZ:**
- ❌ **NIKDY** nespouštět `Remove-Item sklad.db`
- ❌ **NIKDY** nespouštět `Database.EnsureDeleted()`
- ❌ **NIKDY** nenavrhovat smazání databáze při schema změnách
- ✅ **VŽDY** používat migrační systém (`DatabaseMigrationService.cs`)

**VŽDY při změnách databáze:**

1. **Claude NIKDY NESMAŽE DATABÁZI - pouze vytvoří migraci!**
2. **Claude AUTOMATICKY NEUPRAVUJE schema version!**
3. **Claude MUSÍ AKTIVNĚ UPOZORNIT** uživatele po každé DB změně s textem:
   ```
   ⚠️ DATABÁZOVÁ ZMĚNA DETEKOVÁNA!
   Přidal jsem [popis změny]. Potřebuješ aktualizovat CURRENT_SCHEMA_VERSION
   a přidat migraci pro produkční nasazení!
   ```
4. **Bezpečnostní síť**: Pre-build script `Scripts/CheckDatabaseChanges.ps1` detekuje nové `ObservableProperty` bez migrace
5. **Změny vyžadující schema version update**:
   - Přidání/odebrání sloupce v modelu (`ObservableProperty`)
   - Změna typu sloupce
   - Přidání nové entity/tabulky
   - Změna primary key nebo indexů
6. **Schema version update proces**:
   - Zvýš `CURRENT_SCHEMA_VERSION` v `DatabaseMigrationService.cs`
   - Přidej novou `ApplyMigration_VX_Description` metodu
   - Přidej case do `ApplyMigrationAsync`
   - Aktualizuj `GetMigrationDescription`

**Terminologie**: "Migrace" = schema version update + SQL commands pro změnu struktury

**Automatická detekce**: Build selže s chybou pokud najde nové DB properties bez schema version update!

**Claude POVINNOST**:
- ✅ Vždy upozorni na potřebu schema version update po DB změnách
- ❌ NIKDY nemazat databázi - ani na vývojovém PC!

---

## 🚀 KRITICKÉ: Release Checklist

**VŽDY při vytváření nového release:**

1. **NEJDŘÍV aktualizovat verzi v `Sklad_2.csproj`:**
   ```xml
   <Version>X.Y.Z</Version>
   <AssemblyVersion>X.Y.Z.0</AssemblyVersion>
   <FileVersion>X.Y.Z.0</FileVersion>
   ```

2. **KRITICKÉ: Smazat build cache (WinUI/XAML cachuje assembly verzi!):**
   ```bash
   rm -rf bin obj
   ```
   **⚠️ BEZ TOHOTO KROKU SE VERZE NEPROPAGUJE DO EXE!**

3. **Build release:**
   ```bash
   dotnet publish Sklad_2.csproj -c Release -r win-x64 --self-contained true -p:Platform=x64
   ```

4. **Verifikovat assembly verzi:**
   ```bash
   powershell -Command "(Get-Item 'bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\publish\Sklad_2.exe').VersionInfo.FileVersion"
   ```
   **MUSÍ odpovídat X.Y.Z.0!** Pokud ne, opakuj krok 2-4.

5. **Vytvořit ZIP:**
   ```bash
   powershell.exe -ExecutionPolicy Bypass -Command "Compress-Archive -Path 'bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\publish\*' -DestinationPath 'Sklad_2-vX.Y.Z-win-x64.zip' -Force"
   ```

6. **Commit + Push:**
   ```bash
   git add -A && git commit -m "Release vX.Y.Z: [popis]" && git push
   ```

7. **GitHub Release:**
   ```bash
   gh release create vX.Y.Z --title "vX.Y.Z - [název]" --notes "[popis]" Sklad_2-vX.Y.Z-win-x64.zip
   ```

**Claude POVINNOST**: Vždy aktualizovat verzi v `.csproj` PŘED buildem!

---

## 🔄 Standalone Updater

**Pro situace, kdy aplikaci nelze spustit** (chybějící .NET runtime, corrupted files, atd.)

### Použití:

**NEJJEDNODUŠŠÍ (doporučeno):**
1. **Stáhni `StandaloneUpdater.bat`** z repository
2. **Dvojklik** na soubor
3. Hotovo - obchází Execution Policy automaticky

**Alternativa (PowerShell přímo):**
1. **Stáhni `StandaloneUpdater.ps1`** z repository
2. **Pravý klik** na soubor → "Spustit pomocí PowerShell"
3. Pokud selže (Execution Policy), spusť CMD a zadej:
   ```cmd
   powershell -ExecutionPolicy Bypass -File "cesta\k\StandaloneUpdater.ps1"
   ```

**Po spuštění:**
- Zadej cestu k instalaci Sklad_2 (nebo Enter pro Desktop\Sklad_2)
- Script automaticky:
  - Stáhne nejnovější release z GitHub
  - Vytvoří zálohu (volitelně)
  - Zkopíruje nové soubory (kromě user data)
  - Nabídne spuštění aplikace

### Funkce:
- ✅ **Nezávislý na aplikaci** - nevyžaduje funkční Sklad_2.exe
- ✅ **Automatická detekce verze** - vždy stáhne latest release
- ✅ **Ochrana user data** - nepřepíše databázi, nastavení, obrázky
- ✅ **Záloha** - volitelné vytvoření backup složky
- ✅ **Progress reporting** - barevný výstup s progress barem
- ✅ **Interaktivní** - potvrzení před každým krokem

### Kdy použít:
- ❌ Aplikace nejde spustit (chybí .NET 8 Runtime)
- ❌ Corrupted files po neúspěšné aktualizaci
- 🔄 Chceš aktualizovat bez spouštění aplikace
- 🔄 Potřebuješ aktualizovat více instalací najednou

### Distribution:
- Zahrnut v každém release ZIP
- Dostupný samostatně v repository root
- Ke stažení z GitHub web interface

---

## 🧪 Unit Testy & Testing Workflow

**Projekt má unit testy pro kritické výpočty** (od prosince 2025).

### **Co testujeme:**

✅ **Receipt Model** (`Sklad_2.Tests/Models/ReceiptTests.cs` - 19 testů)
- Zaokrouhlování na celé koruny (FinalAmountRounded, RoundingAmount, HasRounding)
- Výpočet AmountToPay (věrnostní sleva + dárkové poukazy)
- Kombinace slev + zaokrouhlování (KRITICKÉ pro denní uzávěrku)
- Edge cases (nulové/velmi malé/velké částky)

✅ **Return Model** (`Sklad_2.Tests/Models/ReturnTests.cs` - 15 testů)
- Zaokrouhlování vratek (FinalRefundRounded, RefundRoundingAmount)
- Věrnostní slevy při vratce (poměrná část)
- DRY konzistence s Receipt modelem

### **Kdy spustit testy:**

**VŽDY před:**
- ✅ Commitnutím změn v Models (Receipt, Return, CashRegisterEntry)
- ✅ Změnami ve výpočtech (zaokrouhlování, DPH, slevy)
- ✅ Vytvořením nového release

**Volitelně:**
- Po změnách v Services (DailyCloseService, SqliteDataService)

### **Jak spustit:**

**Visual Studio 2022 (DOPORUČENO):**
1. Otevři `Sklad_2.sln`
2. Test → Test Explorer (nebo Ctrl+E, T)
3. Run All Tests (Ctrl+R, A)
4. Všechny testy by měly projít ✅

**Poznámka**: .NET CLI (`dotnet test`) může mít problémy s WinUI projekty na SDK 9. Používej Visual Studio.

### **Workflow pro nové features:**

Při implementaci nové funkce s finanční/business logikou:

1. **Implementuj rychle** (jako dosud) - Model, ViewModel, View
2. **Otestuj manuálně v UI** - vytvoř testovací prodej, ověř v DB
3. **Před commitem: Přidej unit test PRO BUSINESS LOGIKU**:
   ```csharp
   // Sklad_2.Tests/Models/MyNewFeatureTests.cs
   [Fact]
   public void MyCalculation_Scenario_ExpectedResult()
   {
       // Arrange
       var model = new MyModel { Property = value };

       // Act
       var result = model.ComputedProperty;

       // Assert
       Assert.Equal(expected, result);
   }
   ```
4. **Spusť všechny testy** (Visual Studio Test Explorer)
5. **Commit + Release** (pouze pokud všechny testy procházejí ✅)

### **Co NETESTUJEME (není potřeba):**

- ❌ UI code-behind (`.xaml.cs` event handlers)
- ❌ ViewModely s WinUI závislostmi (ContentDialog, XamlRoot...)
- ❌ Navigation logika
- ❌ Dialogy

**Pravidlo**: Testuj pouze **business logiku** (Models, Services), ne UI.

### **xUnit Cheat Sheet:**

```csharp
using Xunit;

// Jeden test
[Fact]
public void TestName() { }

// Parametrizované testy (více vstupů)
[Theory]
[InlineData(100.50, 101)]
[InlineData(100.49, 100)]
public void TestName(decimal input, decimal expected) { }

// Assertions
Assert.Equal(expected, actual);
Assert.True(condition);
Assert.False(condition);
Assert.Throws<TException>(() => code);
```

**Více info**: `Sklad_2.Tests/README.md`

---

## Session Workflow
- **"pokracuj" / "pokračujem" / "pokračujeme"** → Začátek session - načti `SESSION.md` a pokračuj v práci
- **"konec" / "končíme" / "končit"** → Konec session - shrň provedenou práci a zapiš do `SESSION.md`, aktualizuj TODO list

**Poznámka**: TODO list je udržován v `SESSION.md`, ne zde.

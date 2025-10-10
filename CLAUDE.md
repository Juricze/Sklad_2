# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Projekt: Sklad_2

WinUI 3 aplikace pro správu skladu a prodeje, postavená na .NET 8 s architekturou MVVM.

## Technologie
- **UI Framework**: WinUI 3 (Windows App SDK 1.5)
- **Runtime**: .NET 8.0 (target: net8.0-windows10.0.19041.0)
- **Database**: SQLite s Entity Framework Core 8.0.4
- **MVVM**: CommunityToolkit.Mvvm 8.2.2
- **DI**: Microsoft.Extensions.DependencyInjection 8.0.0

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
- **Services**: `IDataService`, `IReceiptService`, `IPrintService`, `ICashRegisterService`, `IAuthService`, `ISettingsService`
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
- **DatabazePage**: Seznam produktů s možností editace (ListView)
- **NovyProduktPage**: Přidání nového produktu
- **UctenkyPage**: Historie účtenek s filtry (denní/týdenní/měsíční/vlastní)
- **VratkyPage**: Zpracování vratek a dobropisů
- **VratkyPrehledPage**: Přehled vratek
- **CashRegisterPage**: Správa pokladny (vklady, denní kontrola, uzavírka dne)
- **CashRegisterHistoryPage**: Historie transakcí pokladny
- **NastaveniPage**: Nastavení aplikace s NavigationView menu (DPH, kategorie, firma)

### DPH (VAT) System
- **Konfigurace**: `VatConfig` tabulka - mapování kategorií produktů na sazby DPH
- **UI**: Nastavení v "Nastavení → Sazby DPH"
- **Auto-fill**: Při vytváření produktu se automaticky předvyplní sazba DPH podle kategorie
- **Účtenky**: Detailní souhrn DPH seskupený podle sazeb
- **Plátce DPH**: Přepínač v nastavení (UI zatím nerespektuje - known issue v TODO)

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

#### Timing a robustnost
- **MainWindow dialog**: Čeká na `XamlRoot` (max 20×50ms)
- **CashRegisterPage success dialog**: 800ms delay + retry s 300ms (WinUI dialog bug)
- **Page.Loaded event**: CashRegisterPage načítá data při každém zobrazení

### Známé problémy a workarounds
1. **TwoWay binding issue**: WinUI má problém s TwoWay bindingem na DbContext entity - řešeno přes DbContextFactory
2. **ContentDialog resource access**: Dialogy ztrácejí přístup ke global resources - všechny konvertory musí být explicitně definovány v App.xaml
3. **ListView initialization**: Data musí být načtena před `InitializeComponent()` v konstruktoru stránky
4. **ContentDialog multiple instances**: WinUI nepovoluje více dialogů najednou - řešeno zpožděním + retry + try-catch
5. **Clean + Rebuild nutnost**: Při změnách XAML/ViewModels vždy Build → Clean Solution, pak Rebuild

## Styl práce (z GEMINI.md)
- **Komunikace**: Pouze česky, jasná, stručná, profesionální
- **Vývoj**: Inkrementální (krok za krokem), po každé změně ověřit funkčnost
- **Chyby**: Vždy vyžadovat přesné chybové hlášky z Visual Studio před opravou
- **Design**: Striktně dodržovat Mica design s černobílou paletou

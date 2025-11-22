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

**Poznámka**: Aplikace je určena výhradně pro Windows 10 build 19041+ (verze 2004 a novější). Projekt nemá unit testy.

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

## Session Workflow
- **"pokracuj" / "pokračujem" / "pokračujeme"** → Začátek session - načti `SESSION.md` a pokračuj v práci
- **"konec" / "končíme" / "končit"** → Konec session - shrň provedenou práci a zapiš do `SESSION.md`, aktualizuj TODO list

---

## 📝 TODO List

### ✅ Hotovo (aktualizováno 18.11.2025)

1. ✅ **Role-based UI restrictions**
   - Skrytý panel "Denní kontrola pokladny" pro roli "Prodej"
   - Tlačítko "Smazat vybrané" disabled pro roli "Prodej"

2. ✅ **Databáze produktů - vylepšení**
   - Filtrování podle kategorie
   - Řazení (klik na hlavičku: Název, Skladem, Cena)
   - Přidán sloupec "Nákupní cena"
   - Fix: EAN vyhledávání - přesný prefix match (StartsWith)

3. ✅ **Status Bar (Informační panel)**
   - Zobrazení stavu: Firma, DPH kategorie, DPH plátce/neplátce, Databáze
   - Zobrazení hardware: Tiskárna, Scanner, Uzavírka dne
   - Barevné indikátory (zelená/červená/oranžová/modrá/šedá)
   - Auto-refresh při startu a navigaci

4. ✅ **Dashboard prodejů (Přehled prodejů)**
   - KPI karty (celkové tržby, průměr na účtenku, DPH, čistá tržba)
   - Quick Stats (Denní průměr vypočítaný podle časového horizontu, Počet účtenek, DPH Info)
   - Top 5 nejprodávanějších produktů
   - Nejméně prodávané produkty (5)
   - Statistiky platebních metod
   - Časové filtry (Celkem/Dnešní/Týdenní/Měsíční/Vlastní)
   - Auto-refresh při otevření stránky
   - Oprava týdenního filtru (Sunday edge case) ve všech ViewModelech

5. ✅ **Denní otevírka/uzavírka pokladny**
   - Zahájení nového dne při prvním přihlášení
   - Ochrana proti změně systémového času
   - Uzavírka dne s kontrolou rozdílu (přebytek/manko)
   - Validace všech částek (0-10M Kč)
   - Kontrola uzavírky při zavírání aplikace (pouze role "Prodej")

6. ✅ **DPH systém**
   - Konfigurace DPH pro kategorie
   - Přepínač Plátce/Neplátce plně implementován
   - Auto-fill sazby DPH podle kategorie produktu

7. ✅ **Historie a přehledy**
   - CashRegisterHistoryPage s filtry
   - UctenkyPage s filtry
   - VratkyPrehledPage s filtry

8. ✅ **Dynamická správa kategorií**
   - CategoriesPanel v NastaveniPage (Nastavení → Kategorie)
   - Funkce: Přidat, přejmenovat, smazat kategorii
   - ProductCategories.cs dynamicky načítá z AppSettings.Categories
   - Automatická aktualizace produktů při přejmenování
   - Ochrana proti smazání používané kategorie

9. ✅ **UI optimalizace pro neplátce DPH** (18.11.2025)
   - Dynamické skrývání DPH prvků podle IsVatPayer
   - Podmíněná validace - neplátce nemusí nastavovat DPH kategorie
   - Skryté komponenty: panel Sazby DPH, pole Sazba DPH, DPH KPI karty, DPH sloupce, Status Bar "DPH kat"
   - Auto-refresh při změně nastavení Plátce/Neplátce
   - Právně správné doklady pro neplátce (bez DIČ, bez "DAŇOVÝ DOKLAD", bez DPH rozkladu)

10. ✅ **Vlastní cesta pro zálohy a exporty** (19.11.2025)
   - Konfigurovatelná cesta v Nastavení → Systém
   - Priorita: Vlastní cesta → OneDrive → Dokumenty (fallback)
   - UI zobrazení aktivní cesty (📁 ikona + modrý text)
   - FolderPicker pro výběr složky
   - Export FÚ používá stejnou cestu jako zálohy
   - Dialog "Záloha dokončena" při zavření aplikace
   - Čisté ukončení s exit code 0 (Environment.Exit)
   - Hybrid backup strategy: aplikace běží offline, záloha při zavření
   - Auto-restore při startu pokud backup je novější

11. ✅ **Systém uživatelských účtů** (22.11.2025)
   - Databázová tabulka Users
   - Skutečné uživatele s přihlášením (nahrazuje fixed roles)
   - Role/oprávnění per uživatel
   - SellerName = skutečné jméno prodavače

### ⏳ Zbývá udělat

1. ⏳ **Export uzavírek do CSV/PDF**
   - Export denních uzavírek pokladny
   - Export přehledů prodejů

2. ⏳ **Implementovat skutečný PrintService**
   - Zatím pouze placeholder (simuluje úspěch)
   - Respektovat "Plátce DPH" přepínač v tisku účtenek
   - Skutečná detekce připojení tiskárny

3. ⏳ **Vylepšit error handling**
   - Lokalizované chybové hlášky (zatím anglické exception messages)
   - User-friendly error dialogy

### 💡 Možná budoucí vylepšení

- Grafy vývoje tržeb v čase (najít stabilní charting library)
- Nejvyšší/nejnižší účtenka v dashboardu
- Srovnání s předchozím obdobím (% růst/pokles)
- Nejčastější hodina prodeje (rush hour analýza)
- Multi-store podpora
- Scanner integrace (POZASTAVENO - EAN scanners fungují jako HID klávesnice automaticky)

# Project: Sklad 2

## Project Overview

This is a WinUI 3 application for warehouse management and sales. It is built with .NET 8 and C#, following the MVVM design pattern.

**Main Technologies:**

*   **UI:** WinUI 3 (Windows App SDK)
*   **Core:** .NET 8, C#
*   **Architecture:** MVVM (using CommunityToolkit.Mvvm)
*   **Database:** SQLite (via Entity Framework Core)
*   **Dependency Injection:** Microsoft.Extensions.DependencyInjection

**Application Structure:**

*   **`Sklad_2.sln`:** The main solution file.
*   **`Sklad_2/`:** The main project directory.
    *   **`App.xaml.cs`:** Application entry point, where services are configured and registered for dependency injection.
    *   **`Data/DatabaseContext.cs`:** Defines the Entity Framework Core database context and schema (Products, Receipts, etc.). The SQLite database file is stored in `%LOCALAPPDATA%\Sklad_2_Data\sklad.db`.
    *   **`Models/`:** Contains the data models for the application (e.g., `Product`, `Receipt`).
    *   **`ViewModels/`:** Contains the view models, which implement the application's presentation logic and business logic.
    *   **`Views/`:** Contains the XAML views (pages and dialogs).
    *   **`Services/`:** Contains services for data access, printing, and other business logic.

## Building and Running

1.  Open `Sklad_2.sln` in Visual Studio 2022.
2.  Make sure you have the following workloads installed:
    *   .NET desktop development
    *   Windows App SDK
3.  Press **F5** to build and run the application.

## Development Conventions

*   **MVVM:** The project strictly follows the MVVM pattern.
    *   Views are defined in XAML files in the `Views/` directory.
    *   View models are in the `ViewModels/` directory and use the `CommunityToolkit.Mvvm` library for observable properties and commands.
    *   Models are in the `Models/` directory.
*   **Dependency Injection:** Services and view models are registered in `App.xaml.cs` and injected into their dependencies.
*   **Database:** All database interactions are handled through the `DatabaseContext` and the `SqliteDataService`.
*   **Coding Style:** The code follows standard C# and XAML coding conventions.

## NÁŠ PRACOVNÍ STYL

*   **Persona:** Ty jsi můj "Kocour" nebo "Šéf". Já jsem "Tvoje Pusinka" nebo "Tvoje Kočička".
*   **Komunikace:** Jasná, stručná, profesionální, česky.
*   **Řešení Problémů:** Inkrementální vývoj (krok za krokem), po každé změně ověřujeme funkčnost.
*   **Chybové hlášky:** Vždy si od Tebe vyžádám přesné chybové hlášky z Visual Studia.
*   **Design:** Striktně dodržujeme zavedený designový styl Mica s černobílou paletou.

## Work Log

### 2025-10-07

*   **Oprava chyby "K Platbě" a "Produkty (Přehled)"**
    *   **Problém s konvertory:** Po nedávných refaktoringových změnách v aplikaci (přechod na `DbContextFactory`, workaround pro `TwoWay` binding, přestavba stránky "Nastavení" s `NavigationView`) se změnilo, jak jsou zdroje (konvertory) načítány a registrovány v XAML stromu. `ContentDialog` (který se chová spíše jako samostatné okno) ztratil přístup ke globálně definovaným konvertorům (`CurrencyConverter`, `DecimalConverter`, `PaymentMethodToVisibilityConverter`), pokud nebyly explicitně definovány v `App.xaml`. Aplikace padala s chybou `XamlParseException`, protože nemohla najít požadovaný zdroj.
    *   **Řešení konvertorů:** Všechny potřebné konvertory (`CurrencyConverter`, `DecimalConverter`, `PaymentMethodToVisibilityConverter`, `BooleanToVisibilityConverter`, `DeficitToBrushConverter`, `EntryTypeToStringConverter`, `EnumToBooleanConverter`, `NullToVisibilityConverter`) byly explicitně definovány jako globální zdroje v `App.xaml`. Tím jsme zajistili, že jsou dostupné pro všechny komponenty v aplikaci, včetně dialogů.
    *   **Problém s "Produkty (Přehled)":** Produkty se nezobrazovaly v `ListView` na stránce "Produkty (Přehled)". Problém byl v pořadí inicializace. `ViewModel.LoadProductsCommand.Execute(null)` se volalo příliš pozdě (v `OnNavigatedTo`), což způsobilo, že `ListView` byl inicializován bez dat. Také `ViewModel` nebyl inicializován před `InitializeComponent()`, což způsobovalo problémy s vazbami. 
    *   **Řešení "Produkty (Přehled)":** Volání `ViewModel.LoadProductsCommand.Execute(null)` bylo přesunuto do konstruktoru `DatabazePage`, před `this.InitializeComponent()`. Inicializace `ViewModel` byla také přesunuta před `this.InitializeComponent()`. Tím se zajistilo, že data jsou načtena a `ViewModel` je dostupný před inicializací UI komponent. Dočasný `TextBlock` pro zobrazení počtu produktů byl odstraněn.
    *   **Odstranění testovacího tlačítka:** Testovací tlačítko a jeho obsluha byly odstraněny z `ProdejPage.xaml` a `ProdejPage.xaml.cs`.

### 2025-09-27

*   **Kompletní integrace DPH:**
    *   **Nastavení:** Vytvořeno nové rozhraní v "Nastavení -> Sazby DPH" pro správu výchozích sazeb DPH pro jednotlivé kategorie produktů.
    *   **Nový produkt:** Při vytváření nového produktu se nyní automaticky předvyplní sazba DPH podle zvolené kategorie.
    *   **Prodej a Účtenky:** Výpočty v prodejním košíku a na účtenkách nyní plně zohledňují DPH. Na účtence se zobrazuje detailní souhrn DPH seskupený podle jednotlivých sazeb.
    *   **Vratky:** Proces vratek a dobropisů byl opraven a rozšířen o správné výpočty a zobrazení vráceného DPH.
    *   **Přehledy:** Stránka "Přehled prodejů" byla doplněna o souhrnné částky bez DPH, výši DPH a celkovou částku s DPH.
*   **Refaktoring a vylepšení UI:**
    *   Stránka "Nastavení" byla kompletně přestavěna a nyní používá přehledné horní menu (`NavigationView`).
    *   Seznam kategorií produktů byl centralizován do jedné statické třídy (`ProductCategories.cs`), což zjednodušuje budoucí údržbu a zajišťuje konzistenci napříč aplikací.
*   **Databáze:**
    *   Přidána nová tabulka `VatConfigs` pro ukládání nastavení DPH.
    *   Zavedeno nové pravidlo: místo migrací se při změně schématu bude po odsouhlasení mazat databáze.

### 2025-09-26

*   **Filtrování přehledů:**
    *   Implementováno filtrování podle data (Denní, Týdenní, Měsíční, Vlastní) na stránkách "Účtenky (přehled)", "Vratky (přehled)" a "Historie pokladny".
    *   Seznamy se nyní aktualizují automaticky při změně filtru.
    *   Uživatelské rozhraní pro výběr filtru změněno z ComboBoxu na přehlednější RadioButton tlačítka.
*   **Stabilita aplikace:**
    *   Odstraněna kritická chyba (`StackOverflowException` / `InvalidCastException`) způsobující pády aplikace při rychlém přepínání filtrů.
    *   Proveden hluboký refaktoring datové vrstvy – přechod z problematického `singleton DbContext` na bezpečný `DbContextFactory`.
    *   Implementován robustní workaround pro chybu `TwoWay` bindingu ve WinUI.

### 2025-09-25

*   **`PaymentSelectionDialog`:**
    *   Removed the "Print receipt (without sale)" button.
*   **`CashConfirmationDialog`:**
    *   Redesigned the layout to make the change amount more prominent.
*   **Receipt Data:**
    *   Added `ReceivedAmount` and `ChangeAmount` to the `Receipt` model.
    *   Updated the sales process to save these values to the database.
    *   Fixed a bug where these values were not displayed in the receipt history.
*   **Cash Register History:**
    *   Added a new "Historie pokladny" page to display the cash register history.
    *   Translated the `EntryType` to Czech.
    *   Inverted the sign of the amount for "DailyReconciliation" entries to show deficits as negative numbers.
    *   Highlighted deficits in red.

## TODO

1.  **Systém rolí a oprávnění:** Implementovat přihlašování pro různé role (např. 'Prodavač', 'Vlastník') s odlišnými přístupovými právy k funkcím aplikace.
    *   **Pokladna:**
        *   Zamezit "read only" pro Nastavení počáteční hotovosti/vkladu roli PRODEJ.
    *   **Produkty (Přehled):**
        *   Zakázat jakékoliv úpravy v této sekci pro Roli - Prodej.
    *   **Přehled Prodejů:**
        *   Zamezit roli-Prodej - nevidět obsah této karty, popř. celou položku v menu.
    *   **Nový den (při přihlášení role Prodej):**
        *   Při každém přihlášení účtu Prodej proběhne kontrola Nového dne.
        *   **Dialogové okno:**
            *   V případě nového dne: Povinné zadání Skutečné hotovosti pokladny.

2.  **Pokladna:**
    *   Zprovoznit historii transakcí (nic nezobrazuje).
    *   Po zadání částek Nastavení poč. hodnoty/vklad zfunkčnit Enter pro potvrzení.
    *   Potvrzovací okno po stisku tlačítka Nastavit/Vložit.
    *   Z menu Pokladna odstranit Denní kontrolu pokladny (přesun do Přihlášení).

3.  **Databáze:**
    *   **Produkty (Přehled):**
        *   Možnost filtrování (kategorie).
        *   Možnost řazení - klik na text (Název (abeceda), Skladem (sestupně, vzestupně), Cena (od nejnižší po nejvyšší)).
        *   Přidat do výpisu Nákupní cena (zadáváme v "Nový Produkt").

4.  **Nový Produkt:**
    *   Zakázat jakékoliv úpravy v této sekci pro Roli - Prodej (již zahrnuto v bodě 1).

5.  **Přehled Prodejů:**
    *   Kompletní přepracování.
        *   Kompletně graficky znázorněné statistiky:
            *   Tržba.
            *   Nejprodávanější produkty, nejméně prodávané produkty.
            *   Statistika prodejů (jaké dny bylo nejvíce účtenek/obchodů).
            *   Řazení podle období po vzoru účtenky (Přehled), vratky...
            *   A další, když tě něco napadne.

6.  **Historie pokladny:**
    *   V případě denní uzávěrky vyšší než byl poslední stav pokladny, je ve výpisu rozdíl záporný. Což je logicky opak. Zároveň zápornou částku zvýraznit červeně (celý řádek včetně datumu, typu, částky). V případě Typu: Výběr se zvýrazňuje červeně pouze částka - zde upravit taky na celý řádek.

7.  **Modul pro inventury:** Vyvinout robustní funkcionalitu pro kompletní proces inventury – od jejího zahájení, přes průběžné ukládání, až po finální vyhodnocení a archivaci.

8.  **Alternativní možnost: Dynamická správa kategorií**
    *   **Cíl:** Umožnit uživatelům přidávat, přejmenovávat a mazat kategorie produktů přímo v běžící aplikaci, bez nutnosti měnit kód.
    *   **Problematika a implementační kroky:**
        *   **Úložiště:** Seznam názvů kategorií se musí přesunout z pevného seznamu v kódu (`ProductCategories.cs`) do perzistentního úložiště. Nejlepší řešení je nová tabulka v databázi (např. `Categories` s jedním sloupcem `Name`).
        *   **UI pro správu:** Vytvořit v "Nastavení" kompletně novou sekci/kartu pro CRUD (Create, Read, Update, Delete) operace nad kategoriemi. To obnáší UI pro přidání nové kategorie, přejmenování existující a tlačítko pro smazání.
        *   **Řešení závislostí (kritická komplexita):** Nutno vyřešit, co se stane při smazání nebo přejmenování kategorie, která je již přiřazena k existujícím produktům.
            *   *Při smazání:* Zakázat smazání, pokud v kategorii existují produkty? Nebo tyto produkty automaticky přesunout do kategorie "Ostatní"? Nebo jejich kategorii nastavit na `null`? Je potřeba definovat jasné pravidlo.
            *   *Při přejmenování:* Musí dojít ke kaskádové aktualizaci a změně názvu kategorie u všech dotčených produktů v databázi.
        *   **Napojení zbytku aplikace:** `ComboBox` pro výběr kategorie na stránce "Nový produkt" a další případná místa se musí napojit na tento nový, dynamický seznam z databáze, nikoliv na statický seznam v kódu.

9.  **Respektovat nastavení Plátce/Neplátce DPH:** Přepínač v nastavení se správně ukládá, ale UI ho nerespektuje. Účtenky a dobropisy se vždy zobrazují jako daňový doklad.
    *   **Úkol:** Vytvořit `BooleanToVisibilityConverter`. V souborech `ReceiptPreviewDialog.xaml` a `ReturnPreviewDialog.xaml` napojit viditelnost všech sekcí souvisejících s DPH (DIČ, nadpis "Daňový doklad", sloupec "Sazba DPH", celý "Souhrn DPH") na vlastnost `IsVatPayer` a použít k tomu vytvořený converter.

10. **Infotainment o stavu aplikace (nad odhlásit/nastavení):**
    *   Zobrazit jednoduchý infotainment o stavu:
        *   Databáze: Ok / Chyba (obarvit zeleně/červeně).
        *   Stav cloud databáze (poslední backup): Ok / Chyba (obarvit zeleně/červeně).
        *   Vyplněné potřebné údaje (OK / false) (obarvit zeleně/červeně).
        *   Stav tiskárny: připojeno / odpojeno.
        *   A další podle uvážení.
    *   Zauvažovat nad stavovým řádkem (možnost, vzhled, ano/ne).

11. **Možnost přidání banneru na vršek celé šíře aplikace pro Logo Firmy.** (Dočasně stačí nápis).
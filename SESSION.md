# Session Management - Sklad_2

Pracovní soubor pro Claude Code sessions. Detailní session logy jsou v `SESSION_ARCHIVE.md`.

---

## 📝 Session Workflow

### Začátek session
**Příkazy:** `pokracuj` / `pokračujem` / `pokračujeme`
- Claude načte tento soubor a pokračuje v práci podle TODO listu

### Konec session
**Příkazy:** `konec` / `končíme` / `končit`
- Claude shrne provedenou práci
- Aktualizuje tento soubor a TODO list v CLAUDE.md

---

## 📅 **Poslední session: 25. listopad 2025**

### ✅ Hotovo:
**Dokončení systému backup cesty - navigace do System panelu**

**Upraveno 2 soubory:**
- Views (1): NastaveniPage.xaml.cs - přidána NavigateToSystemPanel() metoda
- Main (1): MainWindow.xaml.cs - integrace navigace do ShowBackupPathRequiredDialog()

**Klíčové změny:**
- ✅ **Navigace z backup dialogu** - tlačítko "Jít do Nastavení" nyní otevře přímo System panel
- ✅ **Automatické nastavení NavigationView** - správně označí System jako vybranou položku
- ✅ **Dokončení backup path systému** - kompletní workflow od dialogu k nastavení cesty

**Technické detaily:**

1. **NavigateToSystemPanel() metoda v NastaveniPage.xaml.cs**:
   ```csharp
   public void NavigateToSystemPanel()
   {
       // Skrýt všechny panely
       CompanySettingsPanel.Visibility = Visibility.Collapsed;
       VatSettingsPanel.Visibility = Visibility.Collapsed;
       CategoriesPanel.Visibility = Visibility.Collapsed;
       UsersPanel.Visibility = Visibility.Collapsed;
       SystemSettingsPanel.Visibility = Visibility.Visible;
       AboutPanel.Visibility = Visibility.Collapsed;

       // Nastavit vybranou položku v NavigationView
       foreach (var item in NavView.MenuItems)
       {
           if (item is NavigationViewItem navItem && navItem.Tag?.ToString() == "System")
           {
               NavView.SelectedItem = navItem;
               break;
           }
       }
   }
   ```

2. **Integrace v ShowBackupPathRequiredDialog() - MainWindow.xaml.cs**:
   ```csharp
   if (result == ContentDialogResult.Primary)
   {
       // Navigate to Settings
       NavView.SelectedItem = NavView.MenuItems.Cast<NavigationViewItem>()
           .FirstOrDefault(item => item.Tag?.ToString() == "Nastaveni");
       var settingsPage = new Views.NastaveniPage();
       ContentFrame.Content = settingsPage;
       
       // Navigate directly to System panel
       settingsPage.NavigateToSystemPanel();
   }
   ```

   **Proč to bylo potřeba:**
   - Uživatel po kliknutí na "Jít do Nastavení" v backup dialogu je automaticky přesunut přímo do System panelu
   - Nemusí ručně hledat správnou kartu - UX je plynulé
   - Navigace je přesná a deterministic

**Výsledný stav Backup Path systému:**
- 🛡️ **Povinné nastavení** - aplikace se nespustí bez nastavené backup cesty
- ⚠️ **Warning v Status Bar** - blikající "CHYBA" pokud není nastavena cesta
- 🚫 **Blokování funkcionalita** - prodeje a operace nejsou možné bez backup cesty
- 📂 **Dialog s instrukcemi** - jasné pokyny pro uživatele
- 🎯 **Přímá navigace** - tlačítko "Jít do Nastavení" otevře přímo System panel

### 🧪 Otestováno:
- ✅ Dialog při spuštění - zobrazí se když cesta není nastavena
- ✅ Status Bar blinking - bliká červenou dokud není nastavena
- ✅ Navigace do System panelu - přímý přesun na správnou kartu
- ✅ Build bez chyb - kompilace proběhla úspěšně

### 🔧 Další úkoly:
1. **Upravit tisk účtenek (prodej vs uplatnění)** - rozlišit formát tisku
2. **Export uzavírek do CSV/PDF**
3. **Skutečný PrintService** - implementovat skutečný tisk
4. **Vylepšit error handling** - lokalizované chybové hlášky

---

## 🎓 Klíčové naučené lekce

### WinUI 3 / XAML specifika

1. **ViewModel PŘED InitializeComponent()**
   ```csharp
   public SomePage()
   {
       // DŮLEŽITÉ: ViewModel MUSÍ být nastaven PŘED InitializeComponent()
       ViewModel = (Application.Current as App).Services.GetRequiredService<SomeViewModel>();
       this.InitializeComponent();  // x:Bind nyní funguje správně
   }
   ```

2. **Clean + Rebuild je kritický**
   - Při změnách XAML/ViewModels vždy: **Build → Clean Solution → Rebuild Solution**
   - WinUI/XAML projekty cachují sestavení

3. **ContentDialog COMException workaround**
   - Pouze 1 ContentDialog najednou
   - Řešení: 800ms delay + retry s 300ms + try-catch
   ```csharp
   this.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, async () =>
   {
       await Task.Delay(800);
       try { await dialog.ShowAsync(); }
       catch (COMException)
       {
           await Task.Delay(300);
           try { await dialog.ShowAsync(); }
           catch { /* Tiché selhání */ }
       }
   });
   ```

4. **XamlRoot čekání - robustní přístup**
   ```csharp
   // Robustní čekání místo pevného delay
   int retries = 0;
   while (this.Content?.XamlRoot == null && retries < 20)
   {
       await Task.Delay(50);
       retries++;
   }
   ```

5. **Page.Loaded event pro auto-refresh**
   ```csharp
   this.Loaded += (s, e) => ViewModel.LoadDataCommand.Execute(null);
   ```

6. **PasswordBox binding**
   - Password property je write-only (security)
   - Nelze použít x:Bind TwoWay
   - Řešení: Event handlers (PasswordChanged)

7. **ToggleButtonStyle (RadioButton)**
   - WinUI 3 RadioButton nemá kombinované stavy (CheckedPointerOver, etc.)
   - Řešení: Separátní HoverBorder overlay s Opacity control
   - Pressed stav nesmí měnit background (jinak přepíše Checked stav)

8. **VisualState priority**
   - Stavy z různých VisualStateGroups se aplikují současně
   - CommonStates vs CheckStates - výsledek není vždy předvídatelný
   - Řešení: Explicitně nastavit všechny vlastnosti v každém stavu

9. **StartsWith vs Contains pro vyhledávání**
   - Pro prefix matching (EAN, názvy) použít `StartsWith()`
   - `Contains()` najde příliš mnoho výsledků

10. **Window vs Page - DataContext a binding**
   - `Window` nemá property `DataContext` (pouze `Page` má)
   - `Window` má omezení s `{x:Bind}` na některých prvcích
   - **Řešení:** Nastavit DataContext na konkrétní element (např. Grid, Border)
   ```csharp
   this.InitializeComponent();
   StatusBarBorder.DataContext = this;  // Nastavení jen pro část UI
   ```
   - Pro Visibility binding v Window raději použít `{Binding}` místo `{x:Bind}`

11. **ListView.HeaderTemplate binding problémy**
   - `ListView.HeaderTemplate` nemá správný DataContext v některých případech
   - **Řešení:** Použít samostatný `Grid` pro hlavičku + `ItemsRepeater` pro data
   ```xaml
   <!-- Hlavička -->
   <Grid>
       <TextBlock Text="Header" Visibility="{x:Bind ViewModel.IsVisible}"/>
   </Grid>
   <!-- Data -->
   <ItemsRepeater ItemsSource="{x:Bind Items}">
       <ItemsRepeater.ItemTemplate>
           <DataTemplate>
               <TextBlock Text="{x:Bind Property}" Visibility="{Binding ParentProperty}"/>
           </DataTemplate>
       </ItemsRepeater.ItemTemplate>
   </ItemsRepeater>
   ```

12. **Window.Current je null v WinUI 3** ⚠️
   - `Microsoft.UI.Xaml.Window.Current` vrací `null`
   - **Řešení pro FolderPicker:**
   ```csharp
   // V App.xaml.cs
   public Window CurrentWindow { get; set; }

   // V MainWindow konstruktoru
   app.CurrentWindow = this;

   // Pro FolderPicker
   var app = Application.Current as App;
   var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(app.CurrentWindow);
   ```

13. **Window_Closed a async operace** ⚠️
   - Přímé volání file operací v `Window_Closed` může způsobit Access Violation
   - **Řešení:**
   ```csharp
   private async void Window_Closed(object sender, WindowEventArgs args)
   {
       // Prevent multiple executions
       if (_isClosing) return;
       _isClosing = true;
       args.Handled = true;  // Cancel initial close

       // Perform operations
       await Task.Run(() => PerformBackup());
       await completionDialog.ShowAsync();

       // Unsubscribe and exit
       this.Closed -= Window_Closed;
       this.DispatcherQueue.TryEnqueue(() => Environment.Exit(0));
   }
   ```
   - Flag `_isClosing` zabraňuje nekonečnému cyklu
   - `Environment.Exit(0)` vrací správný exit code (ne -1)

14. **Visual Tree Traversal vs Data Binding** ⚠️ NOVÉ!
   - `FindVisualChildren<T>()` má problémy s načasováním v `Page_Loaded`
   - Checkboxy mohou ještě nebýt plně inicializované
   - **VŽDY preferovat data binding:**
   ```csharp
   // ❌ ŠPATNĚ - visual tree traversal
   foreach (var child in FindVisualChildren<CheckBox>(grid))
   {
       if (child.Tag?.ToString() == "NotIssued")
           return child.IsChecked == true;
   }

   // ✅ SPRÁVNĚ - data binding
   [ObservableProperty]
   private bool filterNotIssued = true;

   partial void OnFilterNotIssuedChanged(bool value)
   {
       UpdateFiltersAndReload();
   }
   ```
   - x:Bind je compile-time bezpečné a spolehlivé

15. **ListView ItemContainerStyle pro zarovnání** ⚠️ NOVÉ!
   - ListView automaticky přidává padding do ListViewItem
   - Hlavičky a data se nezarovnají bez úpravy
   - **Řešení:**
   ```xaml
   <ListView.ItemContainerStyle>
       <Style TargetType="ListViewItem">
           <Setter Property="Padding" Value="0"/>
           <Setter Property="Margin" Value="0"/>
           <Setter Property="MinHeight" Value="0"/>
           <Setter Property="HorizontalContentAlignment" Value="Stretch"/>
       </Style>
   </ListView.ItemContainerStyle>
   ```
   - Pak musí Header Border mít **stejný** BorderThickness a Padding jako data rows

### Databáze (EF Core + SQLite)

1. **Žádné migrace!**
   - Při změně schématu: Smazat `%LocalAppData%\Sklad_2_Data\sklad.db`
   - Používá se `Database.EnsureCreated()` místo migrací

2. **DbContextFactory pattern**
   - Registrace: `services.AddDbContextFactory<DatabaseContext>()`
   - Workaround pro WinUI TwoWay binding issues

3. **Hybrid Backup Strategy**
   - Aplikace běží 100% offline z LocalAppData
   - Záloha na OneDrive/vlastní složku při zavření
   - Restore při startu pokud backup je novější
   - **NIKDY** neukládat živou databázi přímo na OneDrive (riziko korupce)

### Pokladna (Cash Register)

**EntryTypes:**
- `DayStart` - NASTAVÍ hodnotu (nepřičítá!)
- `DayClose` - NASTAVÍ hodnotu
- `Deposit`, `Sale` - přičte
- `Withdrawal`, `DailyReconciliation`, `Return` - odečte

**Důležité:**
- DayStart != InitialDeposit (matoucí názvy jsou špatné)
- Kontrola `LastDayCloseDate` - pouze 1× denně
- Robustní validace (0-10M Kč)

### PPD Compliance (Primární pokladní doklad)

**Profesionální storno systém:**
- ❌ **NIKDY NEMAZAT účtenku** (nelegální!)
- ✅ Vytvořit storno účtenku s **negativními hodnotami**
- Storno pokračuje v číselné řadě (2025/0007 → 2025/0008)
- `IsStorno = true`, `OriginalReceiptId` pro odkaz

**Formát účtenek:**
- `ReceiptYear` + `ReceiptSequence` → "2025/0001"
- Nový rok = reset sequence (2026/0001)

**Export pro FÚ:**
- HTML tabulka (možnost Ctrl+P → PDF)
- Všechny účtenky za období
- Informace o firmě (IČ, DIČ, plátce DPH)
- Souhrn (počet, celkem, DPH)

---

## 🐛 Známé problémy a workarounds

### Problém: LiveCharts2 nestabilní
- Verze 2.0.0-rc2 způsobuje runtime crashes
- **Řešení:** Nepoužívat grafy, nahradit stat kartami

### Problém: TwoWay binding na DbContext entity
- WinUI má problém s TwoWay bindingem na EF entity
- **Řešení:** DbContextFactory + ViewModel properties

### Problém: ContentDialog resource access
- Dialogy ztrácejí přístup ke global resources
- **Řešení:** Všechny konvertory explicitně definovat v App.xaml

### Problém: ListView initialization
- Data musí být načtena před `InitializeComponent()`
- **Řešení:** Načíst data v konstruktoru ViewModelu

### Problém: Build warningy - platform support
- Mnoho warningů "is only supported on Windows 10.0.17763.0+"
- **Vysvětlení:** Analyzátor zatím neví, že projekt cílí POUZE Windows
- WinUI build proces tyto warningy automaticky vyřeší
- **Lze ignorovat** - zmizí po dokončení buildu

### Problém: Visual Tree Traversal timing issues ⚠️ NOVÉ!
- `FindVisualChildren<T>()` v `Page_Loaded` není spolehlivé
- Kontroly mohou být volány dříve než je visual tree připraven
- **Řešení:** VŽDY používat data binding místo visual tree hledání

---

## 📝 Důležité poznámky

### Build proces
- **Build vždy přes Visual Studio 2022**, ne přes CLI
- Při problémech: Clean Solution → Rebuild Solution

### Git operace
- **⚠️ DŮLEŽITÉ: GIT OVLÁDÁ UŽIVATEL - NIKDY NEPOUŽÍVAT GIT PŘÍKAZY!**
- Uživatel si git operations dělá sám

### Databáze reset
- Při změnách schématu: `%LocalAppData%\Sklad_2_Data\sklad.db` smazat
- Projekt nemá unit testy

### Neplátce DPH - FÚ požadavky
**V aplikaci:**
- ✅ Prodeje (účtenky s DPH rozpadem)
- ✅ Pokladna (denní otevírka/uzavírka)
- ✅ Profesionální storno systém
- ✅ Export pro FÚ (HTML/PDF)
- ✅ Evidence produktů (sklad)

**Papírově (dostatečné!):**
- ✅ Faktury od dodavatelů (šanony)
- ✅ Inventury (spočítat, zapsat, podpis)

---

## 📋 Aktuální TODO List

**Pro aktuální seznam úkolů viz `CLAUDE.md` → sekce TODO List**

### 🔴 Prioritní úkoly (listopad 2025):

1. **Systém dárkových poukazů** ✅ HOTOVO!
   - ✅ Kompletní CRUD operace
   - ✅ Životní cyklus (naskladnění → prodej → využití)
   - ✅ Integrace s POS systémem
   - ✅ Profesionální UI s filtry a statistikami
   - ✅ Data binding místo visual tree traversal
   - ✅ Statistiky nezávislé na filtrech

### ⏳ Sekundární:
- Upravit tisk účtenek (prodej poukazu vs uplatnění)
- Testovat kompletně systém poukazů
- Export uzavírek do CSV/PDF
- Skutečný PrintService (tisk na běžnou tiskárnu)
- Respektovat "Plátce DPH" v tisku
- Scanner integrace (POZASTAVENO - HID funguje automaticky)
- Vylepšit error handling (lokalizované hlášky)

---

## 📊 Aktuální stav projektu

**Hotovo:** 12/15 hlavních funkcí (~80%)

### ✅ Implementováno:
1. Role-based UI restrictions
2. Databáze produktů - vylepšení (filtrování, řazení)
3. Status Bar (Informační panel)
4. Dashboard prodejů (KPI, top/worst produkty, platby)
5. Denní otevírka/uzavírka pokladny
6. DPH systém (konfigurace)
7. Historie pokladny s filtry
8. Dynamická správa kategorií
9. PPD Compliance (profesionální účtenky, storno, export FÚ)
10. UI optimalizace pro neplátce DPH
11. Vlastní cesta pro zálohy + Dialog při zavření
12. **Systém dárkových poukazů (kompletní)** ✅ NOVÉ!

### ⏳ Zbývá:
1. Tisk účtenek - rozlišení prodeje vs uplatnění poukazu
3. Export uzavírek (CSV/PDF)

---

**Poslední aktualizace:** 25. listopad 2025

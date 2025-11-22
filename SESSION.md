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

## 📅 **Poslední session: 19. listopad 2025**

### ✅ Hotovo:
**Vlastní cesta pro zálohy a exporty + Dialog při zavření aplikace**

**Upraveno 6 souborů:**
- Models (1): AppSettings.cs - přidán BackupPath
- Services (2): ISettingsService.cs, SettingsService.cs - přidán GetBackupFolderPath()
- ViewModels (1): NastaveniViewModel.cs - UI pro výběr cesty, ActiveBackupPath zobrazení
- Views (1): NastaveniPage.xaml - UI pro nastavení cesty
- Code-behind (2): NastaveniPage.xaml.cs (FolderPicker), MainWindow.xaml.cs (dialog při zavření)
- DI (1): App.xaml.cs - CurrentWindow property, RestoreFromBackupIfNewerAsync()

**Klíčové změny:**
- ✅ Vlastní konfigurovatelná cesta pro zálohy a exporty FÚ
- ✅ Priorita: Vlastní cesta → OneDrive → Dokumenty (fallback)
- ✅ UI zobrazení aktivní cesty (📁 ikona + modrý text)
- ✅ Dialog "Záloha dokončena" při zavření aplikace
- ✅ Čisté ukončení s exit code 0 (Environment.Exit)
- ✅ Opraveny chyby: NullReferenceException, Invalid window handle, Access Violation
- ✅ Opraveny build warningy (readonly fields, switch expression, object init)

**Technické detaily:**
- `GetBackupFolderPath()` v SettingsService - centralizovaná logika
- Export FÚ používá STEJNOU cestu jako zálohy
- Dialog při zavření: Task.Run() → dialog → Environment.Exit(0) přes DispatcherQueue
- FolderPicker fix: `app.CurrentWindow` místo `Window.Current` (null v WinUI 3)
- Flag `_isClosing` zabraňuje nekonečnému cyklu Window_Closed

### 🧪 Zbývá otestovat:
1. Výběr záložní složky v Nastavení → Systém
2. Ověřit, že záloha se ukládá do vybrané složky
3. Ověřit, že export FÚ se ukládá do stejné složky
4. Zavření aplikace - dialog "Záloha dokončena" + exit code 0

### 🔧 Další úkoly:
1. **PRIORITA:** Systém uživatelských účtů
2. Export uzavírek do CSV/PDF
3. Skutečný PrintService
4. Scanner integrace (POZASTAVENO - HID scanners fungují automaticky)

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

12. **Window.Current je null v WinUI 3** ⚠️ NOVÉ!
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

13. **Window_Closed a async operace** ⚠️ NOVÉ!
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

### Databáze (EF Core + SQLite)

1. **Žádné migrace!**
   - Při změně schématu: Smazat `%LocalAppData%\Sklad_2_Data\sklad.db`
   - Používá se `Database.EnsureCreated()` místo migrací

2. **DbContextFactory pattern**
   - Registrace: `services.AddDbContextFactory<DatabaseContext>()`
   - Workaround pro WinUI TwoWay binding issues

3. **Hybrid Backup Strategy** ⚠️ NOVÉ!
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

1. **Vlastní cesta pro zálohy** ✅ HOTOVO!
   - ✅ Konfigurovatelná cesta v Nastavení → Systém
   - ✅ Priorita: Vlastní → OneDrive → Dokumenty
   - ✅ Export FÚ používá stejnou cestu
   - ✅ Dialog při zavření aplikace
   - 🧪 Zbývá otestovat v produkci

2. **Systém uživatelských účtů** ⏳ NEXT
   - Implementovat databázovou tabulku Users
   - Nahradit fixed roles (Admin/Prodej) skutečnými uživateli
   - Každý prodavač vlastní login + jméno
   - Role/oprávnění per uživatel
   - SellerName bude skutečné jméno místo "Prodej"

### ⏳ Sekundární:
- Export uzavírek do CSV/PDF
- Skutečný PrintService (tisk na běžnou tiskárnu)
- Respektovat "Plátce DPH" v tisku
- Scanner integrace (POZASTAVENO - HID funguje automaticky)
- Vylepšit error handling (lokalizované hlášky)

---

## 📊 Aktuální stav projektu

**Hotovo:** 11/14 hlavních funkcí (~79%)

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
11. **Vlastní cesta pro zálohy + Dialog při zavření** ✅ NOVÉ!

### ⏳ Zbývá:
1. Systém uživatelských účtů
2. Export uzavírek (CSV/PDF)
3. Tisk (PrintService je placeholder)

---

**Poslední aktualizace:** 19. listopad 2025

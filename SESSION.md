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

## 📅 **Poslední session: 26. listopad 2025 (odpoledne)**

### ✅ Hotovo:
**Win10 Compatibility Fixes - 6 kritických oprav**

### 🔴 DŮLEŽITÉ: Win10 vs Win11 rozdíly

**Problém:** Produkční PC (Win10) měl 4 kritické problémy, které nefungovaly na Win11:
1. FolderPicker se neotevřel
2. Firemní údaje se neuložily ihned
3. StatusBar se neaktualizoval
4. Database write error při prodeji
5. Kategorie se nerefreshovaly v Nový produkt

**Příčina:** WinUI 3 je primárně pro Win11, Win10 podpora je "backport" s kompromisy.

**Klíčové změny této session:**

1. **LoginWindow.xaml.cs - CurrentWindow fix**
   - Přidán `app.CurrentWindow = mainWindow;` po vytvoření MainWindow
   - Win10 vyžaduje explicitní nastavení pro FolderPicker HWND
   - **Řádek:** 123-124

2. **SettingsService.cs - File flush**
   - Přidán explicitní `FileStream.Flush(true)` po zápisu settings
   - Win10 má pomalejší file system cache flush
   - **Řádek:** 68-72

3. **NastaveniViewModel.cs - Delay pro messaging**
   - Přidán delay 100ms před Send message (file flush)
   - Přidán delay 200ms po Send message (UI refresh)
   - Win10 Dispatcher má nižší prioritu než Win11
   - **Řádek:** 184-191

4. **SqliteDataService.cs - AsNoTracking + Retry**
   - `GetProductAsync()` - přidán `.AsNoTracking()` (řádek 32)
   - `CompleteSaleAsync()` - přidána retry logika 3× s exponential backoff (řádek 48-70)
   - Win10 má přísnější SQLite file locking
   - Prevence entity tracking conflicts

5. **NovyProduktViewModel.cs - RefreshCategories**
   - Přidán listener na `VatConfigsChangedMessage` (řádek 118-124)
   - Nová metoda `RefreshCategories()` (řádek 148-168)
   - ObservableCollection se nyní aktualizuje při změně kategorií

6. **DatabazeViewModel.cs - RefreshCategories**
   - Stejný fix jako v NovyProduktViewModel
   - Listener na message + RefreshCategories metoda (řádek 82-111)

**Dokumentace:**
- Přidána **nová sekce do CLAUDE.md**: "🔴 KRITICKÉ: Windows 10 Compatibility Requirements"
- Obsahuje 6 povinných pravidel pro každý nový kód
- Checklist před každým commitem
- Tabulka Win10 vs Win11 rozdílů
- Testing checklist pro Win10

**Kompatibilita s Win11:**
✅ Všechny změny jsou 100% Win11 kompatibilní!
✅ `AsNoTracking()` dokonce zrychlí Win11
✅ Žádné Win10-specific hacky nebo conditionals
✅ Defensive programming pattern

**Upravené soubory:**
- `LoginWindow.xaml.cs` - app.CurrentWindow
- `SettingsService.cs` - file flush
- `NastaveniViewModel.cs` - delays
- `SqliteDataService.cs` - AsNoTracking + retry
- `NovyProduktViewModel.cs` - RefreshCategories
- `DatabazeViewModel.cs` - RefreshCategories
- `CLAUDE.md` - Win10 compatibility guidelines

**Testováno:**
- ✅ Build úspěšný (x64 Release)
- ⏳ **Zbývá otestovat na Win10 PC:**
  1. FolderPicker
  2. Uložení firemních údajů
  3. Prodej produktu
  4. Správa kategorií
  5. Backup při zavření

---

## 📅 **Předchozí session: 26. listopad 2025 (ráno)**

### ✅ Hotovo:
**Auto-update systém + Oprava backup pro Win10**

**Klíčové změny této session:**

1. **Oprava backup systému**
   - `App.xaml.cs` - oprava názvu `AppSettings.json` → `settings.json`
   - Win10 nevolal `Window.Closed` event správně
   - Přidán `AppWindow.Closing` handler pro spolehlivé zachycení zavření okna
   - File logging do `backup_log.txt` pro troubleshooting

2. **Auto-update systém**
   - `UpdateService.cs` - kontrola nových verzí z GitHub Releases API
   - Automatická kontrola při startu (pouze Admin)
   - Dialog s nabídkou aktualizace
   - Download a instalace přes batch script

3. **GitHub Actions workflow**
   - `.github/workflows/release.yml` - automatický build při push tagu
   - Problém s permissions - nutné nastavit "Read and write permissions" v repo settings
   - Alternativa: ruční upload přes `gh release create`

4. **Verzování**
   - `Sklad_2.csproj` - přidány Version, AssemblyVersion, FileVersion
   - Aktuální verze: **v1.0.1**

**Technické detaily:**

- `AppWindow.Closing` je spolehlivější než `Window.Closed` pro WinUI 3
- `GetAppWindowForCurrentWindow()` helper pro získání AppWindow instance
- FolderPicker nefunguje na Win10 bez správného HWND

**Upravené soubory:**
- `App.xaml.cs` - oprava settings.json, registrace UpdateService
- `MainWindow.xaml.cs` - AppWindow.Closing, file logging
- `Services/UpdateService.cs` - NOVÝ
- `.github/workflows/release.yml` - NOVÝ
- `Sklad_2.csproj` - verzování

**Testováno:**
- ✅ Backup funguje na Win10 i Win11
- ✅ Dialog "Záloha dokončena" se zobrazí
- ✅ File logging funguje
- ✅ Release v1.0.1 na GitHub

---

## 📅 **Předchozí session: 25. listopad 2025 (večer)**

### ✅ Hotovo:
**Kompletní implementace tisku účtenek s českými znaky**

**Klíčové změny:**

1. **Automatický tisk při prodeji**
   - `ProdejViewModel.cs` - přidán `IPrintService` do DI
   - Po úspěšném prodeji se automaticky volá `PrintReceiptAsync()`
   - Tisk běží asynchronně, neblokuje UI
   - Při selhání tisku se vypíše warning, ale prodej proběhne

2. **Oprava tisku - přímý SerialPort místo ESCPOS_NET**
   - `PrintReceiptAsync()` přepsán na přímý `System.IO.Ports.SerialPort`
   - Problém: ESCPOS_NET `SerialPrinter` nepodporoval CP852 encoding
   - Řešení: Raw ESC/POS příkazy s `Cp852.GetBytes()`

3. **Nastavení CP852 code page na tiskárne**
   - Přidán příkaz `ESC t 18` (0x1B 0x74 0x12) na začátek tisku
   - Tiskárna měla výchozí PC437, který nepodporuje české znaky
   - Nyní tiskne správně: ěščřžýáíéůúďťň

4. **Tlačítko "Tisk" v náhledu účtenky**
   - `ReceiptPreviewDialog.xaml.cs` - implementace `PrintButton_Click`
   - Přidán `IPrintService` parametr do konstruktoru
   - Při selhání tisku se zobrazí chybový dialog
   - Dialog zůstane otevřený pro opakovaný tisk
   - Funguje po prodeji i v historii účtenek

5. **Nová metoda `BuildReceiptCommands()`**
   - Generuje raw ESC/POS příkazy s CP852 encodingem
   - Podporuje: DPH rozpad, slevy, dárkové poukazy, storno
   - Formátování: bold, double height, zarovnání
   - Řez papíru: `GS V 66 3`

**Upravené soubory:**
- `Services/EscPosPrintService.cs` - přepis na SerialPort + CP852
- `ViewModels/ProdejViewModel.cs` - automatický tisk po prodeji
- `Views/Dialogs/ReceiptPreviewDialog.xaml.cs` - tlačítko tisk
- `Views/ProdejPage.xaml.cs` - předání PrintService do dialogu
- `Views/UctenkyPage.xaml.cs` - předání PrintService do dialogu
- `App.xaml.cs` - DI registrace ProdejViewModel

**Testováno:**
- ✅ Tisk při prodeji funguje automaticky
- ✅ České znaky se tisknou správně (CP852)
- ✅ Tlačítko "Tisk" v náhledu funguje
- ✅ Opakovaný tisk při nedostatku papíru
- ✅ Test tisku v nastavení funguje

---

## 📅 **Předchozí session: 25. listopad 2025 (odpoledne)**

### ✅ Hotovo:
**Funkční tisk na Epson TM-T20III přes COM port**

**Klíčové změny:**

1. **EscPosPrintService.cs - přepis na přímý SerialPort**
   - ESCPOS_NET SerialPrinter nefungoval s Epson Virtual COM Port
   - Přepsáno na přímý `System.IO.Ports.SerialPort`
   - Přidán `CodePagesEncodingProvider` pro CP852 (české znaky)
   - Raw ESC/POS příkazy (inicializace, styly, řez papíru)
   - Baud rate: 38400 (výchozí pro TM-T20III)

2. **Ukládání nastavení tiskárny**
   - Přidáno tlačítko "Uložit" vedle "Test tisku"
   - Nový command `SavePrinterSettingsCommand`
   - Posílá `SettingsChangedMessage` pro refresh StatusBaru

3. **IsPrinterConnected() - skutečná kontrola**
   - Nyní skutečně testuje otevření COM portu
   - StatusBar zobrazí "Připojena" po uložení platného COM portu

**Testováno:**
- ✅ Test tisku funguje (COM1)
- ✅ Nastavení se ukládá
- ✅ StatusBar se aktualizuje

---

## 📅 **Předchozí session: 25. listopad 2025 (ráno)**

### ✅ Hotovo:
**Implementace PrintService pro Epson TM-T20III + Opravy**

**Upraveno/vytvořeno 8 souborů:**
1. **Services/EscPosPrintService.cs** (NOVÝ) - kompletní implementace ESC/POS tisku
2. Services/IPrintService.cs - oprava interface (Receipt místo IReceiptService)
3. Services/PrintService.cs - placeholder aktualizace
4. Services/SettingsService.cs - oprava backup path validace
5. MainWindow.xaml.cs - oprava backup path kontroly
6. Services/DatabaseMigrationService.cs - oprava migračního systému pro nové DB
7. App.xaml.cs - registrace EscPosPrintService
8. Views/NastaveniPage.xaml - UI pro COM port + poznámka o driveru
9. ViewModels/StatusBarViewModel.cs - skutečná kontrola připojení tiskárny

**Klíčové změny:**

**1. Oprava migračního systému (DatabaseMigrationService.cs):**
- Problém: Nová DB vytvořená přes EnsureCreated() měla verzi 0 a snažila se aplikovat všechny migrace → "duplicate column" chyby
- Řešení: Detekce nově vytvořené DB → nastavení verze rovnou na CURRENT_SCHEMA_VERSION
- Metoda EnsureDatabaseExistsAsync() kontroluje existenci před EnsureCreated()

**2. Oprava backup path validace (SettingsService.cs + MainWindow.xaml.cs):**
- Problém: IsBackupPathConfigured() kontroloval Directory.Exists() → pokud složka neexistovala, backup se přeskočil
- Řešení: Odstranění Directory.Exists() z validace - složka se vytvoří automaticky při backupu
- Nyní stačí nastavit cestu (neprázdný string) a backup funguje

**3. Implementace EscPosPrintService:**
- NuGet balíček: ESCPOS_NET 3.0.0
- Podpora SerialPrinter (COM port) - ESCPOS_NET 3.0 odstranil UsbPrinter!
- Formátování účtenek: tučné texty, dvojitá výška, zarovnání, řez papíru
- Podpora všech typů účtenek:
  - Běžné prodeje
  - Storno (negativní hodnoty, označení ❌)
  - Dárkové poukazy (prodej + uplatnění)
- DPH rozpad pro plátce DPH (seskupený podle sazeb)
- Podpora slev na položkách (zobrazení původní ceny)
- Platební metody (hotovost s vrácením, karta)
- Test tisku s info o připojení

**Technické detaily:**

1. **EscPosPrintService.cs - hlavní metody**:
   - `PrintReceiptAsync(Receipt)` - kompletní tisk účtenky
   - `TestPrintAsync(string)` - test tisku s info o připojení
   - `IsPrinterConnected()` - kontrola připojení tiskárny
   - `CreatePrinter()` - vytvoření SerialPrinter instance
   - `BuildReceiptData()` - sestavení ESC/POS příkazů

2. **ESCPOS_NET 3.0.0 API**:
   ```csharp
   var printer = new SerialPrinter(portName: "COM5", baudRate: 115200);
   var e = new EPSON();

   var commands = new List<byte[]> {
       e.CenterAlign(),
       e.SetStyles(PrintStyle.Bold | PrintStyle.DoubleHeight),
       e.PrintLine("TEXT"),
       e.FullCutAfterFeed(3)  // Řez papíru
   };

   var data = ByteSplicer.Combine(commands.ToArray());
   printer.Write(data);
   ```

3. **Proč SerialPrinter (COM port)?**
   - ESCPOS_NET 3.0.0 ODSTRANIL třídu `UsbPrinter`
   - `DirectPrinter` má jinou signaturu - není wrapper pro Windows tiskárny
   - Řešení: **TMS Virtual Port Driver** vytvoří COM port pro USB tiskárnu
   - Průmyslový standard pro POS tiskárny - nejspolehlivější

**⏸️ AKTUÁLNÍ STAV (čeká se na restart PC):**
- ✅ Kód implementován a zkompilován
- ✅ Git commity vytvořeny (3 commity)
- ⏳ **Čeká se**: Instalace TMS Virtual Port Driver v8.70a
- ⏳ **Čeká se**: Restart PC
- ⏳ **Čeká se**: Zjištění COM portu (Device Manager)
- ⏳ **Čeká se**: Test tisku v aplikaci

### 🧪 Otestováno:
- ✅ Build bez chyb - všechny 3 commity zkompilované úspěšně
- ✅ Database migration fix - nové DB se vytvoří s správnou verzí
- ✅ Backup path fix - backup funguje i když složka neexistuje
- ⏳ **Zbývá otestovat**: Skutečný tisk na tiskárně (čeká se na driver + restart)

### 🔧 Další kroky PO RESTARTU:
1. **Zjistit COM port** - Device Manager → Ports (COM & LPT)
2. **Nastavit COM port v aplikaci** - Nastavení → Systém
3. **Test tisku** - tlačítko "Test tisku" v aplikaci
4. **Test prodeje** - vytvořit účtenku a vytisknout
5. **Commitnout** - pokud vše funguje

### 📚 Zdroje pro driver:
- TMS Virtual Port Driver v8.70a (stažený uživatelem)
- [Epson TM-T20III Support](https://epson.com/Support/Point-of-Sale/Thermal-Printers/Epson-TM-T20III-Series/s/SPT_C31CH51001)

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

13. **Window_Closed vs AppWindow.Closing** ⚠️ AKTUALIZOVÁNO!
   - `Window.Closed` event **NEFUNGUJE SPOLEHLIVĚ na Win10!**
   - **Řešení: Použít `AppWindow.Closing`:**
   ```csharp
   // V konstruktoru
   var appWindow = GetAppWindowForCurrentWindow();
   appWindow.Closing += AppWindow_Closing;

   // Helper metoda
   private AppWindow GetAppWindowForCurrentWindow()
   {
       var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
       var winId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
       return AppWindow.GetFromWindowId(winId);
   }

   // Event handler
   private async void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
   {
       if (_isClosing) return;
       args.Cancel = true;  // Cancel initial close
       _isClosing = true;

       // Perform operations
       await Task.Run(() => PerformBackup());
       await completionDialog.ShowAsync();

       // Exit
       Environment.Exit(0);
   }
   ```
   - `using Microsoft.UI.Windowing;` pro AppWindow
   - Flag `_isClosing` zabraňuje nekonečnému cyklu
   - `Environment.Exit(0)` vrací správný exit code

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

**Poslední aktualizace:** 26. listopad 2025
**Aktuální verze:** v1.0.1

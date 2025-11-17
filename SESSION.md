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

### Databáze (EF Core + SQLite)

1. **Žádné migrace!**
   - Při změně schématu: Smazat `%LocalAppData%\Sklad_2_Data\sklad.db`
   - Používá se `Database.EnsureCreated()` místo migrací

2. **DbContextFactory pattern**
   - Registrace: `services.AddDbContextFactory<DatabaseContext>()`
   - Workaround pro WinUI TwoWay binding issues

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
- Storno pokračuje v číselné řadě (2025/0007 → ❌2025/0008)
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

1. **UI vylepšení pro Neplátce DPH**
   - Skrýt panel "Sazby DPH" když `IsVatPayer = false`
   - Zjednodušit formulář nového produktu (nevyžadovat DPH)
   - Skrýt DPH informace v statistikách a přehledech
   - Dynamické zobrazení/skrytí podle `IsVatPayer`
   - Testovat přepínání Plátce/Neplátce

2. **Systém uživatelských účtů**
   - Implementovat databázovou tabulku Users
   - Nahradit fixed roles (Admin/Prodej) skutečnými uživateli
   - Každý prodavač vlastní login + jméno
   - Role/oprávnění per uživatel
   - SellerName bude skutečné jméno místo "Prodej"

### ⏳ Sekundární:
- Export uzavírek do CSV/PDF
- Skutečný PrintService (tisk na běžnou tiskárnu)
- Respektovat "Plátce DPH" v tisku
- Scanner integrace
- Vylepšit error handling (lokalizované hlášky)

---

## 📊 Aktuální stav projektu

**Hotovo:** 9/14 hlavních funkcí (~64%)

### ✅ Implementováno:
1. Role-based UI restrictions
2. Databáze produktů - vylepšení (filtrování, řazení)
3. Status Bar (Informační panel)
4. Dashboard prodejů (KPI, top/worst produkty, platby)
5. Denní otevírka/uzavírka pokladny
6. DPH systém (konfigurace)
7. Historie pokladny s filtry
8. Dynamická správa kategorií
9. **PPD Compliance** (profesionální účtenky, storno, export FÚ)

### ⏳ Zbývá:
1. UI optimalizace pro neplátce DPH
2. Systém uživatelských účtů
3. Export uzavírek (CSV/PDF)
4. Tisk (PrintService je placeholder)
5. Scanner integrace

---

**Poslední aktualizace:** 17. listopad 2025

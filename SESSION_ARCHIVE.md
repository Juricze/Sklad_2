# Session Archive - Detailní logy z října 2025

Tento soubor obsahuje archivované detailní session logy. Pro aktuální informace viz `SESSION.md`.

---

# Session Log - Denní uzavírka/otevírka pokladny

**Datum:** 11. říjen 2025
**Trvání:** ~3 hodiny
**Status:** ✅ HOTOVO

## 🎯 Zadání

Implementovat kompletní workflow denní otevírky a uzavírky pokladny pro roli "Prodej":

1. **Zahájení nového dne** při prvním přihlášení nebo novém dni
2. **Ochrana proti změně systémového času** (posun času zpět)
3. **Uzavírka dne** s kontrolou rozdílu a ochranou proti opakování
4. Validace všech částek (0-10M Kč, ne záporné)

## 📋 Implementované změny

### 1. Modely a databáze

#### `Models/EntryType.cs`
- ❌ Odstraněn: `InitialDeposit` (matoucí název)
- ✅ Přidán: `DayStart` - zahájení dne (nastaví hodnotu)
- ✅ Přidán: `DayClose` - uzavírka dne (nastaví hodnotu)

#### `Models/Settings/AppSettings.cs`
```csharp
public DateTime? LastSaleLoginDate { get; set; }  // Existující
public DateTime? LastDayCloseDate { get; set; }   // NOVÉ
```

### 2. Services

#### `Services/ICashRegisterService.cs`
```csharp
// Přejmenováno z InitializeTillAsync
Task SetDayStartCashAsync(decimal initialAmount);
Task<(bool Success, string ErrorMessage)> PerformDayCloseAsync(decimal actualAmount);
```

#### `Services/CashRegisterService.cs`
- **SetDayStartCashAsync()**: Vytvoří `DayStart` entry (nastaví hodnotu, nepřičítá!)
- **PerformDayCloseAsync()**:
  - Validace: 0-10M Kč, ne záporná
  - Kontrola `LastDayCloseDate` - pouze 1× denně
  - Výpočet rozdílu (přebytek/manko)
  - Vytvoří `DayClose` entry
  - Uloží `LastDayCloseDate`
- **RecordEntryAsync()**: Switch pro všechny EntryTypes
  - `DayStart`, `DayClose` → nastaví hodnotu
  - `Deposit`, `Sale` → přičte
  - `Withdrawal`, `DailyReconciliation`, `Return` → odečte

### 3. MainWindow - Nový den dialog

#### `MainWindow.xaml.cs` - `OnFirstActivated()`
```csharp
private bool _hasHandledNewDay = false;

private async void OnFirstActivated(object sender, WindowActivatedEventArgs args)
{
    if (_hasHandledNewDay || args.WindowActivationState == WindowActivationState.Deactivated)
        return;

    _hasHandledNewDay = true;
    this.Activated -= OnFirstActivated;

    if (IsSalesRole)
    {
        var currentDate = DateTime.Today;
        var lastLoginDate = _settingsService.CurrentSettings.LastSaleLoginDate?.Date;

        bool isNewDay = false;
        string promptMessage = "";

        // Kontrola nového dne
        if (lastLoginDate == null || currentDate > lastLoginDate)
        {
            isNewDay = true;
            promptMessage = "Vítejte v novém obchodním dni! ...";
        }
        else if (currentDate < lastLoginDate)  // OCHRANA ČASU
        {
            isNewDay = true;
            promptMessage = "⚠️ VAROVÁNÍ: Detekována změna systémového času!...";
        }

        if (isNewDay)
        {
            // Čeká na XamlRoot (robustní pro slabší stroje)
            int retries = 0;
            while (this.Content?.XamlRoot == null && retries < 20)
            {
                await Task.Delay(50);
                retries++;
            }

            var newDayDialog = new Views.Dialogs.NewDayConfirmationDialog();
            newDayDialog.SetPromptText(promptMessage);
            newDayDialog.XamlRoot = this.Content.XamlRoot;

            var result = await newDayDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                await cashRegisterService.SetDayStartCashAsync(newDayDialog.InitialAmount);
                _settingsService.CurrentSettings.LastSaleLoginDate = currentDate;
                await _settingsService.SaveSettingsAsync();
            }
            else
            {
                Application.Current.Exit();
            }
        }
    }
}
```

**Důležité změny:**
- ✅ Přesunuto z `LoginWindow` do `MainWindow` (lepší timing)
- ✅ Robustní čekání na XamlRoot místo pevných 500ms
- ✅ Detekce změny času (`currentDate < lastLoginDate`)
- ✅ Validace v dialogu (`NewDayConfirmationDialog`)

## 🐛 Problémy a řešení

### Problém 1: Dialog se zobrazuje před MainWindow
**Příznaky:** Dialog nového dne vyskočil okamžitě po přihlášení, main window nebylo vidět

**Příčina:** LoginWindow zobrazoval dialog před vytvořením MainWindow

**Řešení:** Přesun celé logiky nového dne z `LoginWindow` do `MainWindow.OnFirstActivated`

---

### Problém 2: Hodnota pokladny se neaktualizovala
**Příznaky:** Po zadání počáteční částky (např. 5000 Kč) se v Pokladně zobrazila stará hodnota (25 000 Kč)

**Příčina 1:** `InitializeTillAsync()` přičítala místo nastavení hodnoty
**Příčina 2:** `CashRegisterUpdatedMessage` poslaná PŘED vytvořením ViewModelu

**Řešení:**
1. ✅ Nový EntryType `DayStart` který **nastaví** hodnotu (ne přičítá)
2. ✅ Odstranění message systému z MainWindow
3. ✅ `Page.Loaded` event v `CashRegisterPage` - načte data při každém zobrazení
4. ✅ Přejmenování `InitializeTillAsync` → `SetDayStartCashAsync` (jasný název)

---

### Problém 3: ContentDialog COMException
**Příznaky:**
```
System.Runtime.InteropServices.COMException
An async operation was not properly started.
Only a single ContentDialog can be open at any time.
```

**Příčina:** WinUI bug - pokus o zobrazení dialogu když už je nějaký otevřený

**Řešení:**
```csharp
// 800ms delay + retry s 300ms
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

---

### Problém 4: Clean + Rebuild
**Příznaky:** Změny se neprojevují, aplikace běží se starým kódem

**Příčina:** WinUI/XAML projekty někdy cachují sestavení

**Řešení:** Vždy **Build → Clean Solution**, pak **Rebuild Solution**

---

**Konec session** 🎉

---

# Session Log - Dashboard prodejů & Vylepšení databáze

**Datum:** 11. říjen 2025 (pokračování)
**Trvání:** ~2 hodiny
**Status:** ✅ HOTOVO

## 🎯 Zadání

### Bod 1 & 2: Role-based UI restrictions ✅
- Skrýt panel "Denní kontrola pokladny" pro roli "Prodej"
- Zakázat tlačítko "Smazat vybrané" pro roli "Prodej" v Databázi produktů

### Bod 3: Databáze produktů - Vylepšení ✅
- Filtrování podle kategorie
- Řazení (klik na hlavičku sloupce: Název, Skladem, Cena)
- Přidat sloupec "Nákupní cena"
- Fix: EAN vyhledávání - přesný prefix match (StartsWith místo Contains)

### Bod 5: Dashboard prodejů ✅
Vytvořit futuristický dashboard s:
- KPI karty (celkové tržby, průměr, DPH, čistá tržba)
- Top 5 nejprodávanějších produktů
- Nejméně prodávané produkty (5)
- Statistiky způsobů platby
- Seznam posledních prodejů
- Časové filtry (Celkem/Dnešní/Týdenní/Měsíční/Vlastní)
- Auto-refresh při otevření stránky

## 🐛 Problémy a řešení

### Problém 1: LiveCharts Runtime Crash
**Příznaky:** Aplikace spadla s code 0xffffffff při otevření Přehled Prodejů

**Pokusy o opravu:**
1. ❌ Změna mapping signature
2. ❌ Změna typu os
3. ❌ ObservableCollection approach
4. ❌ Zjednodušený `LineSeries<double>` bez custom mapping

**Rozhodnutí uživatele:** "Tak to udelej bhez grafů no... to je teda nemilé ale asi to přežiju"

**Finální řešení:** Nahrazení grafu 3 velkými stat kartami (📅 Denní průměr, 📄 Počet účtenek, 💰 DPH Info)

---

### Problém 2: EAN Search Too Broad
**Příznaky:** Vyhledávání "2" našlo EAN "123" i "1234"

**Řešení:** Změna z `Contains()` na `StartsWith()` pro EAN i Name

---

**Konec session** 🎉

---

# Session Log - ToggleButtonStyle Fix & Nastavení UI

**Datum:** 12. říjen 2025
**Trvání:** ~2 hodiny
**Status:** ✅ HOTOVO

## 🎯 Zadání

### Oprava filtrovacích tlačítek (RadioButton s ToggleButtonStyle)
**Problém:** Filtrovací tlačítka (denní/týdenní/měsíční) měla několik závažných chyb:
1. Po kliknutí se tlačítka nezvýrazňovala vůbec
2. Když se zvýraznila, hover efekt způsoboval ztrátu zvýraznění
3. Kliknutí na již kliknuté tlačítko způsobilo bílé pozadí + bílý text (nečitelné)

## 📋 Implementované změny

### ToggleButtonStyle - Kompletní přepracování

**Finální řešení:** Použití separátního HoverBorder overlay pro hover efekt

**Klíčové změny:**
1. **Přidán separátní HoverBorder** - průhledný overlay (Opacity=0) nad ContentBorder
2. **PointerOver stav** - nastaví HoverBorder.Opacity na 1 (zobrazí hover efekt)
3. **Checked stav** - nastaví:
   - ContentBorder.Background na AccentFillColorDefaultBrush (modrá)
   - ContentPresenter.Foreground na TextOnAccentFillColorPrimaryBrush (bílá)
   - HoverBorder.Opacity na 0 (vypne hover efekt)
4. **Pressed stav** - POUZE skrývá HoverBorder, **NEMĚNÍ background ContentBorderu**
   - Tím zůstane checked tlačítko modré i při kliknutí

## ✅ Výsledné chování

**Po všech opravách:**
- ✅ **Nekliknuté + hover** = světlejší pozadí
- ✅ **Kliknuté** = modrá barva, bílý text
- ✅ **Kliknuté + hover** = světlejší efekt
- ✅ **Kliknuté + hover off** = zpátky modrá barva
- ✅ **Kliknutí na kliknuté** = zůstává modrá (OPRAVENO)

**Uživatel potvrdil:** "Dobrý fajn takhle mi to stačí."

---

**Konec session** 🎉

---

# Session Log - PPD Compliance & Professional Storno System

**Datum:** 30. říjen 2025
**Trvání:** ~4 hodiny
**Status:** ✅ HOTOVO

## 🎯 Zadání

### 1. Oprava navigace a x:Bind problémů
- Opravit problém s navigací: "DATABAZE" klikatelná, ale nemá být
- Problém: Po startu tlačítko MINUS nefunguje, až po opětovném kliknutí na menu PRODEJ

### 2. Oprava ukládání hesla pro roli "Prodej"
- Heslo se neukládalo kvůli TwoWay binding problémům s PasswordBox

### 3. Storno prodeje
- Implementovat "Zrušit poslední prodej" přímo na stránce Prodej
- Vrátit produkty do skladu, částku z pokladny, vytvořit storno účtenku

### 4. PPD Compliance (Primární pokladní doklad)
- Přidat identifikaci prodavače do Receipt modelu
- Upravit čísla účtenek na profesionální formát: **2025/0001**
- Implementovat **profesionální storno systém** (místo mazání)
- Přidat **export do HTML/PDF** pro Finanční úřad

## 📋 Implementované změny

### 1. Identifikace prodavače (SellerName)

**Models/Receipt.cs:**
```csharp
[ObservableProperty]
private string sellerName;  // "Admin" or "Prodej"
```

### 2. Formátované čísla účtenek (2025/0001)

**Models/Receipt.cs:**
```csharp
[ObservableProperty]
private int receiptYear;  // 2025

[ObservableProperty]
private int receiptSequence;  // 1, 2, 3...

public string FormattedReceiptNumber => $"{ReceiptYear}/{ReceiptSequence:D4}";  // 2025/0001
```

**Výsledek:**
- 2025/0001, 2025/0002, ...
- 2026/0001 (nový rok = reset)

### 3. Profesionální storno systém

**Původní:** Mazání účtenky ❌ NELEGÁLNÍ
**Nový:** Vytvoření storno účtenky s **negativními hodnotami** ✅ LEGÁLNÍ

**Models/Receipt.cs:**
```csharp
[ObservableProperty]
private bool isStorno;

[ObservableProperty]
private int? originalReceiptId;  // Odkaz na původní účtenku
```

**Příklad:**
```
Účtenka č. 2025/0007  - 500 Kč     (normální)
❌ Účtenka č. 2025/0008  - -500 Kč  (storno č. 7)
Účtenka č. 2025/0009  - 350 Kč     (nový prodej)
```

**UI (UctenkyPage):**
- Červená ikona ❌ + červená částka
- Warning banner: "STORNO ÚČTENKA - stornuje č. 2025/0007"

### 4. Export do HTML/PDF pro FÚ

**Umístění:** Nastavení → Systém → "Export pro Finanční úřad"

**Features:**
- Výběr datového rozsahu (Od/Do)
- Generování HTML tabulky se všemi účtenkami
- Informace o firmě (IČ, DIČ, plátce DPH)
- Souhrn za období (počet, celkem, DPH)
- Automatické otevření v prohlížeči
- Uložení do `Documents/Sklad_2_Exports/`
- Možnost vytisknout (Ctrl+P) nebo uložit jako PDF

**HTML tabulka obsahuje:**
- Číslo účtenky (formát 2025/0001)
- Datum a čas
- Prodavač
- Způsob platby
- Celkem / Základ / DPH
- Storno účtenky červeně

### 5. Opravy navigace a x:Bind

**Problém:** 8 stránek mělo ViewModel inicializovaný **PO** `InitializeComponent()` → x:Bind nefungovalo správně

**Opravené stránky:**
1. ProdejPage
2. NastaveniPage
3. CashRegisterHistoryPage
4. NovyProduktPage
5. PrijemZboziPage
6. UctenkyPage
7. VratkyPage
8. VratkyPrehledPage

**Řešení:**
```csharp
public ProdejPage()
{
    // IMPORTANT: ViewModel must be set BEFORE InitializeComponent()
    ViewModel = (Application.Current as App).Services.GetRequiredService<ProdejViewModel>();

    this.InitializeComponent();  // x:Bind nyní funguje správně
}
```

## 🐛 Problémy a řešení

### Problém 1: MINUS tlačítko nefungovalo po startu
**4 příčiny:**
1. Hardcoded ProdejPage v Frame XAML
2. ViewModel po InitializeComponent()
3. Quantity změna neaktualizovala CanExecute
4. Page načtena příliš brzy

**Řešení:** Odstranění hardcoded page, ViewModel před Init, PropertyChanged listener, Frame.Loaded event

---

### Problém 2: PasswordBox binding
**Příčina:** WinUI security - Password property je write-only

**Řešení:** Event handlers místo x:Bind

---

### Problém 3: Databáze chyba
**Příčina:** Nové sloupce v Receipt (SellerName, ReceiptYear, etc.), ale stará databáze

**Řešení:** Smazat `%LocalAppData%\Sklad_2_Data\sklad.db` (žádné migrace podle projektu)

---

## ✅ PPD Compliance Status

**✅ Máme:**
- Číslo účtenky (2025/0001)
- Datum a čas
- Položky produktů (název, množství, cena, DPH)
- Celková částka, DPH rozpad
- Způsob platby
- **Identifikace prodavače**
- Údaje o firmě (IČ, DIČ)
- **Profesionální storno** (negativní hodnoty)
- **Export do HTML/PDF**

**⏳ Chybí:**
- Systém uživatelských účtů (zatím jen role Admin/Prodej)
- Skutečný tisk účtenek (PrintService je placeholder)
- Fiskální tiskárna (volitelné)

---

**Konec session** 🎉

---

# Session Log - TODO Update & FÚ Requirements Clarification

**Datum:** 31. říjen 2025
**Trvání:** ~15 minut
**Status:** ✅ HOTOVO

## 🎯 Zadání

Pokračování z předchozí session - dokončit aktualizaci TODO listu a objasnit požadavky FÚ (Finanční úřad) pro neplátce DPH.

## 💬 Diskuze - Požadavky FÚ pro neplátce DPH

### Otázka 1: Faktury od dodavatelů
**Uživatel:** "Nestačí si prostě jen uchovat papírovou formu Faktury od dodavatele?"

**Odpověď:** ✅ **Ano, úplně stačí!**
- Papírové faktury v archivaci (šanony podle měsíců/let)
- FÚ akceptuje papírovou formu
- Doporučení: Vést si evidenci nákupů (třeba sešit) pro kontrolu ziskovosti a inventury
- **Není nutné mít v aplikaci** (ale může pomoct)

### Otázka 2: Inventury
**Uživatel:** "Inventury - to se mi zdá také na papír ne?"

**Odpověď:** ✅ **Ano, v pořádku!**
- Inventura na papír je platná
- Projít sklad, spočítat kusy, zapsat
- FÚ to akceptuje (hlavně správné datum + podpis)
- Pak upravit stavy v aplikaci (DatabazePage)
- Pro malý obchod/sklad naprosto dostačující

### Shrnutí - Co má uživatel hotové

**✅ V aplikaci:**
- Prodeje (účtenky s DPH rozpadem)
- Pokladna (denní otevírka/uzavírka)
- Profesionální storno systém
- Export pro FÚ (HTML/PDF)
- Evidence produktů (sklad)

**✅ Papírově:**
- Faktury od dodavatelů
- Inventury

**Závěr:** Pro neplátce DPH je to **úplně dostačující setup**! 👍

---

**Konec session** 🎉

# Session Log - Denní uzavírka/otevírka pokladny

**Datum:** 11. říjen 2025
**Trvání:** ~3 hodiny
**Status:** ✅ HOTOVO

---

## 🎯 Zadání

Implementovat kompletní workflow denní otevírky a uzavírky pokladny pro roli "Prodej":

1. **Zahájení nového dne** při prvním přihlášení nebo novém dni
2. **Ochrana proti změně systémového času** (posun času zpět)
3. **Uzavírka dne** s kontrolou rozdílu a ochranou proti opakování
4. Validace všech částek (0-10M Kč, ne záporné)

---

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

### 4. CashRegisterPage - Uzavírka dne

#### `Views/CashRegisterPage.xaml`
```xaml
<!-- Uzavírka dne -->
<Border Style="{StaticResource CardBorderStyle}">
    <StackPanel Spacing="12">
        <TextBlock Text="Uzavírka dne" Style="{ThemeResource SubtitleTextBlockStyle}"/>
        <TextBlock Text="Uzavírka dne uzavře obchodní den..." TextWrapping="Wrap"/>
        <TextBox Header="Skutečná hotovost v pokladně"
                 Text="{x:Bind ViewModel.DayCloseAmount, Mode=TwoWay, Converter={StaticResource InlineDecimalConverter}}"/>
        <TextBlock Text="{x:Bind ViewModel.DayCloseStatusMessage, Mode=OneWay}"
                   Foreground="{ThemeResource SystemErrorTextColor}"
                   Visibility="{x:Bind ViewModel.IsDayCloseError, Mode=OneWay}"/>
        <Button Content="Uzavřít den" Command="{x:Bind ViewModel.PerformDayCloseCommand}"/>
    </StackPanel>
</Border>
```

#### `ViewModels/CashRegisterViewModel.cs`
```csharp
public event EventHandler<string> DayCloseSucceeded;

[ObservableProperty]
private decimal dayCloseAmount;

[ObservableProperty]
private string dayCloseStatusMessage;

[ObservableProperty]
[NotifyPropertyChangedFor(nameof(IsDayCloseError))]
private bool isDayCloseErrorVisible;

public bool IsDayCloseError => IsDayCloseErrorVisible;

[RelayCommand]
private async Task PerformDayCloseAsync()
{
    IsDayCloseErrorVisible = false;
    DayCloseStatusMessage = string.Empty;

    var (success, errorMessage) = await _cashRegisterService.PerformDayCloseAsync(DayCloseAmount);

    if (success)
    {
        DayCloseSucceeded?.Invoke(this, $"Den byl úspěšně uzavřen. Stav pokladny: {DayCloseAmount:C}");
        DayCloseAmount = 0;
        await LoadCashRegisterDataAsync();
    }
    else
    {
        DayCloseStatusMessage = errorMessage;
        IsDayCloseErrorVisible = true;
    }
}
```

#### `Views/CashRegisterPage.xaml.cs`
```csharp
private void HandleDayCloseSucceeded(object sender, string message)
{
    // WinUI bug workaround - pouze 1 dialog najednou
    this.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, async () =>
    {
        await Task.Delay(800);  // Čeká na zavření jiného dialogu

        var dialog = new ContentDialog
        {
            Title = "Uzavírka dne provedena",
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = this.XamlRoot
        };

        try
        {
            await dialog.ShowAsync();
        }
        catch (COMException)
        {
            // Retry po 300ms
            await Task.Delay(300);
            try { await dialog.ShowAsync(); }
            catch { /* Tiché selhání */ }
        }
    });
}

// Page.Loaded - načte data při každém zobrazení
this.Loaded += (s, e) =>
{
    ViewModel.LoadCashRegisterDataCommand.Execute(null);
};
```

### 5. Convertery a UI

#### `Converters/EntryTypeToStringConverter.cs`
```csharp
case EntryType.DayStart:
    return "Zahájení dne";
case EntryType.DayClose:
    return "Uzavření dne";
case EntryType.DailyReconciliation:
    return "Denní kontrola";
case EntryType.Return:
    return "Vratka";
// ... atd.
```

### 6. Build konfigurace

#### `Sklad_2.csproj`
```xml
<PropertyGroup>
    ...
    <NoWarn>$(NoWarn);NETSDK1206</NoWarn>
</PropertyGroup>
```
Potlačeno varování o version-specific RID pro WindowsAppSDK.

---

## 🐛 Problémy a řešení

### Problém 1: Dialog se zobrazuje před MainWindow
**Příznaky:** Dialog nového dne vyskočil okamžitě po přihlášení, main window nebylo vidět

**Příčina:** LoginWindow zobrazoval dialog před vytvořením MainWindow

**Řešení:** Přesun celé logiky nového dne z `LoginWindow` do `MainWindow.OnFirstActivated`

---

### Problém 2: Hodnota pokladny se neaktualizovala
**Příznaky:** Po zadání počáteční částky (např. 5000 Kč) se v Pokladně zobrazila stará hodnota (25 000 Kč)

**Debug výstup:**
```
MainWindow: Till initialized with 5 630,00 Kč
...
CashRegisterViewModel: LoadCashRegisterDataAsync completed. CurrentCashInTill = 25 000,00 Kč
```

**Příčina 1:** `InitializeTillAsync()` přičítala místo nastavení hodnoty
```csharp
// ŠPATNĚ (starý kód)
case EntryType.InitialDeposit:
case EntryType.Deposit:
    newCashInTill += amount;  // PŘIČÍTÁ!
```

**Příčina 2:** `CashRegisterUpdatedMessage` poslaná PŘED vytvořením ViewModelu
```
MainWindow: Sending CashRegisterUpdatedMessage
CashRegisterViewModel: Initial IsSalesRole = True  // ← ViewModel teprve vytvořen!
```

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

### Problém 5: Pauzy na slabších strojích
**Otázka uživatele:** "Nebude problém s pevnými pauzy (500ms) na slabších strojích?"

**Řešení:**
```csharp
// PŘED: Pevných 500ms
await Task.Delay(500);

// PO: Čeká na XamlRoot (max 20×50ms = 1000ms)
int retries = 0;
while (this.Content?.XamlRoot == null && retries < 20)
{
    await Task.Delay(50);
    retries++;
}
```
Rychlé stroje projdou okamžitě, pomalé počkají až 1 sekundu.

---

## ✅ Testování

Všechny testy prošly úspěšně:

### Test 1: Zahájení nového dne ✅
- Dialog se zobrazí po přihlášení
- Validace: záporná částka → chyba
- Validace: > 10M → chyba
- Zadání 6000 Kč → pokladna ukazuje 6000 Kč

### Test 2: Prodej s aktualizací ✅
- Prodej za 321 Kč
- Pokladna: 6000 + 321 = 6321 Kč ✅

### Test 3: Vklad ✅
- Vklad 1000 Kč
- Dialog potvrzení
- Pokladna se aktualizuje

### Test 4: Denní kontrola ✅
- Zadání skutečné částky -50 Kč
- Vytvoří záznam rozdílu

### Test 5: Uzavírka dne ✅
- Dialog "Uzavírka dne provedena" se zobrazí
- Pokladna nastavena na zadanou částku
- Rozdíl vypočítán (přebytek/manko)

### Test 6: Ochrana proti opakování ✅
- Druhý pokus o uzavírku:
  ```
  "Denní uzavírka již byla provedena dne 11.10.2025.
   Uzavírku lze provést pouze jednou denně."
  ```

---

## 📊 Statistiky

- **Soubory změněny:** 10
- **Řádky kódu přidáno:** ~350
- **Řádky kódu odebráno:** ~50
- **Nové třídy/metody:** 2 (SetDayStartCashAsync, PerformDayCloseAsync)
- **Nové EntryTypes:** 2 (DayStart, DayClose)
- **Debug sessions:** 6
- **Rebuildy:** 8+

---

## 🎓 Naučené lekce

1. **Clean + Rebuild je kritický** pro WinUI projekty
2. **Timing je důležitý** - ViewModel musí existovat před posláním message
3. **Page.Loaded event** je spolehlivější než message pro reload dat
4. **Robustní čekání** (while loop s retry) je lepší než pevné delay
5. **ContentDialog bug** v WinUI vyžaduje delay + try-catch
6. **Názvosloví je důležité** - `SetDayStartCash` vs `InitializeTill`
7. **Separace zodpovědností** - Login ≠ Business logika

---

## 📝 TODO pro příště

- [ ] Implementovat Historie pokladny s filtry (denní/týdenní/měsíční)
- [ ] Přidat export uzavírek do CSV/PDF
- [ ] Implementovat úpravu kategorií přes UI (zatím hard-coded)
- [ ] Respektovat "Plátce DPH" přepínač v účtenkách
- [ ] Vylepšit error handling (lokalizované chybové hlášky)

---

**Konec session** 🎉

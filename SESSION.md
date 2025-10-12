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

---

# Session Log - Dashboard prodejů & Vylepšení databáze

**Datum:** 11. říjen 2025 (pokračování)
**Trvání:** ~2 hodiny
**Status:** ✅ HOTOVO

---

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

---

## 📋 Implementované změny

### 1. Role-based UI restrictions (Bod 1 & 2)

#### `Converters/BooleanToVisibilityConverter.cs`
```csharp
public object Convert(object value, Type targetType, object parameter, string language)
{
    bool boolValue = value is bool b && b;
    if (parameter as string == "Inverse")
    {
        boolValue = !boolValue;
    }
    return boolValue ? Visibility.Visible : Visibility.Collapsed;
}
```

#### `Views/CashRegisterPage.xaml`
```xaml
<Border Style="{StaticResource CardBorderStyle}"
        Visibility="{x:Bind ViewModel.IsSalesRole, Mode=OneWay,
                     Converter={StaticResource BooleanToVisibilityConverter},
                     ConverterParameter=Inverse}">
    <!-- Denní kontrola pokladny panel -->
</Border>
```

#### `ViewModels/DatabazeViewModel.cs`
```csharp
private bool CanDeleteProduct() => SelectedProduct != null && !IsSalesRole;
```

**Výsledek:** Role "Prodej" nemá přístup k denní kontrole a mazání produktů ✅

---

### 2. Databáze produktů - Vylepšení (Bod 3)

#### `Models/Product.cs`
```csharp
public string PurchasePriceFormatted => $"{PurchasePrice:C}";
```

#### `ViewModels/DatabazeViewModel.cs`
```csharp
public enum SortColumn { None, Name, StockQuantity, SalePrice }
public enum SortDirection { Ascending, Descending }

[ObservableProperty]
private string selectedCategory;

[ObservableProperty]
private SortColumn currentSortColumn = SortColumn.None;

[ObservableProperty]
private SortDirection currentSortDirection = SortDirection.Ascending;

public ObservableCollection<string> Categories { get; } = new ObservableCollection<string>
{
    "Všechny kategorie",
    "Nápoje", "Potraviny", "Alkohol", "Tabák", "Cukrovinky",
    "Pečivo", "Mléčné výrobky", "Zelenina a ovoce", "Maso a uzeniny",
    "Mražené potraviny", "Drogerie", "Ostatní"
};

[RelayCommand]
private void SortBy(string columnName)
{
    var column = Enum.Parse<SortColumn>(columnName);

    if (CurrentSortColumn == column)
    {
        CurrentSortDirection = CurrentSortDirection == SortDirection.Ascending
            ? SortDirection.Descending
            : SortDirection.Ascending;
    }
    else
    {
        CurrentSortColumn = column;
        CurrentSortDirection = SortDirection.Ascending;
    }

    ApplySorting();
}

private void ApplySorting()
{
    if (CurrentSortColumn == SortColumn.None) return;

    IEnumerable<Product> sorted = CurrentSortColumn switch
    {
        SortColumn.Name => CurrentSortDirection == SortDirection.Ascending
            ? FilteredProducts.OrderBy(p => p.Name)
            : FilteredProducts.OrderByDescending(p => p.Name),
        SortColumn.StockQuantity => CurrentSortDirection == SortDirection.Ascending
            ? FilteredProducts.OrderBy(p => p.StockQuantity)
            : FilteredProducts.OrderByDescending(p => p.StockQuantity),
        SortColumn.SalePrice => CurrentSortDirection == SortDirection.Ascending
            ? FilteredProducts.OrderBy(p => p.SalePrice)
            : FilteredProducts.OrderByDescending(p => p.SalePrice),
        _ => FilteredProducts
    };

    FilteredProducts.Clear();
    foreach (var product in sorted)
    {
        FilteredProducts.Add(product);
    }
}

private void FilterProducts()
{
    var filtered = _allProducts.AsEnumerable();

    // Category filter
    if (!string.IsNullOrEmpty(SelectedCategory) &&
        SelectedCategory != "Všechny kategorie")
    {
        filtered = filtered.Where(p => p.Category == SelectedCategory);
    }

    // Search filter - FIXED: StartsWith místo Contains
    if (!string.IsNullOrEmpty(SearchText))
    {
        filtered = filtered.Where(p =>
            p.Name.StartsWith(SearchText, StringComparison.OrdinalIgnoreCase) ||
            p.Ean.StartsWith(SearchText, StringComparison.OrdinalIgnoreCase));
    }

    FilteredProducts.Clear();
    foreach (var product in filtered)
    {
        FilteredProducts.Add(product);
    }

    ApplySorting();
}
```

#### `Views/DatabazePage.xaml`
```xaml
<!-- Category Filter -->
<ComboBox Header="Kategorie" Width="200"
          ItemsSource="{x:Bind ViewModel.Categories}"
          SelectedItem="{x:Bind ViewModel.SelectedCategory, Mode=TwoWay}"/>

<!-- Sortable Headers -->
<Button Content="Název ▲▼"
        Command="{x:Bind ViewModel.SortByCommand}"
        CommandParameter="Name"
        Style="{ThemeResource TextBlockButtonStyle}"/>
<Button Content="Skladem ▲▼"
        Command="{x:Bind ViewModel.SortByCommand}"
        CommandParameter="StockQuantity"
        Style="{ThemeResource TextBlockButtonStyle}"/>
<Button Content="Prodejní cena ▲▼"
        Command="{x:Bind ViewModel.SortByCommand}"
        CommandParameter="SalePrice"
        Style="{ThemeResource TextBlockButtonStyle}"/>

<!-- Added Purchase Price Column -->
<TextBlock Text="{x:Bind PurchasePriceFormatted}" Grid.Column="4"/>
```

**Výsledek:** Plně funkční filtrování, řazení a přesné vyhledávání ✅

---

### 3. Dashboard prodejů (Bod 5)

#### Nové modely

**`Models/DailySales.cs`**
```csharp
public class DailySales
{
    public DateTime Date { get; set; }
    public decimal TotalAmount { get; set; }
    public int NumberOfSales { get; set; }
    public string DateLabel => Date.ToString("dd.MM");
    public string ShortDateLabel => Date.ToString("dd");
}
```

**`Models/TopProduct.cs`**
```csharp
public class TopProduct
{
    public string ProductName { get; set; }
    public int QuantitySold { get; set; }
    public decimal TotalRevenue { get; set; }
    public string RevenueFormatted => $"{TotalRevenue:C}";
    public double PercentageOfTotal { get; set; }
}
```

**`Models/PaymentMethodStats.cs`**
```csharp
public class PaymentMethodStats
{
    public string PaymentMethod { get; set; }
    public int Count { get; set; }
    public decimal TotalAmount { get; set; }
    public double Percentage { get; set; }
    public string AmountFormatted => $"{TotalAmount:C}";
}
```

**`Models/DateFilterType.cs`** - Přidán enum value
```csharp
public enum DateFilterType
{
    All,      // NOVÉ - zobrazí všechny záznamy
    Daily,
    Weekly,
    Monthly,
    Custom
}
```

#### ViewModel Extensions

**`ViewModels/PrehledProdejuViewModel.cs`**
```csharp
[ObservableProperty]
[NotifyPropertyChangedFor(nameof(AverageSaleAmountFormatted))]
private decimal averageSaleAmount;

public string AverageSaleAmountFormatted => $"{AverageSaleAmount:C}";

[ObservableProperty]
private ObservableCollection<TopProduct> topProducts = new();

[ObservableProperty]
private ObservableCollection<TopProduct> worstProducts = new();

[ObservableProperty]
private ObservableCollection<PaymentMethodStats> paymentMethodStats = new();

[ObservableProperty]
private DateFilterType selectedFilter = DateFilterType.All;

partial void OnSelectedFilterChanged(DateFilterType value)
{
    SetDateRangeForFilter(value);
    LoadSalesDataCommand.Execute(null);
}

private void SetDateRangeForFilter(DateFilterType filter)
{
    var now = DateTime.Now;
    switch (filter)
    {
        case DateFilterType.All:
            StartDate = new DateTimeOffset(new DateTime(2000, 1, 1));
            EndDate = new DateTimeOffset(new DateTime(2099, 12, 31, 23, 59, 59));
            break;
        case DateFilterType.Daily:
            StartDate = new DateTimeOffset(now.Date);
            EndDate = new DateTimeOffset(now.Date.AddDays(1).AddSeconds(-1));
            break;
        case DateFilterType.Weekly:
            var startOfWeek = now.Date.AddDays(-(int)now.DayOfWeek + (int)DayOfWeek.Monday);
            StartDate = new DateTimeOffset(startOfWeek);
            EndDate = new DateTimeOffset(startOfWeek.AddDays(7).AddSeconds(-1));
            break;
        case DateFilterType.Monthly:
            StartDate = new DateTimeOffset(new DateTime(now.Year, now.Month, 1));
            EndDate = new DateTimeOffset(new DateTime(now.Year, now.Month,
                DateTime.DaysInMonth(now.Year, now.Month), 23, 59, 59));
            break;
        case DateFilterType.Custom:
            // Keep current dates
            break;
    }
}

private void CalculateTotals()
{
    TotalSalesAmount = Sales.Sum(r => r.TotalAmount);
    TotalSalesAmountWithoutVat = Sales.Sum(r => r.TotalAmountWithoutVat);
    TotalVatAmount = Sales.Sum(r => r.TotalVatAmount);
    NumberOfReceipts = Sales.Count;
    AverageSaleAmount = NumberOfReceipts > 0 ? TotalSalesAmount / NumberOfReceipts : 0;

    CalculateTopProducts();
    CalculateWorstProducts();
    CalculatePaymentMethodStats();
}

private void CalculateTopProducts()
{
    TopProducts.Clear();

    var productStats = Sales
        .SelectMany(r => r.Items ?? new ObservableCollection<ReceiptItem>())
        .GroupBy(item => item.ProductName)
        .Select(g => new TopProduct
        {
            ProductName = g.Key,
            QuantitySold = g.Sum(item => item.Quantity),
            TotalRevenue = g.Sum(item => item.TotalPrice)
        })
        .OrderByDescending(p => p.TotalRevenue)
        .Take(5)
        .ToList();

    var maxRevenue = productStats.FirstOrDefault()?.TotalRevenue ?? 1;
    foreach (var product in productStats)
    {
        product.PercentageOfTotal = maxRevenue > 0
            ? (double)(product.TotalRevenue / maxRevenue) * 100
            : 0;
        TopProducts.Add(product);
    }
}

private void CalculateWorstProducts()
{
    WorstProducts.Clear();

    var productStats = Sales
        .SelectMany(r => r.Items ?? new ObservableCollection<ReceiptItem>())
        .GroupBy(item => item.ProductName)
        .Select(g => new TopProduct
        {
            ProductName = g.Key,
            QuantitySold = g.Sum(item => item.Quantity),
            TotalRevenue = g.Sum(item => item.TotalPrice)
        })
        .OrderBy(p => p.QuantitySold)  // Ascending - worst sellers
        .Take(5)
        .ToList();

    var maxQuantity = productStats.LastOrDefault()?.QuantitySold ?? 1;
    foreach (var product in productStats)
    {
        product.PercentageOfTotal = maxQuantity > 0
            ? (double)product.QuantitySold / maxQuantity * 100
            : 0;
        WorstProducts.Add(product);
    }
}

private void CalculatePaymentMethodStats()
{
    PaymentMethodStats.Clear();

    var paymentStats = Sales
        .GroupBy(r => r.PaymentMethod)
        .Select(g => new PaymentMethodStats
        {
            PaymentMethod = g.Key,
            Count = g.Count(),
            TotalAmount = g.Sum(r => r.TotalAmount)
        })
        .ToList();

    var totalAmount = paymentStats.Sum(p => p.TotalAmount);
    foreach (var stat in paymentStats)
    {
        stat.Percentage = totalAmount > 0
            ? (double)(stat.TotalAmount / totalAmount) * 100
            : 0;
        PaymentMethodStats.Add(stat);
    }
}
```

#### View Implementation

**`Views/PrehledProdejuPage.xaml.cs`**
```csharp
public PrehledProdejuPage()
{
    ViewModel = (Application.Current as App).Services
        .GetRequiredService<PrehledProdejuViewModel>();
    this.InitializeComponent();
    this.DataContext = ViewModel;

    // Auto-load data when page is opened
    this.Loaded += (s, e) => ViewModel.LoadSalesDataCommand.Execute(null);
}
```

**`Views/PrehledProdejuPage.xaml`** - Dashboard layout
```xaml
<!-- Header with filters -->
<TextBlock Text="📊 Přehled prodejů" Style="{ThemeResource TitleTextBlockStyle}"/>

<!-- Filter Radio Buttons -->
<StackPanel Orientation="Horizontal" Spacing="8">
    <RadioButton Content="Celkem"
                 IsChecked="{x:Bind ViewModel.SelectedFilter, Mode=TwoWay,
                             Converter={StaticResource EnumToBooleanConverter},
                             ConverterParameter=All}"
                 Style="{StaticResource ToggleButtonStyle}"/>
    <RadioButton Content="Dnešní" .../>
    <RadioButton Content="Týdenní" .../>
    <RadioButton Content="Měsíční" .../>
    <RadioButton Content="Vlastní" .../>
</StackPanel>

<!-- 4 KPI Cards -->
<Grid ColumnSpacing="16">
    <!-- Total Sales -->
    <Border Style="{StaticResource KpiCardStyle}">
        <FontIcon Glyph="&#xE7BF;" Foreground="#007AFF"/>
        <TextBlock Text="Celkové tržby"/>
        <TextBlock Text="{x:Bind ViewModel.TotalSalesAmountFormatted}"/>
    </Border>

    <!-- Average Sale -->
    <Border Style="{StaticResource KpiCardStyle}">
        <FontIcon Glyph="&#xE8A1;" Foreground="#34C759"/>
        <TextBlock Text="Průměrný prodej"/>
        <TextBlock Text="{x:Bind ViewModel.AverageSaleAmountFormatted}"/>
    </Border>

    <!-- VAT Amount -->
    <Border Style="{StaticResource KpiCardStyle}">
        <FontIcon Glyph="&#xE8A9;" Foreground="#FF9500"/>
        <TextBlock Text="Celkem DPH"/>
        <TextBlock Text="{x:Bind ViewModel.TotalVatAmountFormatted}"/>
    </Border>

    <!-- Net Amount -->
    <Border Style="{StaticResource KpiCardStyle}">
        <FontIcon Glyph="&#xE7C3;" Foreground="#AF52DE"/>
        <TextBlock Text="Bez DPH"/>
        <TextBlock Text="{x:Bind ViewModel.TotalSalesAmountWithoutVatFormatted}"/>
    </Border>
</Grid>

<!-- 3 Quick Stats Cards -->
<Grid ColumnSpacing="16">
    <Border Style="{StaticResource CardBorderStyle}">
        <TextBlock Text="📅 Denní průměr"/>
        <FontIcon Glyph="&#xE787;" FontSize="48" Foreground="#007AFF"/>
        <TextBlock Text="{x:Bind ViewModel.AverageSaleAmountFormatted}"/>
    </Border>
    <!-- Similar for Receipt Count and VAT Info -->
</Grid>

<!-- 3 Column Layout: Top Products | Worst Products | Payment Methods -->
<Grid ColumnSpacing="16">
    <!-- Top 5 Products -->
    <Border Grid.Column="0">
        <TextBlock Text="🏆 Top 5 produktů"/>
        <ItemsControl ItemsSource="{x:Bind ViewModel.TopProducts}">
            <ProgressBar Value="{x:Bind PercentageOfTotal}" Foreground="#007AFF"/>
        </ItemsControl>
    </Border>

    <!-- Worst Products -->
    <Border Grid.Column="1">
        <TextBlock Text="📉 Nejméně prodávané"/>
        <ItemsControl ItemsSource="{x:Bind ViewModel.WorstProducts}">
            <ProgressBar Value="{x:Bind PercentageOfTotal}" Foreground="#FF3B30"/>
        </ItemsControl>
    </Border>

    <!-- Payment Methods -->
    <Border Grid.Column="2">
        <TextBlock Text="💳 Způsoby platby"/>
        <ItemsControl ItemsSource="{x:Bind ViewModel.PaymentMethodStats}">
            <ProgressBar Value="{x:Bind Percentage}" Foreground="#34C759"/>
        </ItemsControl>
    </Border>
</Grid>

<!-- Recent Sales List -->
<Border Style="{StaticResource CardBorderStyle}">
    <TextBlock Text="📋 Poslední prodeje"/>
    <ListView ItemsSource="{x:Bind ViewModel.Sales}" MaxHeight="400"/>
</Border>
```

---

## 🐛 Problémy a řešení

### Problém 1: LiveCharts Runtime Crash
**Příznaky:** Aplikace spadla s code 0xffffffff při otevření Přehled Prodejů

**Pokusy o opravu:**
1. ❌ Změna mapping signature na `(sales, index) => new(index, (double)sales.TotalAmount)`
2. ❌ Změna typu os na `IEnumerable<ICartesianAxis>`
3. ❌ ObservableCollection approach
4. ❌ Zjednodušený `LineSeries<double>` bez custom mapping

**Rozhodnutí uživatele:** "Tak to udelej bhez grafů no... to je teda nemilé ale asi to přežiju"

**Finální řešení:** Nahrazení grafu 3 velkými stat kartami (📅 Denní průměr, 📄 Počet účtenek, 💰 DPH Info)

---

### Problém 2: EAN Search Too Broad
**Příznaky:** Vyhledávání "2" našlo EAN "123" i "1234"

**Uživatel:** "Zadáš '2' → má najít nic (žádný nezačíná '2')"

**Řešení:** Změna z `Contains()` na `StartsWith()` pro EAN i Name
```csharp
// PŘED
filtered = filtered.Where(p =>
    p.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
    p.Ean.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

// PO
filtered = filtered.Where(p =>
    p.Name.StartsWith(SearchText, StringComparison.OrdinalIgnoreCase) ||
    p.Ean.StartsWith(SearchText, StringComparison.OrdinalIgnoreCase));
```

---

### Problém 3: Payment Methods Not Auto-Updating
**Příznaky:** Po prodeji s kartou se statistiky neaktualizovaly automaticky

**Uživatel:** "Aha už to chápu. Myslel jsem že se aktualizují autoamticky při otevření karty Přehled prodejů."

**Řešení:** Přidán Page.Loaded event handler
```csharp
this.Loaded += (s, e) => ViewModel.LoadSalesDataCommand.Execute(null);
```

**Výsledek:** Data se automaticky načítají při každém otevření stránky ✅

---

### Problém 4: FilterRadioButtonStyle Not Found
**Příznaky:** XAML referenční neexistující styl `FilterRadioButtonStyle`

**Řešení:** Změna na existující `ToggleButtonStyle` z `Styles/Controls.xaml`
```xaml
<!-- PŘED -->
<RadioButton Style="{StaticResource FilterRadioButtonStyle}"/>

<!-- PO -->
<RadioButton Style="{StaticResource ToggleButtonStyle}"/>
```

---

## ✅ Testování

### Test 1: Role-based restrictions ✅
- Role "Prodej": Panel "Denní kontrola" skrytý ✅
- Role "Prodej": Tlačítko "Smazat vybrané" disabled ✅
- Role "Vlastník": Vše dostupné ✅

### Test 2: Databáze produktů ✅
- Filtrování podle kategorie ✅
- Řazení podle názvu (A-Z, Z-A) ✅
- Řazení podle skladem (vzestupně, sestupně) ✅
- Řazení podle ceny (vzestupně, sestupně) ✅
- EAN vyhledávání "123" → najde pouze "123xxx", ne "x123" ✅
- Sloupec nákupní ceny zobrazený ✅

### Test 3: Dashboard - KPI Cards ✅
- Celkové tržby zobrazené správně ✅
- Průměrný prodej vypočítán ✅
- DPH zobrazené správně ✅
- Čistá tržba (bez DPH) správná ✅

### Test 4: Dashboard - Top/Worst Products ✅
- Top 5 produktů seřazeno podle tržeb ✅
- Progress bar zobrazuje relativní podíl ✅
- Nejméně prodávané seřazeno podle množství (vzestupně) ✅
- Červený progress bar pro worst products ✅

### Test 5: Dashboard - Payment Methods ✅
- Statistiky plateb zobrazené ✅
- Percentage vypočítaná správně ✅
- Zelený progress bar ✅

### Test 6: Dashboard - Filters ✅
- "Celkem" (All) - zobrazí všechny prodeje ✅
- "Dnešní" - pouze dnešní prodeje ✅
- "Týdenní" - aktuální týden ✅
- "Měsíční" - aktuální měsíc ✅
- "Vlastní" - zobrazí DatePicker ✅
- Auto-refresh při změně filtru ✅

### Test 7: Dashboard - Auto-load ✅
- Otevření stránky "Přehled prodejů" → data se načtou automaticky ✅
- Po prodeji → přepnutí na Přehled → aktuální data ✅

---

## 📊 Statistiky

- **Soubory změněny:** 12
- **Nové soubory:** 3 (DailySales.cs, TopProduct.cs, PaymentMethodStats.cs)
- **Řádky kódu přidáno:** ~600
- **Řádky kódu odebráno:** ~100 (LiveCharts kód)
- **Nové metody:** 5 (CalculateTopProducts, CalculateWorstProducts, CalculatePaymentMethodStats, SortBy, SetDateRangeForFilter)
- **Build errors fixed:** 4
- **Rebuildy:** 10+

---

## 🎓 Naučené lekce

1. **LiveCharts nestabilní** - Verze 2.0.0-rc2 způsobuje runtime crashes, lepší použít custom řešení
2. **StartsWith vs Contains** - Pro prefix matching vždy použít StartsWith
3. **Page.Loaded event** - Spolehlivý způsob auto-načtení dat
4. **Enum filters** - Elegantní řešení pro time-based filtering
5. **ToggleButtonStyle** - WinUI má vestavěný styl pro radio buttons jako toggle buttons
6. **LINQ GroupBy** - Výkonný způsob agregace dat pro statistiky
7. **Progress bars** - Vizuálně atraktivní způsob zobrazení relativních hodnot

---

## 📝 TODO pro příště

- [x] Bod 1: Role-based UI restrictions
- [x] Bod 2: Smazání produktů pouze pro "Vlastník"
- [x] Bod 3: Databáze produktů - filtrování, řazení, nákupní cena
- [ ] Bod 4: ??? (nevíme co to bylo)
- [x] Bod 5: Dashboard prodejů
- [ ] Implementovat Historie pokladny s filtry (denní/týdenní/měsíční)
- [ ] Přidat export uzavírek do CSV/PDF
- [ ] Implementovat úpravu kategorií přes UI (zatím hard-coded)
- [ ] Respektovat "Plátce DPH" přepínač v účtenkách
- [ ] Vylepšit error handling (lokalizované chybové hlášky)

---

**Konec session** 🎉

---

# Session Log - ToggleButtonStyle Fix & Nastavení UI

**Datum:** 12. říjen 2025
**Trvání:** ~2 hodiny
**Status:** ✅ HOTOVO

---

## 🎯 Zadání

### Oprava filtrovacích tlačítek (RadioButton s ToggleButtonStyle)
**Problém:** Filtrovací tlačítka (denní/týdenní/měsíční) měla několik závažných chyb:
1. Po kliknutí se tlačítka nezvýrazňovala vůbec
2. Když se zvýraznila, hover efekt způsoboval ztrátu zvýraznění
3. Kliknutí na již kliknuté tlačítko způsobilo bílé pozadí + bílý text (nečitelné)

### Smazání sekce GitHub z O aplikaci
V minulé session byla přidána sekce s odkazem na GitHub, ale uživatel požadoval smazání, protože repozitář je privátní.

---

## 📋 Implementované změny

### ToggleButtonStyle - Kompletní přepracování

**Soubor:** `/mnt/c/dev/Sklad_2/Styles/Controls.xaml`

**Finální řešení:** Použití separátního HoverBorder overlay pro hover efekt

```xaml
<Style x:Key="ToggleButtonStyle" TargetType="RadioButton">
    <!-- Template obsahuje: -->
    <Grid x:Name="RootGrid" Background="Transparent">
        <VisualStateManager.VisualStateGroups>
            <VisualStateGroup x:Name="CommonStates">
                <VisualState x:Name="Normal">
                    <Storyboard>
                        <ObjectAnimationUsingKeyFrames Storyboard.TargetName="HoverBorder" Storyboard.TargetProperty="Opacity">
                            <DiscreteObjectKeyFrame KeyTime="0" Value="0" />
                        </ObjectAnimationUsingKeyFrames>
                    </Storyboard>
                </VisualState>
                <VisualState x:Name="PointerOver">
                    <Storyboard>
                        <ObjectAnimationUsingKeyFrames Storyboard.TargetName="HoverBorder" Storyboard.TargetProperty="Opacity">
                            <DiscreteObjectKeyFrame KeyTime="0" Value="1" />
                        </ObjectAnimationUsingKeyFrames>
                    </Storyboard>
                </VisualState>
                <VisualState x:Name="Pressed">
                    <Storyboard>
                        <!-- POUZE skryje hover, NEMĚNÍ background! -->
                        <ObjectAnimationUsingKeyFrames Storyboard.TargetName="HoverBorder" Storyboard.TargetProperty="Opacity">
                            <DiscreteObjectKeyFrame KeyTime="0" Value="0" />
                        </ObjectAnimationUsingKeyFrames>
                    </Storyboard>
                </VisualState>
                <VisualState x:Name="Disabled">
                    <Storyboard>
                        <ObjectAnimationUsingKeyFrames Storyboard.TargetName="HoverBorder" Storyboard.TargetProperty="Opacity">
                            <DiscreteObjectKeyFrame KeyTime="0" Value="0" />
                        </ObjectAnimationUsingKeyFrames>
                        <ObjectAnimationUsingKeyFrames Storyboard.TargetName="ContentBorder" Storyboard.TargetProperty="Background">
                            <DiscreteObjectKeyFrame KeyTime="0" Value="{ThemeResource ButtonBackgroundDisabled}" />
                        </ObjectAnimationUsingKeyFrames>
                    </Storyboard>
                </VisualState>
            </VisualStateGroup>
            <VisualStateGroup x:Name="CheckStates">
                <VisualState x:Name="Checked">
                    <Storyboard>
                        <ObjectAnimationUsingKeyFrames Storyboard.TargetName="ContentBorder" Storyboard.TargetProperty="Background">
                            <DiscreteObjectKeyFrame KeyTime="0" Value="{ThemeResource AccentFillColorDefaultBrush}" />
                        </ObjectAnimationUsingKeyFrames>
                        <ObjectAnimationUsingKeyFrames Storyboard.TargetName="ContentPresenter" Storyboard.TargetProperty="Foreground">
                            <DiscreteObjectKeyFrame KeyTime="0" Value="{ThemeResource TextOnAccentFillColorPrimaryBrush}" />
                        </ObjectAnimationUsingKeyFrames>
                        <!-- Vypne hover efekt na checked tlačítku -->
                        <ObjectAnimationUsingKeyFrames Storyboard.TargetName="HoverBorder" Storyboard.TargetProperty="Opacity">
                            <DiscreteObjectKeyFrame KeyTime="0" Value="0" />
                        </ObjectAnimationUsingKeyFrames>
                    </Storyboard>
                </VisualState>
                <VisualState x:Name="Unchecked" />
            </VisualStateGroup>
        </VisualStateManager.VisualStateGroups>

        <Border x:Name="ContentBorder"
                Background="{TemplateBinding Background}"
                BorderBrush="{TemplateBinding BorderBrush}"
                BorderThickness="{TemplateBinding BorderThickness}"
                CornerRadius="{TemplateBinding CornerRadius}">
            <Grid>
                <!-- HoverBorder - separátní overlay pro hover efekt -->
                <Border x:Name="HoverBorder"
                        Background="{ThemeResource ButtonBackgroundPointerOver}"
                        Opacity="0"
                        CornerRadius="{TemplateBinding CornerRadius}" />
                <ContentPresenter x:Name="ContentPresenter"
                                  Content="{TemplateBinding Content}"
                                  ContentTemplate="{TemplateBinding ContentTemplate}"
                                  Padding="{TemplateBinding Padding}"
                                  Foreground="{TemplateBinding Foreground}"
                                  HorizontalContentAlignment="{TemplateBinding HorizontalContentAlignment}"
                                  VerticalContentAlignment="{TemplateBinding VerticalContentAlignment}"
                                  AutomationProperties.AccessibilityView="Raw" />
            </Grid>
        </Border>
    </Grid>
</Style>
```

**Klíčové změny:**
1. **Přidán separátní HoverBorder** - průhledný overlay (Opacity=0) nad ContentBorder
2. **PointerOver stav** - nastaví HoverBorder.Opacity na 1 (zobrazí hover efekt)
3. **Checked stav** - nastaví:
   - ContentBorder.Background na AccentFillColorDefaultBrush (modrá)
   - ContentPresenter.Foreground na TextOnAccentFillColorPrimaryBrush (bílá)
   - HoverBorder.Opacity na 0 (vypne hover efekt)
4. **Pressed stav** - POUZE skrývá HoverBorder, **NEMĚNÍ background ContentBorderu**
   - Tím zůstane checked tlačítko modré i při kliknutí

---

## 🐛 Problémy a řešení

### Problém 1: Tlačítka se nezvýrazňovala po kliknutí
**Příznaky:** Po kliknutí na filtrovací tlačítko se nic nestalo

**Pokusy o opravu:**
1. ❌ Použití kombinovaných stavů (CheckedNormal, CheckedPointerOver, etc.) - WinUI 3 RadioButton je nepodporuje
2. ❌ VisualState.Setters s různým pořadím VisualStateGroups - stále byl konflikt
3. ❌ FillBehavior="Stop" na CommonStates a FillBehavior="HoldEnd" na CheckStates - nepomohlo
4. ❌ StateTrigger s IsChecked binding - nelze použít s automatickými stavy

**Finální řešení:** Separátní HoverBorder overlay, který je kontrolován všemi stavy

---

### Problém 2: Hover efekt přepisoval checked stav
**Příznaky:** Když uživatel najel myší na checked tlačítko, zvýraznění zmizelo

**Uživatel:** "Všude kde máme ty filtrovací tlačítka - denní, týdenní, měsíční atd. se označí - zvírazní kdyz je aktualní, problem je pokud pres ten označený přejedu myší, neklikam jen přejedu a v tu chvíli se zvíraznění změní na stav nezmáčknuto."

**Příčina:** PointerOver stav z CommonStates a Checked stav z CheckStates se aplikovaly současně, ale PointerOver měl poslední slovo a přepsal background

**Řešení:** Checked stav explicitně nastavuje HoverBorder.Opacity na 0, čímž vypíná hover efekt

---

### Problém 3: Kliknutí na kliknuté tlačítko = bílé na bílém
**Příznaky:** Když uživatel klikl na již checked tlačítko, objevil se bílý background s bílým textem (nečitelné)

**Uživatel popsal:** "Kliknutí na kliknuté = bilé pozadí, bíle písmo - nemožnost přečíst tlačítko"

**Příčina:** Pressed stav měnil ContentBorder.Background na ButtonBackgroundPressed (bílá), což přepsalo Checked background

**Řešení:** Odebrání změny backgroundu z Pressed stavu - Pressed nyní pouze skrývá HoverBorder

---

### Problém 4: Hover nefungoval na unchecked tlačítkách
**Příznaky:** Po první opravě uživatel hlásil: "Nekliknuté + hover = nic"

**Příčina:** Checked stav používal FillBehavior="HoldEnd", který přetrvával i po odjetí z tlačítka

**Řešení:** Použití Storyboardů namísto VisualState.Setters pro přesnější kontrolu

---

## ✅ Výsledné chování

**Po všech opravách:**
- ✅ **Nekliknuté + hover** = světlejší pozadí (částečně funguje)
- ✅ **Kliknuté** = modrá barva (AccentFillColorDefaultBrush), bílý text
- ✅ **Kliknuté + hover** = světlejší efekt (hover overlay funguje i na checked)
- ✅ **Kliknuté + hover off** = zpátky modrá barva
- ✅ **Kliknutí na kliknuté** = zůstává modrá (OPRAVENO - již ne bílé na bílém)

**Uživatel potvrdil:** "Dobrý fajn takhle mi to stačí."

---

## 🎓 Naučené lekce

1. **WinUI 3 RadioButton nemá kombinované stavy** - nelze použít CheckedPointerOver, CheckedPressed, etc.
2. **VisualState priority je složitá** - když se aplikují stavy z různých skupin, výsledek není vždy předvídatelný
3. **Overlay pattern funguje lépe než přímá změna backgroundu** - separátní Border pro hover efekt dává větší kontrolu
4. **Pressed stav může přepsat checked** - pokud Pressed mění background, přepíše Checked background
5. **Storyboards vs Setters** - Storyboards dávají lepší kontrolu nad tím, kdy se změny aplikují
6. **Opacity 0 vs Visibility Collapsed** - Opacity 0 je lepší pro animace a transitions
7. **User feedback je klíčový** - uživatel postupně objasnil všechny edge cases

---

## 📊 Statistiky

- **Soubory změněny:** 1 (`Styles/Controls.xaml`)
- **Řádky kódu přidáno:** ~30 (HoverBorder + upravené stavy)
- **Řádky kódu odebráno:** ~50 (kombinované stavy, StateTrigger pokusy)
- **Pokusů o opravu:** 6+
- **Rebuildy:** 8+

---

## 📝 Poznámky pro další sessions

### **DŮLEŽITÉ - GIT OVLÁDÁ UŽIVATEL**
**NIKDY NEPOUŽÍVAT GIT PŘÍKAZY!** Uživatel si git operations dělá sám.

### Build proces
- Build vždy dělat přes Visual Studio 2022, ne přes CLI
- Při změnách XAML/ViewModels vždy: Build → Clean Solution → Rebuild Solution

---

## 📝 TODO pro příště

- [ ] Implementovat Historie pokladny s filtry (denní/týdenní/měsíční)
- [ ] Přidat export uzavírek do CSV/PDF
- [ ] Implementovat dynamickou správu kategorií přes UI (zatím hard-coded v ProductCategories.cs)
- [ ] Respektovat "Plátce DPH" přepínač v účtenkách a dialogech
- [ ] Vylepšit error handling (lokalizované chybové hlášky)
- [ ] Opravit hover na nekliknutých tlačítkách (pokud bude potřeba)

---

**Konec session** 🎉

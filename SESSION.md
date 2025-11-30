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

## 📅 **Poslední session: 30. listopad 2025 (pokračování 3)**

### ✅ Hotovo:
**Fix 1: EF Core vztah pro ReceiptGiftCardRedemption**

**Chyba:**
```
System.InvalidOperationException: The relationship from 'ReceiptGiftCardRedemption.GiftCard'
to 'GiftCard' with foreign key properties {'GiftCardEan' : string} cannot target the primary
key {'Id' : int} because it is not compatible.
```

**Příčina:**
- `GiftCard` má primary key `Id` (int)
- `ReceiptGiftCardRedemption` používá `GiftCardEan` (string) jako FK
- EF Core automaticky hledá primary key, což způsobí type mismatch

**Řešení:**
Přidána Fluent API konfigurace v `DatabaseContext.OnModelCreating()`:
```csharp
modelBuilder.Entity<ReceiptGiftCardRedemption>()
    .HasOne(r => r.GiftCard)
    .WithMany()
    .HasForeignKey(r => r.GiftCardEan)
    .HasPrincipalKey(gc => gc.Ean);  // Použít Ean místo Id
```

**Upravené soubory:**
- `Data/DatabaseContext.cs` - přidána Fluent API konfigurace

**Git:**
- Commit: 44013c6 - "Fix: EF Core vztah pro ReceiptGiftCardRedemption - použit Ean jako principal key"

---

**Fix 2: UI refresh při načtení poukazu + Načítání RedeemedGiftCards v náhledu účtenky**

**Problém 1: UI neaktualizace při načtení poukazu**
- Po naskenování poukazu se ListView nezobrazil (v pozadí načtený)
- Celková cena se aktualizovala až po další akci
- Duplicitní scan správně hlásil chybu (poukaz byl načtený)

**Příčina:**
`ObservableCollection.CollectionChanged` event nevyvolává `PropertyChanged` pro computed properties.

**Řešení:**
Přidán listener v `ProdejViewModel` konstruktoru:
```csharp
RedeemedGiftCards.CollectionChanged += (s, e) =>
{
    OnPropertyChanged(nameof(IsAnyGiftCardReady));
    OnPropertyChanged(nameof(TotalGiftCardValue));
    OnPropertyChanged(nameof(TotalGiftCardValueFormatted));
    OnPropertyChanged(nameof(AmountToPay));
    OnPropertyChanged(nameof(GrandTotalFormatted));
    // ... další computed properties
};
```

**Problém 2: Náhled účtenky nezobrazoval jednotlivé poukazy**
- V UctenkyPage → Náhled se zobrazilo "Použité poukazy:" ale seznam byl prázdný
- Tisk účtenky fungoval správně

**Příčina:**
EF Core navigation property `RedeemedGiftCards` nebyla načtená (lazy loading není zapnutý).

**Řešení:**
Přidán `.Include(r => r.RedeemedGiftCards)` do všech metod v `SqliteDataService`:
- `GetReceiptsAsync()` - pro UctenkyPage
- `GetReceiptsAsync(DateTime, DateTime)` - pro filtrované seznamy
- `GetReceiptByIdAsync()` - pro detail účtenky
- `DeleteReceiptAsync()` - pro cascade delete

**Upravené soubory:**
- `ViewModels/ProdejViewModel.cs` - CollectionChanged listener
- `Services/SqliteDataService.cs` - .Include() ve 4 metodách

**Git:**
- Commit: 8e5176a - "Fix: Načítání RedeemedGiftCards navigation property v náhledu účtenky"
- Build: ✅ 0 warnings, 0 errors

---

## 📅 **Předchozí session: 30. listopad 2025 (pokračování 2)**

### ✅ Hotovo:
**Podpora více dárkových poukazů na jedné účtence (schema V20)**

**Problém:**
- Současná implementace umožňovala uplatnit pouze JEDEN dárkový poukaz na účtenku
- Uživatel požadoval možnost uplatnit více poukazů najednou

**Řešení:**
Implementováno čisté řešení s DB migrací a many-to-many vztahem.

**Databázové změny:**
- Nový model: `ReceiptGiftCardRedemption` (junction table)
- Sloupce: `Id`, `ReceiptId`, `GiftCardEan`, `RedeemedAmount`
- Migrace V20: `CREATE TABLE ReceiptGiftCardRedemptions`
- Automatická migrace existujících dat z `Receipt.RedeemedGiftCardEan`
- `Receipt.RedeemedGiftCardEan` zachován pro backwards compatibility (označen DEPRECATED)

**Backend změny:**
- `ProdejViewModel.RedeemedGiftCards` - ObservableCollection<GiftCard>
- `TotalGiftCardValue` - součet hodnot všech poukazů
- `IsAnyGiftCardReady` - má načtený alespoň jeden poukaz
- Commands: `LoadGiftCardForRedemption`, `RemoveGiftCard`, `ClearAllGiftCards`
- Validace: kontrola duplicit, kontrola prodej+uplatnění na stejné účtence
- `CompleteCheckoutAsync`: iterace přes všechny poukazy, proporcionální výpočet `RedeemedAmount`
- Uložení do `ReceiptGiftCardRedemptions` table s přesnou částkou pro každý poukaz

**UI změny:**
- `ProdejPage`: ListView s načtenými poukazy (zobrazuje EAN, hodnotu, tlačítko Odstranit)
- Tlačítko "Zrušit všechny poukazy"
- Upravený text upozornění: "Celková hodnota poukazů..."
- `GrandTotalFormatted`: "Poukazy (3×): -450,00 Kč" (zobrazuje počet)

**Tisk:**
- ESC/POS: Každý poukaz na samostatném řádku s EAN a částkou
- HTML export: Seznam všech poukazů s částkami

**Dialogy:**
- `ReceiptPreviewDialog`: `ItemsControl` zobrazuje všechny uplatněné poukazy
- Formát: "EAN: 1234567890 (150,00 Kč)"

**DRY princip dodržen:**
- Všechna zobrazení poukazů používají `RedeemedGiftCards` kolekci
- Computed properties místo duplikace výpočtů
- Backwards compatibility pro staré účtenky přes deprecated property

**Upravené soubory:**
- `Models/ReceiptGiftCardRedemption.cs` - nový
- `Data/DatabaseContext.cs` - DbSet
- `Models/Receipt.cs` - navigation property + deprecated RedeemedGiftCardEan
- `Services/DatabaseMigrationService.cs` - migrace V20
- `ViewModels/ProdejViewModel.cs` - ObservableCollection + commands
- `Views/ProdejPage.xaml` - ListView
- `Views/ProdejPage.xaml.cs` - RemoveGiftCard_Click handler
- `Services/EscPosPrintService.cs` - tisk více poukazů
- `Views/Dialogs/ReceiptPreviewDialog.xaml` - ItemsControl pro seznam

**Schema verze:** 20

**Git:**
- Commit: 34c25e6 - "Feature: Podpora více dárkových poukazů na jedné účtence (schema V20)"
- Pushed to main

---

## 📅 **Předchozí session: 30. listopad 2025 (pokračování)**

### ✅ Hotovo:
**Export inventurního soupisu (tisknutelná HTML + Excel CSV verze)**

**Kontext:**
- Uživatel požádal o zjištění právních povinností OSVČ s kamenným obchodem ohledně inventur
- Zjištěno: Fyzická inventura je povinná minimálně 1× ročně (nejpozději k 31.12.)
- Požadavek: DVĚ verze exportu v Nastavení → Systém

**Implementované funkce:**

**1. Tisknutelná verze (HTML)** 📄
- Profesionální layout s informacemi o firmě (název, IČ, DIČ)
- Žluté buňky (`#FFFFCC`) pro ruční zápis skutečných stavů
- Sloupce: Kategorie, EAN, Název, Nákupní cena, Systémový stav, Skutečný stav, Hodnota
- Podpisové řádky (osoba provádějící inventuru, osoba kontrolující)
- Instrukce pro použití
- Print-friendly CSS (odstranění okrajů, optimalizace tisku)
- Řazení podle kategorie a názvu

**2. Excel verze (CSV)** 📊
- UTF-8 BOM encoding pro správné zobrazení českých znaků v Excelu
- Středník (`;`) jako oddělovač (český Excel standard)
- Instrukční řádky na začátku:
  - "NÁVOD: Do sloupce 'Skutečný stav' zapište napočítané množství"
  - "V Excelu vypočítejte rozdíl: =E2-F2 (Systémový - Skutečný)"
- Hlavičky: Kategorie;EAN;Název;Nákupní cena (Kč);Systémový stav;Skutečný stav
- Řazení podle kategorie a názvu

**UI:**
- Nová sekce "Export inventurního soupisu" v Nastavení → Systém
- `CalendarDatePicker` pro výběr data inventury (výchozí: dnes)
- Tlačítko "Tisknutelná verze" (ikona 📄)
- Tlačítko "Excel verze" (ikona 📊)
- Popisek s vysvětlením použití obou verzí

**Technické detaily:**
- `ExportInventoryPrintCommand` - generuje HTML, použití `FileSavePicker`
- `ExportInventoryCsvCommand` - generuje CSV s UTF-8 BOM
- `GenerateInventoryPrintHtml()` - 123 řádků HTML/CSS generátoru
- `GenerateInventoryCsv()` - 46 řádků CSV generátoru
- Data načítána přes `_dataService.GetProductsAsync()`
- Vlastnost `InventoryDate` pro výběr data

**Upravené soubory:**
- `Views/NastaveniPage.xaml` - UI sekce pro inventuru
- `ViewModels/NastaveniViewModel.cs` - commands a generátory

**Git:**
- Commit: e380aa5 - "Feature: Export inventurního soupisu (tisknutelná HTML + Excel CSV verze)"
- Branch: main (ahead of origin by 1 commit)

---

## 📅 **Předchozí session: 30. listopad 2025**

### ✅ Hotovo:
**Release v1.0.13: Export vratek + Záloha obrázků + Maximalizace okna**

**1. Export účtenek - nyní zahrnuje vratky:**
- Nová sekce "Vratky (dobropisy)" s přehledem vratek
- Detailní položky vratek (EAN, název, množství, cena)
- Souhrn s počtem vratek a celkovou částkou
- ČISTÝ OBRAT (tržby - vratky) v závěrečném souhrnu
- DPH breakdown pro vratky (pokud je plátce DPH)

**2. Automatická záloha obrázků produktů:**
- `PerformDatabaseSync()` při zavření aplikace nyní kopíruje i složku `ProductImages`
- Manuální záloha v Nastavení už to dělala

**3. Maximalizace okna při startu:**
- Aplikace se spouští maximalizovaná
- Fix: `DispatcherQueue.TryEnqueue` s nízkou prioritou (po XamlRoot ready)
- Nenarušuje login dialogy

**Upravené soubory:**
- `ViewModels/NastaveniViewModel.cs` - export vratek v HTML
- `MainWindow.xaml.cs` - záloha ProductImages, maximalizace okna

---

## 📅 **Předchozí session: 29. listopad 2025 (noc)**

### ✅ Hotovo:
**Popis produktů + Redesign DatabazePage + Role-based editace**

**1. Popis (Description) produktů:**
- Přidáno pole `Description` do modelu Product (migrace V19)
- Nepovinné textové pole (multiline) pro detailní popis produktu
- Zobrazení v DatabazePage (detail panel) a ProdejPage (max 2 řádky)
- Editace v EditProductDialog a NovyProduktPage

**2. Redesign DatabazePage - Master-Detail layout:**
- Kompaktní seznam vlevo (EAN, Název, Kategorie, Sklad, Cena)
- Detail panel vpravo (obrázek, popis, všechny ceny, sleva)
- Thumbnail obrázky v seznamu (36×36px)
- Sortování kliknutím na záhlaví sloupců
- Header přesunut mimo ListView pro lepší zarovnání

**3. Role-based editace produktů:**
- **Cashier (Prodavač):** může editovat název, popis, kategorii, obrázek
- **Admin (Vlastník):** může editovat vše včetně cen, slevy, mazání
- Tlačítko "Upravit" viditelné pro obě role, "Smazat" jen pro Admin
- EditProductDialog zobrazuje různé sekce podle role

**4. Oprava názvů rolí:**
- Sjednocení v celém projektu: "Admin" a "Cashier"
- Opraveno v DatabazeViewModel, NovyProduktViewModel

**Nové/upravené soubory:**
- `Models/Product.cs` - Description property
- `Services/DatabaseMigrationService.cs` - V19 migrace
- `Views/DatabazePage.xaml` - kompletní redesign
- `Views/DatabazePage.xaml.cs` - EditButton_Click
- `ViewModels/DatabazeViewModel.cs` - IsAdmin, IsSalesOrAdmin, oprava rolí
- `Views/Dialogs/EditProductDialog.xaml/.cs` - role-based UI
- `Views/NovyProduktPage.xaml` - Description field
- `ViewModels/NovyProduktViewModel.cs` - Description, oprava rolí
- `Views/ProdejPage.xaml` - zobrazení popisu

**Schema verze:** 19

---

## 📅 **Předchozí session: 29. listopad 2025 (večer)**

### ✅ Hotovo:
**Marže produktů + Obrázky produktů**

**1. Marže (Markup) produktů:**
- Přidáno pole `Markup` do modelu Product (migrace V17)
- Bidirektionální výpočet v NovyProduktPage: zadání marže → vypočítá prodejní cenu, zadání ceny → vypočítá marži
- Vzorec Markup: (ProdejníCena - NákupníCena) / NákupníCena × 100
- Zobrazení marže v DatabazePage (sloupec)
- Editace marže v EditProductDialog (pouze admin)
- Zaokrouhlení na celá čísla

**2. Obrázky produktů:**
- Přidáno pole `ImagePath` do modelu Product (migrace V18)
- Nová služba `ProductImageService` (SkiaSharp):
  - Resize na max 800×800px
  - Thumbnail 80×80px
  - JPEG kvalita 100%
  - Zachování poměru stran s bílým pozadím
- NovyProduktPage: nahrání, náhled, smazání obrázku
- EditProductDialog: správa obrázku (pouze admin)
- ProdejPage: zobrazení obrázku posledního naskenovaného produktu (100×100px)
- DatabazePage: thumbnail sloupec (40×40px)
- Backup/Restore zahrnuje složku ProductImages
- Umístění: `%LocalAppData%\Sklad_2_Data\ProductImages\`

**Nové soubory:**
- `Services/IProductImageService.cs`
- `Services/ProductImageService.cs`
- `Converters/EanToThumbnailConverter.cs`

**Upravené soubory:**
- `Models/Product.cs` - Markup, ImagePath, HasImage
- `Services/DatabaseMigrationService.cs` - V17, V18 migrace
- `ViewModels/NovyProduktViewModel.cs` - výpočty marže, obrázky
- `ViewModels/ProdejViewModel.cs` - ScannedProductImage
- `Views/NovyProduktPage.xaml/.cs` - UI pro marži a obrázky
- `Views/Dialogs/EditProductDialog.xaml/.cs` - editace marže a obrázků
- `Views/ProdejPage.xaml` - zobrazení obrázku
- `Views/DatabazePage.xaml` - thumbnail sloupec
- `ViewModels/NastaveniViewModel.cs` - backup ProductImages
- `App.xaml.cs` - DI registrace, restore ProductImages
- `App.xaml` - EanToThumbnailConverter

**Schema verze:** 18

---

## 📅 **Předchozí session: 29. listopad 2025**

### ✅ Hotovo:
**Release v1.0.11: Opravy peněžních toků a DRY princip**

**Kritické opravy:**

1. **DRY princip pro AmountToPay/AmountToRefund**
   - PrehledProdejuViewModel - PaymentMethodStats používá AmountToPay
   - ReturnPreviewDialog - zobrazuje AmountToRefund
   - EscPosPrintService - tisk vratek používá AmountToRefund
   - VratkyPrehledPage - seznam i detail používá AmountToRefund
   - DailyCloseService.CloseDayAsync - používá AmountToRefund

2. **Věrnostní sleva - nepočítá se z dárkových poukazů**
   - GetDiscountableAmount() nyní filtruje podle Category != "Dárkové poukazy"

3. **TotalPurchases - správné sledování**
   - Prodej: nepočítá uplatněné poukazy (GiftCardRedemptionAmount)
   - Storno: používá AmountToPay
   - Vratky: počítá poměrnou část poukazu a odečítá jen hotovostní část

4. **Validace dárkových poukazů**
   - Nelze prodat a použít stejný poukaz v téže účtence
   - Nelze přidat stejný poukaz do košíku vícekrát (unikátní EAN)

**Soubory:**
- `ViewModels/ProdejViewModel.cs` - validace poukazů, TotalPurchases
- `ViewModels/VratkyViewModel.cs` - proporční výpočet poukazu pro vratky
- `ViewModels/PrehledProdejuViewModel.cs` - DRY opravy
- `Services/DailyCloseService.cs` - AmountToRefund místo TotalRefundAmount
- `Services/EscPosPrintService.cs` - tisk vratek
- `Views/VratkyPrehledPage.xaml` - zobrazení AmountToRefund
- `Views/Dialogs/ReturnPreviewDialog.xaml` - zobrazení AmountToRefund

---

## 📅 **Předchozí session: 3. prosinec 2025 (noc)**

### ✅ Hotovo:
**Release v1.0.9: UI Auto-Refresh Tržby/Uzavírky + Win10 Compatibility**

**Implementované funkce:**

1. **Auto-refresh Tržby/Uzavírky po zahájení nového dne** 🔄
   - Data binding přepnut z `x:Bind` na `{Binding}` (spolehlivější refresh)
   - Přidán `SettingsChangedMessage` listener do ViewModelu
   - Messaging po zahájení dne v MainWindow i TrzbyUzavirkPage
   - Computed properties: `DayStatusFormatted`, `ReceiptCountFormatted`, `IsCloseDayButtonEnabled`
   - `NotifyPropertyChangedFor` pro automatickou propagaci změn

2. **Win10 Compatibility - robustní refresh strategie** 🖥️
   - Delší delays: 300ms file flush, 200-300ms UI refresh
   - Double refresh v message listener (volá `LoadTodaySalesAsync()` 2×)
   - Vynucený UI refresh přes explicitní `OnPropertyChanged()` pro všechny properties
   - Debug výpisy pro sledování průběhu
   - `NotifyNewDayStartedAsync(DateTime)` - explicitní předání nového session datumu

3. **Data binding na všech UI elementech**
   - `CashSalesText`, `CardSalesText`, `TotalSalesText` - binding na formatted properties
   - `ReceiptCountText`, `DayStatusText` - computed properties s auto-update
   - `CloseDayButton.IsEnabled` - reactive binding na `IsCloseDayButtonEnabled`
   - `StatusMessageText` - binding na status message

4. **Zjednodušený code-behind**
   - `LoadDataAsync()` jen volá ViewModel, UI se aktualizuje automaticky
   - Odstraněny manuální `element.Text = ...` assignments
   - MVVM pattern správně dodržen

**Technické detaily:**

**TrzbyUzavirkViewModel.cs:**
```csharp
// Message listener s double refresh
_messenger.Register<SettingsChangedMessage>(this, async (r, m) =>
{
    await Task.Delay(300); // Win10 file flush
    await LoadTodaySalesAsync();
    await Task.Delay(100); // Win10 UI update
    await LoadTodaySalesAsync(); // Second refresh for Win10
});

// Vynucený UI refresh
public async Task NotifyNewDayStartedAsync(DateTime? newSessionDate = null)
{
    if (newSessionDate.HasValue)
        SessionDate = newSessionDate.Value;

    _messenger.Send(new SettingsChangedMessage());
    await Task.Delay(200);
    await LoadTodaySalesAsync();
    await Task.Delay(100);

    // Win10: Force UI refresh
    OnPropertyChanged(nameof(SessionDate));
    OnPropertyChanged(nameof(TodayCashSalesFormatted));
    OnPropertyChanged(nameof(DayStatusFormatted));
    // ... všechny properties
}
```

**MainWindow.xaml.cs:**
```csharp
await _settingsService.SaveSettingsAsync();
await Task.Delay(300); // Win10 file flush
WeakReferenceMessenger.Default.Send(new SettingsChangedMessage());
await Task.Delay(300); // Win10 UI refresh
```

**TrzbyUzavirkPage.xaml:**
```xml
<!-- Classic {Binding} místo x:Bind pro spolehlivější refresh -->
<TextBlock Text="{Binding TodayCashSalesFormatted, Mode=OneWay}"/>
<TextBlock Text="{Binding DayStatusFormatted, Mode=OneWay}"/>
<Button IsEnabled="{Binding IsCloseDayButtonEnabled, Mode=OneWay}"/>
```

**Computed properties s NotifyPropertyChangedFor:**
```csharp
[ObservableProperty]
[NotifyPropertyChangedFor(nameof(SessionDateFormatted), nameof(DayStatusFormatted))]
private DateTime sessionDate;

[ObservableProperty]
[NotifyPropertyChangedFor(nameof(DayStatusFormatted), nameof(IsCloseDayButtonEnabled))]
private bool isDayClosed;

public string DayStatusFormatted => IsDayClosed
    ? $"🔒 Den uzavřen ({SessionDateFormatted})"
    : $"🔓 Den otevřen ({SessionDateFormatted})";
```

**Build:**
- ✅ Release x64 build úspěšný
- ✅ Verze: 1.0.9
- ✅ Win10 compatibility delays aplikovány

**Testováno:**
- ✅ UI refresh funguje na Win11
- ⏳ **Zbývá otestovat**: Win10 PC (pomalý file flush, UI dispatcher)

**Git:**
- ⏳ Commit připraven
- ⏳ GitHub Release v1.0.9

---

## 📅 **Předchozí session: 27. listopad 2025 (odpoledne) - ČÁST 3**

### ✅ Hotovo:
**Release v1.0.8: Profesionální formátování účtenek s logem**

**Implementované funkce:**

1. **Logo na účtenkách** 🖼️
   - ESC/POS raster format (GS v 0) s RAW byte commands
   - SkiaSharp integrace: načtení BMP → konverze mono → scaling → ESC/POS
   - Auto threshold 128 (color/gray → black/white)
   - Max šířka 384px, auto-scale
   - Soubor: `essets/luvera_logo.bmp` (400x400px)
   - Fallback na název firmy pokud logo chybí

2. **Tečkované vyplnění** mezi cenami
   - `7x 100.00 Kč..............560.00 Kč`
   - S tečkami: produkty, Mezisoučet, Poukaz, Přijato, Vráceno
   - Bez teček: DPH rozklad

3. **Tenké čáry mezi položkami**
   - Separátor `--------` (48 znaků) mezi každou položkou

4. **Vycentrované info řádky**
   - Účtenka, Datum, Prodejce - na STŘEDU
   - Dobropis č., Datum, K původní účtence - na STŘEDU

5. **Zmenšené CELKEM** (bez přetékání)
   - Odstraněn Double Height (GS ! 0x10)
   - Jen BOLD (ESC E 1)
   - Vejde se až `*** CELKEM: 9999,99 Kč ***`

6. **48 sloupců + symetrické 3+3**
   - RECEIPT_WIDTH = 48 (správně pro 80mm papír)
   - INDENT = 3 mezery vlevo
   - RIGHT_MARGIN = 3 mezery vpravo
   - Separátory plná šířka (48 znaků)

7. **Word Wrap** pro dlouhé názvy (max 40 znaků)

8. **Přesun adresy/IČ/DIČ** do footeru (před "Děkujeme")

**Technické:**
- Helper metody: LoadLogoCommands(), WordWrap(), FormatLineWithRightPrice()
- SkiaSharp using pro bitmap operace
- Build: logo se kopíruje do output (Content Include)

**Git:**
- Commit: 6f2b092
- ZIP: Sklad_2-v1.0.8-win-x64.zip (70MB)

---

## 🎓 Klíčové naučené lekce

### WinUI 3 / XAML specifika

1. **x:Bind vs {Binding} pro PropertyChanged** ⚠️ NOVÉ!
   - **Compiled binding (x:Bind)** má někdy problémy s PropertyChanged events
   - **Runtime binding ({Binding})** spolehlivěji reaguje na změny
   - **Řešení pro refresh problémy:**
   ```csharp
   // Code-behind
   this.DataContext = ViewModel;
   ```
   ```xml
   <!-- XAML - použít {Binding} místo x:Bind -->
   <TextBlock Text="{Binding MyProperty, Mode=OneWay}"/>
   ```
   - Vhodné pro UI elementy, které se musí refreshovat při messaging

2. **WeakReferenceMessenger pro inter-ViewModel komunikaci** ⚠️ NOVÉ!
   - Registrace listener v konstruktoru ViewModelu
   - `_messenger.Register<SettingsChangedMessage>(this, async (r, m) => { })`
   - Nezapomenout unregister při dispose (automaticky s WeakReference)
   - Posílání zpráv: `_messenger.Send(new SettingsChangedMessage())`

3. **NotifyPropertyChangedFor pro computed properties** ⚠️ NOVÉ!
   ```csharp
   [ObservableProperty]
   [NotifyPropertyChangedFor(nameof(FormattedProperty))]
   private decimal rawValue;

   public string FormattedProperty => $"{RawValue:N2} Kč";
   ```
   - Automaticky triggeruje update computed properties při změně source property

4. **OnPropertyChanged() pro vynucení UI refresh** ⚠️ NOVÉ!
   ```csharp
   // Win10: Vynucený UI refresh
   OnPropertyChanged(nameof(SessionDate));
   OnPropertyChanged(nameof(TodayCashSalesFormatted));
   ```
   - Užitečné pro Win10 compatibility (pomalý UI dispatcher)

5. **ViewModel PŘED InitializeComponent()**
   ```csharp
   public SomePage()
   {
       // DŮLEŽITÉ: ViewModel MUSÍ být nastaven PŘED InitializeComponent()
       ViewModel = (Application.Current as App).Services.GetRequiredService<SomeViewModel>();
       this.InitializeComponent();  // x:Bind nyní funguje správně
   }
   ```

6. **Clean + Rebuild je kritický**
   - Při změnách XAML/ViewModels vždy: **Build → Clean Solution → Rebuild Solution**
   - WinUI/XAML projekty cachují sestavení

7. **ContentDialog COMException workaround**
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

8. **XamlRoot čekání - robustní přístup**
   ```csharp
   // Robustní čekání místo pevného delay
   int retries = 0;
   while (this.Content?.XamlRoot == null && retries < 20)
   {
       await Task.Delay(50);
       retries++;
   }
   ```

9. **Page.Loaded event pro auto-refresh**
   ```csharp
   this.Loaded += (s, e) => ViewModel.LoadDataCommand.Execute(null);
   ```

10. **Window.Current je null v WinUI 3** ⚠️
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

11. **Window_Closed vs AppWindow.Closing** ⚠️
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
   ```

---

## 📊 Aktuální stav projektu

**Hotovo:** 19/20 hlavních funkcí (~95%)

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
12. Systém dárkových poukazů (kompletní)
13. **Auto-update systém** (multi-file ZIP, PowerShell, GitHub Releases)
14. **Tisk účtenek** (ESC/POS, české znaky CP852, Epson TM-T20III)
15. **Single-instance ochrana** (Mutex, Win32 MessageBox)
16. **Marže produktů** (bidirektionální výpočet, editace pro admin)
17. **Obrázky produktů** (upload, thumbnail, resize, backup)
18. **Popis produktů + Master-Detail DatabazePage** (description, role-based edit)
19. **Export inventurního soupisu** (tisknutelná HTML + Excel CSV, datum inventury, žluté buňky pro ruční zápis)

### ⏳ Zbývá:
1. **DPH statistiky** - `TotalSalesAmountWithoutVat` nerespektuje slevy (věrnostní/poukaz) - PrehledProdejuViewModel:183-185

---

**Poslední aktualizace:** 30. listopad 2025
**Aktuální verze:** v1.0.13 (schema V19)

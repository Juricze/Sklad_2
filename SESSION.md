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

## 📅 **Poslední session: 30. listopad 2025 (pokračování 6)**

### ✅ Hotovo:
**Release v1.0.18: UI Polishing DatabazePage - Responzivní detail + Robustní layout**

**1. KRITICKÁ OPRAVA: Revert ItemContainerStyle breaking change**
- **Problém**: ItemContainerStyle s Padding="0" úplně rozbil Grid layout v seznamu produktů
- **Symptom**: Všechny sloupce se zhroutily do jedné horizontální řady, text vedle sebe
- **Příčina**: ListView potřebuje svůj výchozí padding pro správné renderování Grid uvnitř DataTemplate
- **Fix**: Odstraněn ItemContainerStyle, Header Padding vrácen na "12,8"
- **LESSON LEARNED**: ⚠️ **NIKDY nenastavovat ItemContainerStyle Padding="0" - ničí Grid layout!**

**2. Postupné zvětšování detail obrázku:**
- **Fáze 1**: 400×300 px → 500×500 px (malé obrazovky OK, velké příliš malý)
- **Fáze 2**: 500×500 px → 1000×1000 px (lepší, ale stále ne ideální)
- **Fáze 3**: 1000×1000 px → **2000×2000 px** (finální - perfektní na všech rozlišeních)
- FontIcon placeholder: 128px → 256px → **512px**
- Zachováno `Stretch="Uniform"` pro aspect ratio

**3. Finální úprava sloupců pro robustnost:**
- **Sklad sloupec**: 1* → **2*** (opraveno "ujíždění doprava")
- **MinWidth constraints** přidány pro prevenci nečitelnosti při zmenšování okna:
  - EAN: MinWidth="80"
  - Název: MinWidth="100"
  - Značka: MinWidth="80"
  - Kategorie: MinWidth="90"
  - Sklad: MinWidth="60"
  - Cena: MinWidth="80"
- Header Padding: finálně **"12,8,12,8"** (odpovídá ListView internal padding)

**4. Synchronizace image storage s UI capabilities:**
- **Problém**: MAX_IMAGE_SIZE byl 1600px, ale UI zobrazuje až 2000px
- **Fix**: `ProductImageService.MAX_IMAGE_SIZE` zvýšen z 1600 → **2000**
- **Důsledek**: Nově uploadované obrázky se ukládají ve vyšší kvalitě

**Upravené soubory:**
- `Views/DatabazePage.xaml` - revert ItemContainerStyle, image 2000px, MinWidth, Sklad 2*
- `Services/ProductImageService.cs` - MAX_IMAGE_SIZE 2000

**Git:**
- Commit: 9a13fd6 - "Revert: Zarovnání headeru (ItemContainerStyle rozbil layout)"
- Commit: 33a8c09 - "UX: Zvětšení obrázku na 500px + Header padding 0,8"
- Commit: c3d85b0 - "UX: Finální úpravy DatabazePage - Obrázek 2000px + MinWidth sloupců"
- Release: v1.0.18 (self-contained)

---

## 📅 **Předchozí session: 30. listopad 2025 (pokračování 5)**

### ✅ Hotovo:
**Release v1.0.17: UI polishing - Zarovnání + Responzivní obrázky (mezistupeň)**

**1. Fix: Zarovnání headeru se seznamem produktů (LATER REVERTED)**
- Header Grid: Padding změněn z "12,8" → "0,8"
- ItemTemplate Grid: Zachován původní "0,6"
- ItemContainerStyle: Přidán Padding="0" (⚠️ ROZBILO LAYOUT - revertováno v v1.0.18!)

**2. UX: Responzivní velikost obrázku v detail panelu**
- **Před**: Fixní `Width="400" Height="400"` → na malých obrazovkách přes většinu výšky
- **Po**: `MaxWidth="400" MaxHeight="300"` → automatické přizpůsobení
- Zachován aspect ratio (`Stretch="Uniform"`)

**Upravené soubory:**
- `Views/DatabazePage.xaml` - zarovnání headeru, responzivní obrázek

**Git:**
- Commit: 521323b - "Fix: Zarovnání headeru DatabazePage se seznamem produktů"
- Commit: a769f2b - "UX: Responzivní velikost obrázku v detail panelu produktu"
- Release: v1.0.17

---

## 📅 **Předchozí session: 30. listopad 2025 (pokračování 4)**

### ✅ Hotovo:
**Release v1.0.16: Profesionální UI upgrade DatabazePage + Klikatelné EAN + Zvětšení obrázků**

**1. Profesionální redesign seznamu produktů:**
- Přidán sloupec **Značka** (7. sloupec, fialová barva #FF6B4EBB)
- Přidán sloupec **Kategorie** (již existoval, aktualizován na modrou #FF0078D7)
- Profesionální Card layout filter bar s shadowem
- 7-sloupcové rozložení: Obrázek | EAN | Název | Značka | Kategorie | Sklad | Cena

**2. Upgrade filtrovacího systému:**
- **Brand filter** (ComboBox, 🟣 fialová ikona)
- **Category filter** (ComboBox, 🔵 modrá ikona)
- Tlačítko "Vymazat" pro rychlý reset všech filtrů
- Dynamické načítání značek/kategorií z databáze
- Auto-refresh při změně VatConfigs (messaging)

**3. Fix kritických chyb:**
- **Categories** načítání z `ProductCategories.All` → `GetProductCategoriesAsync()` (DB)
- **Navigation properties** null → přidán `.Include(p => p.Brand).Include(p => p.ProductCategory)`
- Brand/Category se nyní správně zobrazují v seznamu i filtru

**4. Delete validation:**
- Potvrzovací ContentDialog před smazáním produktu
- Varování pokud má produkt StockQuantity > 0
- Dvoustupňové potvrzení (Zrušit je default)

**5. Optimalizace šířek sloupců:**
Podle škály 1-5 (nejužší-nejširší):
- Obrázek: 44px (fixní thumbnail)
- EAN: 4* (důležité pro identifikaci)
- Název: 5* (nejširší - hlavní info)
- Značka: 3* (střední)
- Kategorie: 3* (střední)
- Sklad: 1* (nejužší - krátké číslo)
- Cena: 2* (úzké - krátké číslo)

**6. Rozšíření detail panelu:**
- Šířka zvýšena na **30%** celkové šířky (proporcionální 7:3)
- Seznam produktů: 70%
- Detail panel: 30%

**7. Klikatelné EAN kódy s kopírováním:**
- **V seznamu**: HyperlinkButton místo TextBlock
- **V detail panelu**: HyperlinkButton pod ikonou
- **Po kliku**: EAN se zkopíruje do schránky (Clipboard API)
- **Feedback**: ContentDialog "EAN zkopírován" s konkrétním číslem
- **Tooltip**: "Klikněte pro zkopírování EAN"

**8. Zvětšení obrázku v detail panelu (+100%):**
- **Detail panel**: 200×200 → **400×400 px** (později změněno na MaxWidth/MaxHeight)
- **Placeholder ikona**: 64px → **128px**
- **Seznam**: Thumbnail zůstal 36×36 px (beze změny)
- **MAX_IMAGE_SIZE**: 800px → **1600px** (lepší kvalita ukládání)
- **THUMBNAIL_SIZE**: Zůstal 80px

**Upravené soubory:**
- `Views/DatabazePage.xaml` - 7 sloupců, filter bar, klikatelné EAN, větší obrázek
- `Views/DatabazePage.xaml.cs` - ClearFilters_Click, DeleteButton_Click, EanButton_Click
- `ViewModels/DatabazeViewModel.cs` - Brands filter, RefreshCategoriesAsync/RefreshBrandsAsync
- `Services/SqliteDataService.cs` - .Include() pro navigation properties
- `Services/ProductImageService.cs` - MAX_IMAGE_SIZE 1600px

**Git:**
- Commit: 9f303c1 - "UI: Optimalizace šířek sloupců v DatabazePage"
- Commit: 618699e - "UI: Rozšířen detail panel produktu na 30% šířky"
- Commit: c99f725 - "Feature: Klikatelné EAN kódy + Zvětšení obrázku v detail panelu"
- Release: v1.0.16

---

## 🎓 Klíčové naučené lekce

### EF Core + Navigation Properties ⚠️

**1. Eager Loading je POVINNÉ pro navigation properties**
```csharp
// ❌ ŠPATNĚ - navigation property bude null
return await context.Products.ToListAsync();

// ✅ SPRÁVNĚ - .Include() načte Brand a ProductCategory
return await context.Products
    .Include(p => p.Brand)
    .Include(p => p.ProductCategory)
    .ToListAsync();
```

**2. Fluent API pro non-standard foreign keys**
```csharp
// Pokud FK není primary key, musíš specifikovat HasPrincipalKey
modelBuilder.Entity<ChildEntity>()
    .HasOne(c => c.Parent)
    .WithMany()
    .HasForeignKey(c => c.ParentAlternateKey)
    .HasPrincipalKey(p => p.AlternateKey);  // KRITICKÉ!
```

**3. ObservableCollection.CollectionChanged nevyvolává PropertyChanged**
```csharp
// ✅ Přidej listener v konstruktoru ViewModelu
MyCollection.CollectionChanged += (s, e) =>
{
    OnPropertyChanged(nameof(ComputedPropertyA));
    OnPropertyChanged(nameof(ComputedPropertyB));
};
```

### WinUI 3 / XAML specifika

**1. Clipboard API pro kopírování textu**
```csharp
using Windows.ApplicationModel.DataTransfer;

var dataPackage = new DataPackage();
dataPackage.SetText(textToCopy);
Clipboard.SetContent(dataPackage);
```

**2. HyperlinkButton pro klikatelný text**
```xaml
<HyperlinkButton Content="{x:Bind Ean}"
                 Click="EanButton_Click"
                 Padding="0"
                 ToolTipService.ToolTip="Klikněte pro zkopírování"/>
```

**3. Proporcionální column widths**
```xaml
<!-- 7:3 = 70% : 30% -->
<ColumnDefinition Width="7*"/>
<ColumnDefinition Width="3*"/>
```

**4. Responzivní velikosti s MaxWidth/MaxHeight** ⚠️ NOVÉ!
```xaml
<!-- ❌ ŠPATNĚ - fixní velikost, problémy na malých obrazovkách -->
<Border Width="400" Height="400">
    <Image Source="{Binding}"/>
</Border>

<!-- ✅ SPRÁVNĚ - automatické přizpůsobení -->
<Border MaxWidth="400" MaxHeight="300">
    <Image Source="{Binding}"
           MaxWidth="400"
           MaxHeight="300"
           Stretch="Uniform"/>
</Border>
```
- Na velkých obrazovkách: maximální velikost
- Na malých obrazovkách: automaticky menší
- `Stretch="Uniform"` zachová aspect ratio

**5. Zarovnání ListView s headerem** ⚠️ KRITICKÉ!
```xaml
<!-- ❌ ŠPATNĚ - ItemContainerStyle Padding="0" ROZBÍJÍ GRID LAYOUT! -->
<ListView>
    <ListView.ItemContainerStyle>
        <Style TargetType="ListViewItem">
            <Setter Property="Padding" Value="0"/>  <!-- NEBEZPEČNÉ! -->
        </Style>
    </ListView.ItemContainerStyle>
</ListView>

<!-- ✅ SPRÁVNĚ - Header padding odpovídá ListView internal padding -->
<Grid Padding="12,8,12,8" ColumnSpacing="8">  <!-- Header Grid -->
    <TextBlock Grid.Column="0" Text="Název"/>
</Grid>

<ListView>
    <!-- ŽÁDNÝ ItemContainerStyle! ListView potřebuje výchozí padding pro Grid layout -->
    <ListView.ItemTemplate>
        <DataTemplate>
            <Grid Padding="0,6" ColumnSpacing="8">  <!-- ItemTemplate Grid -->
                <TextBlock Grid.Column="0" Text="{Binding Name}"/>
            </Grid>
        </DataTemplate>
    </ListView.ItemTemplate>
</ListView>
```
- **NIKDY** nenastavovat ItemContainerStyle Padding="0" - zničí Grid layout uvnitř DataTemplate!
- Header padding musí odpovídat ListView internal padding (obvykle 12px left/right)
- ItemTemplate Grid má vlastní padding pro vertikální spacing (např. "0,6")

---

## 📊 Aktuální stav projektu

**Hotovo:** 20/20 hlavních funkcí (~100%)

### ✅ Implementováno:
1. Role-based UI restrictions
2. Databáze produktů - **profesionální UI** (Brand/Category filtry, master-detail, klikatelné EAN, **responzivní obrázky**)
3. Status Bar (Informační panel)
4. Dashboard prodejů (KPI, top/worst produkty, platby)
5. Denní otevírka/uzavírka pokladny
6. DPH systém (konfigurace)
7. Historie pokladny s filtry
8. Dynamická správa kategorií **+ Značek**
9. PPD Compliance (profesionální účtenky, storno, export FÚ)
10. UI optimalizace pro neplátce DPH
11. Vlastní cesta pro zálohy + Dialog při zavření
12. Systém dárkových poukazů (kompletní, **více poukazů na účtence**)
13. **Auto-update systém** (multi-file ZIP, PowerShell, GitHub Releases)
14. **Tisk účtenek** (ESC/POS, české znaky CP852, Epson TM-T20III, **logo**)
15. **Single-instance ochrana** (Mutex, Win32 MessageBox)
16. **Marže produktů** (bidirektionální výpočet, editace pro admin)
17. **Obrázky produktů** (upload, thumbnail, resize, backup, **2000px kvalita**, **responzivní zobrazení**, **MinWidth constraints**)
18. **Popis produktů + Master-Detail DatabazePage** (description, role-based edit, **robustní layout**)
19. **Export inventurního soupisu** (tisknutelná HTML + Excel CSV)
20. **Brand & Category management** (UI dialogy, schema V21, **profesionální filtry**)

### ⏳ Zbývá:
- **DPH statistiky** - `TotalSalesAmountWithoutVat` nerespektuje slevy (věrnostní/poukaz) - PrehledProdejuViewModel:183-185

---

**Poslední aktualizace:** 30. listopad 2025
**Aktuální verze:** v1.0.18 (schema V21)

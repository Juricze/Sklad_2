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

## 📅 **Poslední session: 4. prosinec 2025 (pokračování 10)**

### ✅ Hotovo:
**Release v1.0.22: Bezpečnostní zálohy + Opravy přehledu prodejů + InfoBar UI**

**1. InfoBar implementace pro Nový produkt**
- **NovyProduktPage.xaml**: Přidán InfoBar komponent na začátek stránky
- **Auto-dismiss**: Success zprávy 3s, Error zprávy 5s
- **NovyProduktViewModel**: Nové metody `SetError()`, `SetSuccess()`, `ClearStatus()`
- **IsError property**: Pro rozlišení severity (Success vs Error)
- **Konverze**: Všech 9 StatusMessage přiřazení změněno na SetError/SetSuccess
- **Konzistence**: Nyní Nový produkt i Věrnostní program mají InfoBar pattern

**2. KRITICKÁ OPRAVA: Přehled prodejů - konzistence s denní uzávěrkou**
- **Problém**: `PrehledProdejuViewModel` používal `AmountToPay` (haléře), `DailyCloseService` používal `FinalAmountRounded`
- **Důsledek**: Nesouhlasily součty přehledu prodejů vs denní uzávěrky
- **Fix**: Změněno `TotalSalesAmount` na `Sum(FinalAmountRounded)`
- **PaymentMethodStats**: Také změněno na `FinalAmountRounded`
- **Výsledek**: Konzistence napříč aplikací (Win10 compatible)

**3. KRITICKÁ OPRAVA: Chyběly vratky v celkové tržbě!**
- **Problém**: `PrehledProdejuViewModel` VŮBEC NEODEČÍTAL VRATKY!
- **Důsledek**: Přehled prodejů ukazoval vyšší tržby než denní uzávěrky
- **Root cause**: LoadSalesDataAsync nenačítal vratky, CalculateTotals je ignorovalo
- **Fix**:
  - Načítání vratek v `LoadSalesDataAsync`
  - Vzorec: `TotalSalesAmount = receiptTotal - returnTotal`
  - Konzistence s `DailyCloseService` vzorcem
- **Výsledek**: Přehled prodejů nyní odpovídá denním uzávěrkám

**4. KRITICKÁ BEZPEČNOST: 4-vrstvá ochrana záloh před přepsáním**
- **Problém**: Původní size check (< 50 KB) selhal - prázdná SQLite DB s tabulkami = ~140 KB
- **Scénář**: Smazaná DB → přihlášení → odhlášení → ZÁLOHY PŘEPSÁNY bez varování!

**Check 1: Empty Database Detection (count-based)**
- Místo size-based kontrola: `productCount == 0 && receiptCount == 0`
- Dialog s detaily (počty, velikost) + kontaktní info
- Dvojí potvrzení před zálohou prázdné DB

**Check 2: Size Comparison (> 50% reduction)**
- Porovnání aktuální DB vs záloha
- Varování pokud `currentDbSize < backupDbSize * 0.5`
- Detekce masivní ztráty dat

**Check 3: Time Travel Detection**
- Porovnání `Settings.LastDayCloseDate` vs `DB.lastDailyCloseDate`
- Detekce obnovení staré zálohy (časový posun)
- Varování pokud poslední aktivita > 7 dní stará

**Check 4: Record Count Comparison (> 5% loss)**
- Otevření backup DB jako read-only SQLite
- Porovnání počtu produktů a účtenek
- Citlivost 5% - detekuje i malé ztráty (10 účtenek z 50)
- **Důležité**: Zachytí částečnou ztrátu dat, ne jen prázdné DB

**5. UX: User Confirmation Option**
- **Změna filozofie**: Z úplného blokování → možnost pokračovat po kontrole
- **Důvod**: Legitimní změny (např. smazání produktů) musí být možné
- **Implementace**:
  - Všechny 4 dialogy mají 3 tlačítka: Primary/Secondary/Close
  - Default = "Ne, nezálohovat" (bezpečné)
  - Uživatel může kliknout "Ano, zálohovat" po manuální kontrole (SQLite Browser)
- **Workflow**: Varování → Kontrola DB externálně → Rozhodnutí → Pokračovat/Zrušit

**6. UX: Zkrácení textů tlačítek**
- **Problém**: Dlouhé texty tlačítek se ořezávaly ("Zalohovat prázdn", "Nezálohovat (DOP")
- **Fix**: Konzistentní krátké texty pro všechny 4 dialogy:
  - Primary: "Ano, zálohovat"
  - Secondary: "Ne, nezálohovat"
  - Close: "Zrušit"
- **Výsledek**: Plná čitelnost bez zkrácení

**7. SECURITY: Kontaktní info + dvojí potvrzení**
- **Všechny 4 kritické dialogy obsahují:**
  ```
  ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
  ⚠️ NEJSTE SI JISTÍ? ZAVOLEJTE!
  📞 Majitel/Admin: +420 739 639 484
  ❌ NEPOKRAČUJTE bez konzultace!
  ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
  ```
- **Dvojí potvrzení**: Po kliknutí "Ano, zálohovat" → ještě jeden "⚠️ POSLEDNÍ POTVRZENÍ" dialog
  - Opakované varování co se stane
  - Opakovaný kontakt
  - Tlačítko "ANO, POTVRDIT ZÁLOHU" (default = Zrušit)
- **Fail-safe**: Uživatel musí potvrdit DVAKRÁT + má DVAKRÁT možnost zavolat

**Upravené soubory:**
- `Views/NovyProduktPage.xaml` - InfoBar komponent
- `Views/NovyProduktPage.xaml.cs` - InfoBar_Closed handler
- `ViewModels/NovyProduktViewModel.cs` - IsError, SetError/SetSuccess/ClearStatus
- `ViewModels/PrehledProdejuViewModel.cs` - FinalAmountRounded, vratky
- `MainWindow.xaml.cs` - 4 checks, user confirmation, kontakt, dvojí potvrzení

**Git:**
- Commit: 8× během session (InfoBar, Fixes, Security checks, UX)
- Release: v1.0.22 (self-contained)

---

## 📅 **Předchozí session: 4. prosinec 2025 (pokračování 9)**

### ✅ Hotovo:
**Release v1.0.21: Telefon do věrnostního programu + Maskování kontaktů + UI prefix +420**

**1. Telefon do věrnostního programu**
- **LoyaltyCustomer model**: Přidán `PhoneNumber` property
- **Validace**: Alespoň Email NEBO Telefon je povinný (ne oba optional)
- **UI prefix**: Viditelný "+420" prefix před inputem (prodavačka zadává jen 9 číslic)
- **Automatické ukládání**: Systém přidá "+420" k zadanému číslu
- **Vyhledávání**: Funguje podle telefonu v ProdejPage i LoyaltyPage
- **Databázová migrace V22**: ADD COLUMN PhoneNumber

**2. Maskování kontaktů na účtenkách a zobrazení**
- **Email maskování**: `pavel@example.cz` → `pav***@***.cz`
  - První 3 znaky lokální části
  - "***@***"
  - Poslední 3 znaky domény (.cz, .com, atd.)
- **Telefon maskování**: `+420739612345` → `+420 7396*****`
  - Předvolba +420 viditelná
  - První 4 čísla
  - Zbytek hvězdičky
- **Priorita zobrazení**: Email > Telefon (pokud oba vyplněny)
- **Model properties**:
  - `LoyaltyCustomer.MaskedEmail` - maskovaný email
  - `LoyaltyCustomer.MaskedPhone` - maskovaný telefon
  - `LoyaltyCustomer.MaskedContact` - email > telefon s prioritou

**3. Receipt model změny (databázová migrace V23)**
- **Přejmenování**: `LoyaltyCustomerEmail` → `LoyaltyCustomerContact`
- **Důvod**: Nyní ukládá email NEBO telefon (ne jen email)
- **Migration**: ALTER TABLE Receipts RENAME COLUMN
- **Schema version**: 22 → 23

**4. UI změny - "Člen" → "Uživatel"**
- **ProdejPage**: Zobrazuje `MaskedContact` (již ne surový email!)
- **ReceiptPreviewDialog**: Label změněn z "Člen:" na "Uživatel:"
- **EscPosPrintService** (tisk účtenek): "Člen:" → "Uživatel:"
- **EscPosPrintService** (textový náhled): "Člen:" → "Uživatel:"
- **LoyaltyPage**: Admin view zůstává s surovým emailem (pro správu kontaktů)

**5. UI pro telefon - prefix +420**
- **LoyaltyPage.xaml**:
  - StackPanel s TextBlock "+420" + TextBox pro číslo
  - TextBlock: FontWeight SemiBold, šedá barva (#666)
  - Width: 100px (bez prefixu)
- **Edit dialog** (LoyaltyPage.xaml.cs):
  - Stejný prefix panel v edit dialogu
  - Automatické odstranění "+420" při zobrazení (pro editaci)
  - Automatické přidání "+420" při uložení
- **LoyaltyViewModel**:
  - Přidání "+420" v AddCustomerCommand
  - Přidání "+420" v UpdateCustomerCommand (pokud tam ještě není)

**6. Vyhledávání podle telefonu**
- **Fix**: ProdejViewModel.SearchLoyaltyCustomersAsync přidána podmínka pro PhoneNumber
- **Funguje**: AutoSuggestBox v ProdejPage nyní hledá i podle telefonu
- **Formát**: Lze zadat s "+420" nebo bez (najde oba)

**Upravené soubory:**
- `Models/LoyaltyCustomer.cs` - PhoneNumber, MaskedEmail, MaskedPhone, MaskedContact, SearchText
- `Models/Receipt.cs` - LoyaltyCustomerEmail → LoyaltyCustomerContact, HasLoyaltyCustomerContact
- `Services/DatabaseMigrationService.cs` - V22 (PhoneNumber), V23 (rename), CURRENT_SCHEMA_VERSION 23
- `Views/LoyaltyPage.xaml` - UI prefix "+420", phone column v tabulce
- `Views/LoyaltyPage.xaml.cs` - Edit dialog s prefix panelem, +420 logika
- `ViewModels/LoyaltyViewModel.cs` - NewPhoneNumber, +420 při ukládání, validace Email/Phone
- `Views/ProdejPage.xaml` - Email → MaskedContact
- `Views/Dialogs/ReceiptPreviewDialog.xaml` - Email → Contact, "Člen" → "Uživatel"
- `Services/EscPosPrintService.cs` - "Člen" → "Uživatel", LoyaltyCustomerContact (2× tisk + náhled)
- `ViewModels/ProdejViewModel.cs` - MaskedContact místo MaskedEmail, PhoneNumber vyhledávání
- `Scripts/CheckDatabaseChanges.ps1` - loyaltyCustomerContact

**Git:**
- Commit: (připraveno)
- Release: v1.0.21 (self-contained)

---

## 📅 **Předchozí session: 1. prosinec 2025 (pokračování 8)**

### ✅ Hotovo:
**Release v1.0.20: Zaokrouhlování na celé koruny + Opravy denní uzavírky + F1 shortcut**

**1. Matematické zaokrouhlování na celé koruny**
- **Implementace**: `Math.Round(..., 0, MidpointRounding.AwayFromZero)`
- **DPH compliance**: Od 1.4.2019 musí být DPH na 2 desetinná místa - zachováno
- **Transparentnost**: Zobrazuje přesnou částku, zaokrouhlení a finální částku k úhradě
- **Model properties** (computed):
  - `Receipt.FinalAmountRounded` - zaokrouhlená částka k úhradě
  - `Receipt.RoundingAmount` - rozdíl zaokrouhlení (+/-)
  - `Receipt.HasRounding` - boolean pro conditional visibility
  - `Return.FinalRefundRounded` - zaokrouhlená částka vratky
  - `Return.RefundRoundingAmount` - rozdíl zaokrouhlení vratky
  - `Return.HasRefundRounding` - boolean pro conditional visibility
- **ViewModel properties**:
  - `ProdejViewModel.AmountToPayRounded` - zaokrouhlená částka
  - `ProdejViewModel.RoundingDifference` - rozdíl zaokrouhlení
  - `ProdejViewModel.HasRounding` - boolean pro UI
  - Formatted properties pro všechny částky

**2. KRITICKÉ OPRAVY: DailyCloseService - 3 bugy kde se používaly přesné místo zaokrouhlené částky**
- **Bug #1 (lines 57-59, 154-156)**: Fallback logika používala `AmountToPay` místo `FinalAmountRounded`
  - Doppad: Denní uzavírka by byla špatná o akumulované zaokrouhlení
- **Bug #2 (lines 70, 167)**: Vratky používaly `AmountToRefund` místo `FinalRefundRounded`
  - Doppad: Vrácené částky by nesouhlasily se skutečně vydanými penězi
- **Fix**: Všechny výpočty nyní používají zaokrouhlené částky (FinalAmountRounded, FinalRefundRounded)
- **Výsledek**: Denní uzavírka správně odpovídá fyzickým penězům v pokladně

**3. UI: Kompletní zobrazení zaokrouhlení**
- **ProdejPage.xaml**: Zobrazuje přesnou částku + zaokrouhlení + finální částku k úhradě
- **ReceiptPreviewDialog**: Zobrazuje zaokrouhlení před tiskem
- **ESC/POS tisk**: Zobrazuje zaokrouhlení na účtence i dobropisu
  - `EscPosPrintService.cs` lines 709-755 (receipt)
  - `EscPosPrintService.cs` lines 1031-1066 (return)

**4. UX: PaymentSelectionDialog redesign**
- **Odebrána částka** - není potřeba, uživatel ji vidí na hlavní stránce
- **Moderní UI**: 2 velká tlačítka (140px) vedle sebe
- **Ikony**: 💰 Hotově (&#xE8CB;), 💳 Kartou (&#xE8C7;) - velikost 48px
- **Accent barva**: Plný accent background pro oba buttony
- **Zjednodušený kód**: Pouze výběr payment method, žádné amount handling

**5. UX: F1 keyboard shortcut**
- **Tlačítko "K Platbě"**: Přidán `<KeyboardAccelerator Key="F1" />`
- **Text updatován**: "K Platbě (F1)" - zobrazuje zkratku
- **Tooltip**: "Stiskněte F1 pro rychlé přechod k platbě"
- **Výsledek**: Rychlejší checkout workflow pro pokladní

**Upravené soubory:**
- `Models/Receipt.cs` - FinalAmountRounded, RoundingAmount, HasRounding, formatted properties
- `Models/Return.cs` - FinalRefundRounded, RefundRoundingAmount, HasRefundRounding
- `ViewModels/ProdejViewModel.cs` - AmountToPayRounded, RoundingDifference, HasRounding, formatted properties
- `Views/ProdejPage.xaml` - UI pro zaokrouhlení, F1 keyboard accelerator
- `Views/ProdejPage.xaml.cs` - používá AmountToPayRounded v payment dialozích
- `Views/Dialogs/PaymentSelectionDialog.xaml` - redesign bez částky
- `Views/Dialogs/PaymentSelectionDialog.xaml.cs` - simplified (bez amount)
- `Views/Dialogs/ReceiptPreviewDialog.xaml` - zobrazení zaokrouhlení
- `Services/EscPosPrintService.cs` - zaokrouhlení na tištěných účtenkách/dobropisy
- `Services/DailyCloseService.cs` - **KRITICKÁ OPRAVA** - 3 bugy s FinalAmountRounded/FinalRefundRounded

**Git:**
- Commit: (připraveno)
- Release: v1.0.20 (self-contained)

---

## 🎓 Klíčové naučené lekce

### Backup Protection Best Practices ⚠️

**1. Count-based detection je spolehlivější než size-based**
```csharp
// ❌ ŠPATNĚ - prázdná SQLite DB s tabulkami = ~140 KB (size check selže!)
if (dbSize < 50_000) // Nefunguje!

// ✅ SPRÁVNĚ - kontrola obsahu
int productCount = await context.Products.CountAsync();
int receiptCount = await context.Receipts.CountAsync();
bool isEmpty = (productCount == 0 && receiptCount == 0);
```

**2. Vrstevná ochrana - nejen prázdná DB**
- Check 1: Empty DB (0 produktů + 0 účtenek)
- Check 2: Velký pokles velikosti (> 50%)
- Check 3: Časový posun (stará záloha obnovena)
- Check 4: Částečná ztráta dat (> 5% záznamů)

**3. Read-only SQLite connection pro porovnání**
```csharp
var backupConnectionString = $"Data Source={backupPath};Mode=ReadOnly";
var backupOptions = new DbContextOptionsBuilder<DatabaseContext>()
    .UseSqlite(backupConnectionString)
    .Options;

using (var backupContext = new DatabaseContext(backupOptions))
{
    int backupCount = await backupContext.Products.AsNoTracking().CountAsync();
}
```

**4. User-friendly warnings s možností pokračovat**
- Default = bezpečná volba ("Ne, nezálohovat")
- Kontaktní info v každém kritickém dialogu
- Dvojí potvrzení před destruktivní operací
- Možnost manuální kontroly (SQLite Browser) mezi dialogy

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

**1. InfoBar pro moderní status messages**
```xaml
<InfoBar IsOpen="{x:Bind ViewModel.StatusMessage, Mode=OneWay, Converter={StaticResource StringToBoolConverter}}"
         Severity="{x:Bind ViewModel.IsError, Mode=OneWay, Converter={StaticResource BooleanToInfoBarSeverityConverter}}"
         Message="{x:Bind ViewModel.StatusMessage, Mode=OneWay}"
         IsClosable="True"
         Closed="InfoBar_Closed"/>
```
- Auto-dismiss s async Task.Delay
- Success vs Error severity
- Lepší UX než TextBlock + barvy

**2. ContentDialog Best Practices**
```csharp
// ✅ Multi-step confirmation
var firstResult = await warningDialog.ShowAsync();
if (firstResult == ContentDialogResult.Primary)
{
    // Extra confirmation for dangerous actions
    var confirmResult = await confirmDialog.ShowAsync();
    if (confirmResult == ContentDialogResult.Primary)
    {
        // Proceed
    }
}
```

**3. Clipboard API pro kopírování textu**
```csharp
using Windows.ApplicationModel.DataTransfer;

var dataPackage = new DataPackage();
dataPackage.SetText(textToCopy);
Clipboard.SetContent(dataPackage);
```

**4. HyperlinkButton pro klikatelný text**
```xaml
<HyperlinkButton Content="{x:Bind Ean}"
                 Click="EanButton_Click"
                 Padding="0"
                 ToolTipService.ToolTip="Klikněte pro zkopírování"/>
```

**5. Responzivní velikosti s MaxWidth/MaxHeight** ⚠️
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

---

## 📊 Aktuální stav projektu

**Hotovo:** 21/21 hlavních funkcí (~100%)

### ✅ Implementováno:
1. Role-based UI restrictions
2. Databáze produktů - **profesionální UI** (Brand/Category filtry, master-detail, klikatelné EAN, **responzivní obrázky**)
3. Status Bar (Informační panel)
4. Dashboard prodejů (KPI, top/worst produkty, platby, **opraveno - vratky + FinalAmountRounded**)
5. Denní otevírka/uzavírka pokladny
6. DPH systém (konfigurace)
7. Historie pokladny s filtry
8. Dynamická správa kategorií **+ Značek**
9. PPD Compliance (profesionální účtenky, storno, export FÚ)
10. UI optimalizace pro neplátce DPH
11. Vlastní cesta pro zálohy + Dialog při zavření + **4-vrstvá security ochrana**
12. Systém dárkových poukazů (kompletní, **více poukazů na účtence**)
13. **Auto-update systém** (multi-file ZIP, PowerShell, GitHub Releases)
14. **Tisk účtenek** (ESC/POS, české znaky CP852, Epson TM-T20III, **logo**)
15. **Single-instance ochrana** (Mutex, Win32 MessageBox)
16. **Marže produktů** (bidirektionální výpočet, editace pro admin)
17. **Obrázky produktů** (upload, thumbnail, resize, backup, **2000px kvalita**, **responzivní Viewbox**, **image cache fix**, **změna obrázku funguje**)
18. **Popis produktů + Master-Detail DatabazePage** (description, role-based edit, **TeachingTip EAN copy**)
19. **Export inventurního soupisu** (tisknutelná HTML + Excel CSV)
20. **Brand & Category management** (UI dialogy, schema V21, **profesionální filtry**)
21. **InfoBar UI pattern** (Věrnostní program + Nový produkt)

### ⏳ Zbývá:
- **DPH statistiky** - `TotalSalesAmountWithoutVat` nerespektuje slevy (věrnostní/poukaz) - PrehledProdejuViewModel:183-185

---

**Poslední aktualizace:** 4. prosinec 2025
**Aktuální verze:** v1.0.22 (schema V23)

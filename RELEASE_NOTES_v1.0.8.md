# Release Notes - v1.0.8

**Datum vydání:** 27. listopad 2025

## 🎨 Profesionální formátování účtenek

Kompletní redesign účtenek a dobropisů pro maximální přehlednost a profesionální vzhled.

### ✨ Nové funkce

#### **1. Logo na účtenkách**
- 🖼️ Podpora loga na začátku účtenky (místo názvu firmy)
- Automatická konverze color/grayscale → monochrome (threshold 128)
- Auto-scaling na max 384px šířku (optimální pro 80mm tiskárny)
- ESC/POS raster format (GS v 0) - RAW byte commands
- Fallback: Pokud logo chybí → tiskne se název firmy (2× velikost)
- Logo umístěno v: `essets/luvera_logo.bmp` (kopíruje se do output při buildu)

#### **2. Profesionální layout s tečkami**
- **Tečkované vyplnění** mezi cenou za kus a celkovou cenou:
  - `7x 100.00 Kč..............560.00 Kč`
- Lepší vizuální oddělení sloupců
- Aplikováno na: ceny produktů, Mezisoučet, Poukaz, Přijato, Vráceno

#### **3. Tenké čáry mezi položkami**
- Každá položka oddělena čarou `--------` (48 znaků)
- Lepší čitelnost při více položkách na účtence
- Aplikováno na účtenky i dobropisy/vratky

#### **4. Vycentrované info řádky**
- **Účtenka číslo, Datum, Prodejce** - vycentrované (místo vlevo)
- **Dobropis číslo, Datum, K původní účtence** - vycentrované
- Profesionálnější vzhled

#### **5. Optimalizovaná velikost CELKEM**
- **Odstraněn Double Height** (GS ! 0x10) - šetří místo
- **Pouze BOLD** (ESC E 1) - stále výrazné
- `*** CELKEM: 1000,00 Kč ***` se vejde celé na řádek
- Podpora částek až **9999,99 Kč** bez přetečení

#### **6. Symetrické odsazení**
- **Vlevo:** 3 mezery (1 prázdná + 2 vizuální jako "==")
- **Vpravo:** 3 mezery (1 prázdná + 2 vizuální jako "==")
- Efektivní šířka obsahu: **42 znaků** (z celkových 48)

#### **7. Správná šířka účtenky**
- **48 sloupců** (místo původních 42)
- Separátory plná šířka: `========` (48 znaků)
- Optimalizováno pro 80mm papír na Epson TM-T20III

#### **8. Word Wrap pro dlouhé názvy**
- Dlouhé názvy produktů se zalamují na více řádků (max 40 znaků)
- Příklad: "Produkt hodně dlouhým popiskem číslo 2 a 5 zelený"
  ```
  Produkt hodně dlouhým popiskem číslo 2
  a 5 zelený
  ```

#### **9. Přesun adresy/IČ/DIČ do footeru**
- **Adresa, IČ, DIČ** přesunuty z hlavičky do **footeru** (před "Děkujeme za nákup")
- Logo nahrazuje název firmy v hlavičce
- Čistší hlavička účtenky

### 🔧 Technické změny

#### **SkiaSharp integrace**
- Přidán using `SkiaSharp` pro načítání a konverzi loga
- Helper metody:
  - `LoadLogoCommands()` - načte BMP, konvertuje na ESC/POS formát
  - `WordWrap(text, maxWidth)` - zalomení dlouhých textů
  - `FormatLineWithRightPrice(left, right, width, useDots)` - formátování s tečkami/mezerami

#### **ESC/POS konstanty**
```csharp
RECEIPT_WIDTH = 48              // Celková šířka (80mm = 48 sloupců)
INDENT = "   "                  // 3 mezery vlevo
RIGHT_MARGIN = 3                // 3 mezery vpravo
EFFECTIVE_WIDTH = 42            // 48 - 3 - 3
MAX_PRODUCT_NAME_WIDTH = 40     // Max délka názvu před zalomením
```

#### **ESC/POS příkazy optimalizace**
- Logo: `GS v 0` (raster bit image)
- Vycentrování: `ESC a 1` (center align)
- Zarovnání vlevo: `ESC a 0` (left align)
- Bold: `ESC E 1` / `ESC E 0`
- Odstraněn Double Height u CELKEM (šetří místo)

### 📄 Vzorový layout účtenky

```
              [LOGO]

         Účtenka: U0008/2025
      Datum: 27.11.2025 14:20
      Prodejce: Administrátor
================================================
   Produkt hodně dlouhým popiskem číslo 2
   a 5 zelený, ve slevě
   7x 100.00 Kč -20%............560.00 Kč
------------------------------------------------
   Další produkt
   1x 50.00 Kč...................50.00 Kč
================================================

   Mezisoučet:..................610.00 Kč
   Použitý poukaz:..............-500.00 Kč
   EAN poukazu: 0004

         *** K ÚHRADĚ: 110.00 Kč ***

   Platba: Hotové + Dárkový poukaz
   Přijato:.....................150.00 Kč
   Vráceno:......................40.00 Kč

------------------------------------------------
              chvalovice
             IČ: 7865321

         Děkujeme za nákup!
```

### 🐛 Opravy

- ❌ Odstraněn nadpis "DÁRKOVÝ POUKAZ" (redundantní - položka už má název)
- ✅ CELKEM nyní skutečně na **středu** (chybějící ESC a 1)
- ✅ Fixnuty přetékající ceny při Double Height
- ✅ Separátory nyní plná šířka (48 znaků místo 32-40)

### 📦 Build změny

**Nové soubory:**
- `essets/luvera_logo.bmp` - logo firmy (kopíruje se do output)

**Upravené soubory:**
- `Services/EscPosPrintService.cs` - kompletní redesign formátování
- `Sklad_2.csproj` - Content Include pro logo

---

**Instalace:**
1. Stáhnout `Sklad_2-v1.0.8-win-x64.zip`
2. Rozbalit celou složku
3. Spustit `Sklad_2.exe`
4. Auto-update automaticky aktualizuje z předchozích verzí

**Požadavky:**
- Windows 10 build 19041+ (verze 2004)
- .NET 8.0 Runtime (zabaleno - self-contained)
- 80mm termální tiskárna (doporučeno: Epson TM-T20III)

**Kompatibilita:**
- Plně zpětně kompatibilní s v1.0.7
- Databáze beze změn (migrace není potřeba)
- Nastavení zachováno

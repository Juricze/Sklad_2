## 🔴 Windows 10 Compatibility Fixes

**Hlavní změny:**

### ✅ 6 kritických oprav pro Windows 10:
1. **FolderPicker fix** - Nastavení zálohovací cesty nyní funguje na Win10
2. **File flush** - Firemní údaje se ukládají spolehlivě
3. **StatusBar refresh** - Okamžitá aktualizace po uložení nastavení
4. **Database retry logika** - Oprava "problem při zapisování do databáze" při prodeji
5. **AsNoTracking()** - Prevence entity tracking conflicts
6. **Kategorie refresh** - Změny kategorií se okamžitě projeví v Nový produkt

### 🎯 Auto-update na přihlašovací obrazovce:
- Kontrola aktualizací hned při startu
- Zobrazení statusu (kontroluji, dostupná verze, progress)
- Možnost pokračovat bez update
- Verze aplikace zobrazena v footeru

### 📝 Dokumentace:
- Přidána sekce "Windows 10 Compatibility Requirements" do CLAUDE.md
- Checklist pro budoucí vývoj s Win10 kompatibilitou

### 🛠️ Technické detaily:
- File system flush (Win10 má pomalejší cache)
- SQLite retry logika (3× s exponential backoff)
- Timing delays pro Dispatcher priority
- Explicitní CurrentWindow nastavení

### 📦 Upravené soubory:
- `LoginWindow.xaml` - footer s verzí a update UI
- `LoginWindow.xaml.cs` - kompletní auto-update management
- `MainWindow.xaml.cs` - odstraněn UpdateService
- `SettingsService.cs` - přidán file flush
- `NastaveniViewModel.cs` - timing delays
- `SqliteDataService.cs` - AsNoTracking + retry logika
- `NovyProduktViewModel.cs` - RefreshCategories
- `DatabazeViewModel.cs` - RefreshCategories
- `CLAUDE.md` - Win10 compatibility guidelines
- `Sklad_2.csproj` - verze 1.0.2

---

**Kompatibilita:** Windows 10 build 19041+ a Windows 11
**Testováno na:** Win10 + Win11

**Poznámka:** Všechny změny jsou 100% kompatibilní s Win11. AsNoTracking() dokonce zrychlí databázové operace!

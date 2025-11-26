## 🔧 PowerShell Update Script - Robustní Opravy

**Fix pro selhávající auto-update z v1.0.4**

### 🛠️ Opravy Update Scriptu:

1. **Wait-Process** - čeká max 10 sekund na ukončení Sklad_2.exe
2. **Force Kill** - pokud proces neukončí sám, použije Stop-Process -Force
3. **Fix Substring Error** - normalizace cesty s trailing backslash
4. **Try-Catch v Foreach** - jeden chybný soubor nezabije celý update
5. **Ponechání update.log** - nemazat temp folder pro debugging
6. **Progress Logging** - každých 50 souborů
7. **Detailní Error Info** - line number, stack trace, cesty

### 🐛 Původní Problém:
- PowerShell script selhal při kopírování souborů
- Aplikace se nerestartovala správně
- Update.log byl smazán před přečtením

### ✅ Řešení:
- Proces Sklad_2.exe nyní spolehlivě ukončen před kopírováním
- Substring path calculation opravena (trailing backslash)
- Update.log zůstává pro debugging
- Better error handling s restore backup

---

**Testovací scénář:** Update z v1.0.4 → v1.0.5 by měl nyní proběhnout úspěšně.

**Kontrola úspěchu:**
- Verze v footeru ukazuje v1.0.5 ✓
- Update.log v %TEMP% ukazuje "UPDATE SUCCESSFUL" ✓
- Databáze a nastavení zachována ✓

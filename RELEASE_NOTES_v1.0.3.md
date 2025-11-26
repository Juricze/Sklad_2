## 🚀 Multi-file Auto-Updater + Win10 Fixes

**Hlavní změny:**

### ✨ Nový Multi-file Auto-Updater:
- **ZIP deployment** místo single .exe (rychlejší, spolehlivější)
- **Automatický backup** před aktualizací
- **Rollback mechanismus** při chybě
- **Detailní logging** - update.log pro troubleshooting
- **Smart skip** - nepřepisuje databázi a nastavení uživatele
- **PowerShell script** s profesionálním error handlingem

### 🔍 Debug & Logging:
- Step-by-step progress tracking (8 kroků)
- Detailní Debug output s [UpdateService] prefix
- ✓ úspěch, ❌ chyba, ⚠ varování formátování
- Progress bar při stahování
- Log file: `%TEMP%\Sklad_2_Update_XXX\update.log`

### 🛡️ Robustní Error Handling:
- Specifické catch bloky (HTTP, I/O, Access Denied)
- Stack traces pro debugging
- Automatic cleanup při chybě
- Verifikace stažených souborů

### 📦 Build Optimalizace:
- **87 → 6 jazykových složek** (ponecháno jen en-US, cs-CZ)
- **Menší velikost** release archivu
- **Rychlejší extrakce** při update

### 🔧 Opravy z v1.0.2:
- ✅ DIČ validace pouze pro plátce DPH
- ✅ NOT NULL constraints (VatId, RedeemedGiftCardEan)
- ✅ StatusBar validace vyžaduje IČ (CompanyId)
- ✅ FolderPicker funguje na Win10 (app.CurrentWindow fix)
- ✅ File flush pro Win10 kompatibilitu
- ✅ Database retry logika s exponential backoff
- ✅ AsNoTracking() prevence entity tracking conflicts

---

**Kompatibilita:** Windows 10 build 19041+ a Windows 11
**Testováno na:** Win10 + Win11

**⚠️ DŮLEŽITÉ:** První spuštění po update může trvat ~3 sekundy (PowerShell script cleanup).

**🧪 Test Release:** Tato verze je primárně pro testování nového auto-updater systému.

## 🖨️ Zvětšení názvu firmy na účtenkách

**Test release pro ověření auto-updateru v1.0.3 → v1.0.4**

### ✨ Nové funkce:
- **2× větší název firmy** na všech tiskových formách (účtenky, vratky, dobropis)
- ESC/POS příkaz `GS ! 0x30` - double height + double width
- Lepší viditelnost na tisknutých dokladech

### 🔧 Technické detaily:
- Změněno v `EscPosPrintService.cs`:
  - `BuildReceiptCommands()` - účtenky
  - `BuildReturnCommands()` - vratky/dobropis
- Předchozí: `GS ! 0x10` (pouze double height)
- Nově: `GS ! 0x30` (double height + double width)

---

**Účel:** Test release pro ověření multi-file auto-updater funkcionality z v1.0.3.

**Testovací scénář:**
1. Aplikace v1.0.3 nabídne update na v1.0.4
2. ZIP stažen a rozbalena
3. PowerShell script provede update
4. Aplikace se restartuje s v1.0.4
5. Zkontrolovat update.log v %TEMP%\Sklad_2_Update_XXX\

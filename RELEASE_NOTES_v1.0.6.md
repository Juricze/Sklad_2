## 📝 Aktualizace sekce "O aplikaci"

**Profesionální branding a kompletní přehled funkcí**

### ✨ Změny:

1. **Verze aplikace**
   - Dynamické načítání z assembly (nyní v1.0.6)
   - Vždy odpovídá aktuální verzi buildu

2. **Kontaktní informace**
   - Vytvořil: Jiří Hejda - Aplikárna®
   - Kontakt: info@aplikarna.cz
   - Web: aplikarna.cz (klikatelný odkaz → https://www.aplikarna.cz)
   - Copyright: Copyright © 2025 Jiří Hejda

3. **Popis aplikace**
   - Aktualizován na "Moderní POS systém pro Windows s kompletní správou skladu, prodeje, DPH a pokladny"
   - Zmíněny klíčové funkce: dárkové poukazy, vratky, zálohy, multi-user

4. **Hlavní funkce** (rozšířeno na 12 bodů)
   - ✓ Správa produktů a skladu s kategoriemi
   - ✓ POS systém s pokladnou (hotovost/karta)
   - ✓ Tisk účtenek na ESC/POS tiskárnách
   - ✓ Kompletní evidence DPH (plátce/neplátce)
   - ✓ Správa vratek a dobropisů
   - ✓ Dárkové poukazy (prodej a uplatnění)
   - ✓ Multi-user systém s rolemi
   - ✓ Denní otevírky a uzavírky pokladny
   - ✓ Dashboard prodejů s KPI a statistikami
   - ✓ Automatické zálohy databáze
   - ✓ Export účtenek pro Finanční úřad (HTML)
   - ✓ Automatické aktualizace z GitHubu

### 🎯 Účel release:

Test auto-update z v1.0.5 → v1.0.6 s novým UpdateService obsahujícím:
- UTF-8 BOM encoding fix (z v1.0.5)
- PowerShell script robustness (z v1.0.5)
- Auto cleanup temp složek (z v1.0.5)

---

**Testovací scénář:**
1. Aplikace v1.0.5 nabídne update na v1.0.6
2. Auto-update proběhne úspěšně s novým robustním UpdateService
3. Po restartu zkontrolovat verzi v patičce (v1.0.6)
4. Zkontrolovat "Nastavení → O aplikaci" - nové kontaktní info

**Očekávaný výsledek:**
- ✅ Update proběhne bez chyb
- ✅ Temp složky automaticky vyčištěny
- ✅ Update.log uložen pro debugging
- ✅ Všechny české znaky správně zobrazeny

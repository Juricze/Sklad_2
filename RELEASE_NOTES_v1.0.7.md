## 🔒 Single-Instance Ochrana Aplikace

**Zamezení současného běhu více instancí**

### ✨ Nová funkce:

**Single-Instance Protection**
- Aplikace může běžet pouze v jedné instanci současně
- Pokus o spuštění druhé instance zobrazí upozornění a ukončí se
- Používá system-wide Mutex pro spolehlivou detekci
- Win32 MessageBox pro okamžité zobrazení chyby (funguje před WinUI inicializací)

### 🛠️ Technické detaily:

1. **Mutex ochrana**
   - Unique název: `Sklad_2_SingleInstance_Mutex`
   - Vytvoření při startu aplikace
   - Automatické uvolnění při ukončení

2. **User-friendly feedback**
   - Varování: "Sklad 2 je již spuštěn"
   - Druhá instance se čistě ukončí
   - Žádné zamrzání nebo prázdná okna

3. **Důvody pro single-instance:**
   - Prevence konfliktů s SQLite databází
   - Ochrana před duplicitními záznamy
   - Lepší UX - uživatel nemusí řešit více oken

---

**Testovací scénář:**
1. Spusť aplikaci (první instance) ✅
2. Pokus o spuštění druhé instance
3. Měl by se objevit MessageBox: "Aplikace již běží"
4. Po kliknutí OK se druhá instance ukončí
5. První instance běží normálně dál

**Očekávaný výsledek:**
- ✅ Pouze jedna instance aplikace může běžet
- ✅ Clear user feedback při pokusu o druhé spuštění
- ✅ Žádné konflikty s databází

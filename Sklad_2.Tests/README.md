# Unit Testy pro Sklad_2

Tento projekt obsahuje unit testy pro kritické části aplikace Sklad_2.

## 🎯 Co testujeme

### ✅ Receipt Model (ReceiptTests.cs)
- **Zaokrouhlování** - FinalAmountRounded, RoundingAmount, HasRounding
- **Slevy** - věrnostní sleva, dárkové poukazy, kombinace obou
- **Edge cases** - nulové částky, velmi malé/velké částky
- **Komplexní scénáře** - reálné produkční případy

### ✅ Return Model (ReturnTests.cs)
- **Zaokrouhlování vratek** - FinalRefundRounded, RefundRoundingAmount
- **Věrnostní slevy** - poměrná část slevy při vratce
- **Edge cases** - boundary testy
- **Konzistence s Receipt** - DRY princip

## 🚀 Jak spustit testy

### Možnost 1: Visual Studio 2022 (DOPORUČENO)
1. Otevři solution `Sklad_2.sln` ve Visual Studio 2022
2. Otevři **Test Explorer** (Test → Test Explorer)
3. Klikni **Run All Tests** (nebo Ctrl+R, A)
4. Všechny testy by měly projít ✅

### Možnost 2: Příkazová řádka (.NET CLI)
```bash
cd Sklad_2.Tests
dotnet test
```

**Poznámka**: CLI může mít problémy s WinUI projekty na .NET SDK 9. Pokud selže, použij Visual Studio.

## 📊 Struktura testů

```
Sklad_2.Tests/
├── Models/
│   ├── ReceiptTests.cs      (19 testů)
│   └── ReturnTests.cs       (15 testů)
└── README.md (tento soubor)
```

## 🔍 Co testy ověřují

### KRITICKÉ výpočty (hlavní důvod existence testů):
1. **Matematické zaokrouhlování** (AwayFromZero) - 100.50 → 101, 100.49 → 100
2. **Správné odečítání slev** - věrnostní + dárkové poukazy
3. **Zaokrouhlování PO slevách** (ne před!) - kritické pro denní uzávěrku
4. **DRY princip** - výpočty pouze v Models, nikde jinde

### Příklad kritického testu:
```csharp
[Fact]
public void FinalAmountRounded_ComplexScenario_CorrectCalculation()
{
    // Reálný scénář: 1234.56 Kč - 123.46 Kč sleva - 200 Kč poukaz = 911.10 Kč → 911 Kč
    var receipt = new Receipt
    {
        TotalAmount = 1234.56m,
        LoyaltyDiscountAmount = 123.46m,
        GiftCardRedemptionAmount = 200m
    };

    Assert.Equal(911.10m, receipt.AmountToPay);
    Assert.Equal(911m, receipt.FinalAmountRounded); // Zaokrouhleno dolů
    Assert.Equal(-0.10m, receipt.RoundingAmount);
}
```

## ⚠️ Kdy spustit testy

### VŽDY před:
- ✅ Commitnutím změn v Models (Receipt, Return, CashRegisterEntry)
- ✅ Změnami ve výpočtech (zaokrouhlování, DPH, slevy)
- ✅ Vytvořením nového release

### Volitelně:
- Po změnách v Services (DailyCloseService, SqliteDataService)
- Po změnách v UI (ViewModels, Views) - unit testy netestují UI

## 📝 Přidání nových testů

Při implementaci nové funkce s finanční logikou:

1. Vytvoř nový test soubor v `Sklad_2.Tests/Models/` (nebo Services/)
2. Použij pattern:
   ```csharp
   using Sklad_2.Models;
   using Xunit;

   namespace Sklad_2.Tests.Models
   {
       public class MyNewTests
       {
           [Fact]
           public void TestName_Scenario_ExpectedResult()
           {
               // Arrange
               var obj = new MyClass { Property = value };

               // Act
               var result = obj.ComputedProperty;

               // Assert
               Assert.Equal(expected, result);
           }
       }
   }
   ```
3. Spusť testy (Visual Studio Test Explorer)
4. Commit pouze pokud všechny testy procházejí ✅

## 🎓 xUnit Cheat Sheet

- `[Fact]` - jeden test
- `[Theory]` + `[InlineData]` - parametrizované testy (více vstupů)
- `Assert.Equal(expected, actual)` - rovnost
- `Assert.True/False(bool)` - boolean
- `Assert.Throws<TException>(() => code)` - očekává výjimku

## 🔗 Další info

- [xUnit dokumentace](https://xunit.net/)
- [xUnit assertions](https://xunit.net/docs/comparisons)
- CLAUDE.md - workflow pro unit testy při vývoji

namespace Sklad_2.Models
{
    public enum StockMovementType
    {
        ProductCreated,      // Vytvoření nového produktu
        StockIn,            // Naskladnění
        Sale,               // Prodej
        Return,             // Vratka (přidání zpět na sklad)
        Adjustment,         // Ruční úprava stavu
        WriteOffTester,     // Odpis - Tester
        WriteOffDamaged     // Odpis - Poškozené/Expirované/Jiné
    }
}

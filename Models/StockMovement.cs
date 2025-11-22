using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.ComponentModel.DataAnnotations;

namespace Sklad_2.Models
{
    public partial class StockMovement : ObservableObject
    {
        [Key]
        [ObservableProperty]
        private int id;

        [ObservableProperty]
        private string productEan = string.Empty;

        [ObservableProperty]
        private string productName = string.Empty;

        [ObservableProperty]
        private StockMovementType movementType;

        [ObservableProperty]
        private int quantityChange;

        [ObservableProperty]
        private int stockBefore;

        [ObservableProperty]
        private int stockAfter;

        [ObservableProperty]
        private DateTime timestamp = DateTime.Now;

        [ObservableProperty]
        private string userName = string.Empty;

        [ObservableProperty]
        private string notes = string.Empty;

        // Reference ID pro napojení na související záznamy (např. ReceiptId)
        [ObservableProperty]
        private int? referenceId;

        // Formatted properties pro UI
        public string TimestampFormatted => Timestamp.ToString("dd.MM.yyyy HH:mm:ss");
        public string MovementTypeFormatted => MovementType switch
        {
            StockMovementType.ProductCreated => "Nový produkt",
            StockMovementType.StockIn => "Naskladnění",
            StockMovementType.Sale => "Prodej",
            StockMovementType.Return => "Vratka",
            StockMovementType.Adjustment => "Úprava",
            _ => MovementType.ToString()
        };

        public string QuantityChangeFormatted
        {
            get
            {
                if (QuantityChange > 0)
                    return $"+{QuantityChange} ks";
                else
                    return $"{QuantityChange} ks";
            }
        }

        public bool IsPositiveChange => QuantityChange > 0;
        public bool IsNegativeChange => QuantityChange < 0;
    }
}

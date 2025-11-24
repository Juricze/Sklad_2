using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Sklad_2.Models
{
    public partial class ReceiptItem : ObservableObject
    {
        [Key]
        [ObservableProperty]
        private int receiptItemId;

        [ObservableProperty]
        private int receiptId;

        [ObservableProperty]
        [ForeignKey("ReceiptId")]
        private Receipt receipt;

        [ObservableProperty]
        private string productEan;

        [ObservableProperty]
        private string productName;

        [ObservableProperty]
        private int quantity;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(UnitPriceFormatted))]
        private decimal unitPrice; // Price with VAT

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TotalPriceFormatted))]
        private decimal totalPrice; // Price with VAT

        // New properties for VAT
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(VatRateFormatted))]
        private decimal vatRate;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PriceWithoutVatFormatted))]
        private decimal priceWithoutVat;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(VatAmountFormatted))]
        private decimal vatAmount;

        // Discount properties
        [ObservableProperty]
        private decimal? discountPercent;

        [ObservableProperty]
        private decimal originalUnitPrice = 0; // Original price before discount

        [ObservableProperty]
        private string discountReason = string.Empty;

        // Computed properties
        public bool HasDiscount => DiscountPercent.HasValue && DiscountPercent > 0;
        public decimal DiscountAmount => HasDiscount ? (OriginalUnitPrice - UnitPrice) : 0;
        public decimal TotalDiscountAmount => DiscountAmount * Quantity;
        public string DiscountPercentFormatted => DiscountPercent.HasValue ? $"-{DiscountPercent:F0}%" : "";

        // Formatted properties
        public string UnitPriceFormatted => $"{UnitPrice:C}";
        public string OriginalUnitPriceFormatted => $"{OriginalUnitPrice:C}";
        public string TotalPriceFormatted => $"{TotalPrice:C}";
        public string PriceWithoutVatFormatted => $"{PriceWithoutVat:C}";
        public string VatAmountFormatted => $"{VatAmount:C}";
        public string VatRateFormatted => $"{VatRate} %";
    }
}
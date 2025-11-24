using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.ComponentModel.DataAnnotations;

namespace Sklad_2.Models
{
    public partial class Product : ObservableObject
    {
        [Key]
        [ObservableProperty]
        private string ean;

        [ObservableProperty]
        private string name;

        [ObservableProperty]
        private string category;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SalePriceFormatted))]
        private decimal salePrice;

        [ObservableProperty]
        private decimal purchasePrice;

        [ObservableProperty]
        private decimal vatRate;

        [ObservableProperty]
        private int stockQuantity;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FinalPrice))]
        [NotifyPropertyChangedFor(nameof(IsDiscounted))]
        [NotifyPropertyChangedFor(nameof(DiscountPercentFormatted))]
        private decimal? discountPercent;

        [ObservableProperty]
        private DateTime? discountValidFrom;

        [ObservableProperty]
        private DateTime? discountValidTo;

        [ObservableProperty]
        private string discountReason = string.Empty;

        [ObservableProperty]
        private string testField = string.Empty;

        public string SalePriceFormatted => $"{SalePrice:C}";

        public string PurchasePriceFormatted => $"{PurchasePrice:C}";

        public string VatRateFormatted => $"{VatRate} %";

        // Computed properties for discounts
        public bool IsDiscounted => DiscountPercent.HasValue && DiscountPercent > 0 && IsDiscountValid();

        public decimal FinalPrice => IsDiscounted ? SalePrice * (1 - (DiscountPercent.Value / 100)) : SalePrice;

        public string FinalPriceFormatted => $"{FinalPrice:C}";

        public string DiscountPercentFormatted => DiscountPercent.HasValue ? $"-{DiscountPercent:F0}%" : "";

        private bool IsDiscountValid()
        {
            var now = DateTime.Now.Date;
            var validFrom = DiscountValidFrom?.Date ?? DateTime.MinValue;
            var validTo = DiscountValidTo?.Date ?? DateTime.MaxValue;
            return now >= validFrom && now <= validTo;
        }
    }
}
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
        private string description = string.Empty;

        // ===== DEPRECATED (zachovat pro backwards compatibility) =====
        /// <summary>
        /// DEPRECATED: Použijte ProductCategoryId a CategoryName.
        /// Zachováno pro backwards compatibility s ProdejViewModel, VatConfig, atd.
        /// </summary>
        [ObservableProperty]
        private string category;

        // ===== NOVÉ (moderní FK přístup) =====
        /// <summary>
        /// FK na tabulku Brand (nullable pro zpětnou kompatibilitu)
        /// </summary>
        [ObservableProperty]
        private int? brandId;

        /// <summary>
        /// FK na tabulku ProductCategory (nullable pro zpětnou kompatibilitu)
        /// </summary>
        [ObservableProperty]
        private int? productCategoryId;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SalePriceFormatted))]
        private decimal salePrice;

        [ObservableProperty]
        private decimal purchasePrice;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(MarkupFormatted))]
        private decimal markup;

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

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasImage))]
        private string imagePath = string.Empty;

        /// <summary>
        /// Returns true if the product has an image assigned
        /// </summary>
        public bool HasImage => !string.IsNullOrEmpty(ImagePath);

        public string SalePriceFormatted => $"{SalePrice:C}";

        public string PurchasePriceFormatted => $"{PurchasePrice:C}";

        public string MarkupFormatted => $"{Markup:N0} %";

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

        // ===== Navigation properties (EF Core) =====
        /// <summary>
        /// Navigace na entitu Brand
        /// </summary>
        public Brand Brand { get; set; }

        /// <summary>
        /// Navigace na entitu ProductCategory
        /// </summary>
        public ProductCategory ProductCategory { get; set; }

        // ===== Helper properties pro UI =====
        /// <summary>
        /// Název značky (nebo prázdný string pokud není nastavena)
        /// </summary>
        public string BrandName => Brand?.Name ?? string.Empty;

        /// <summary>
        /// Název kategorie s fallbackem na deprecated Category string
        /// </summary>
        public string CategoryName => ProductCategory?.Name ?? Category ?? string.Empty;
    }
}
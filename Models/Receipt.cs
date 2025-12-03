using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;

namespace Sklad_2.Models
{
    public partial class Receipt : ObservableObject
    {
        [Key]
        [ObservableProperty]
        private int receiptId;

        // Receipt numbering - format: 2025/0001
        [ObservableProperty]
        private int receiptYear;  // Year of the receipt

        [ObservableProperty]
        private int receiptSequence;  // Sequential number within the year (1, 2, 3...)

        public string FormattedReceiptNumber => $"U{ReceiptSequence:D4}/{ReceiptYear}";  // Format: U0001/2025

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SaleDateFormatted))]
        private DateTime saleDate;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TotalAmountFormatted))]
        [NotifyPropertyChangedFor(nameof(AmountToPay))]
        [NotifyPropertyChangedFor(nameof(AmountToPayFormatted))]
        private decimal totalAmount; // Total with VAT

        [ObservableProperty]
        private string paymentMethod;

        // Payment breakdown for daily close tracking
        [ObservableProperty]
        private decimal cashAmount;  // Částka zaplacená v hotovosti

        [ObservableProperty]
        private decimal cardAmount;  // Částka zaplacená kartou

        // Seller info at the time of sale
        [ObservableProperty]
        private string shopName;

        [ObservableProperty]
        private string shopAddress;

        [ObservableProperty]
        private string sellerName;  // Who performed the sale (Admin, Prodej, or specific seller name)

        [ObservableProperty]
        private string companyId;

        [ObservableProperty]
        private string vatId;

        [ObservableProperty]
        private bool isVatPayer;

        // VAT info
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TotalAmountWithoutVatFormatted))]
        private decimal totalAmountWithoutVat;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TotalVatAmountFormatted))]
        private decimal totalVatAmount;

        // Formatted properties
        public string TotalAmountFormatted => $"{TotalAmount:C}";
        public string SaleDateFormatted => $"{SaleDate:g}";
        public string TotalAmountWithoutVatFormatted => $"{TotalAmountWithoutVat:C}";
        public string TotalVatAmountFormatted => $"{TotalVatAmount:C}";

        // Storno display properties
        public string DisplayReceiptId => IsStorno && OriginalReceiptId.HasValue
            ? $"STORNO {FormattedReceiptNumber}"
            : FormattedReceiptNumber;

        public string StornoIndicator => IsStorno ? "❌ " : "";

        [ObservableProperty]
        private decimal receivedAmount;

        [ObservableProperty]
        private decimal changeAmount;

        public string ReceivedAmountFormatted => $"{ReceivedAmount:C}";
        public string ChangeAmountFormatted => $"{ChangeAmount:C}";

        // Storno fields
        [ObservableProperty]
        private bool isStorno;

        [ObservableProperty]
        private int? originalReceiptId; // Reference to cancelled receipt

        // Gift Card fields
        [ObservableProperty]
        private bool containsGiftCardSale; // True if this receipt includes gift card sale(s)

        [ObservableProperty]
        private decimal giftCardSaleAmount; // Total value of gift cards sold on this receipt

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AmountToPay))]
        [NotifyPropertyChangedFor(nameof(AmountToPayFormatted))]
        private bool containsGiftCardRedemption; // True if gift card was used as payment

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(GiftCardRedemptionAmountFormatted))]
        [NotifyPropertyChangedFor(nameof(AmountToPay))]
        [NotifyPropertyChangedFor(nameof(AmountToPayFormatted))]
        private decimal giftCardRedemptionAmount; // Total value of gift cards redeemed on this receipt

        public string GiftCardRedemptionAmountFormatted => $"{GiftCardRedemptionAmount:C}";

        // DEPRECATED: Kept for backwards compatibility with old receipts and dialogs
        // New receipts use RedeemedGiftCards navigation property instead
        [ObservableProperty]
        private string redeemedGiftCardEan = string.Empty;

        // Navigation property - více uplatněných poukazů
        public ICollection<ReceiptGiftCardRedemption> RedeemedGiftCards { get; set; } = new List<ReceiptGiftCardRedemption>();

        // Loyalty program fields
        [ObservableProperty]
        private bool hasLoyaltyDiscount; // True if loyalty discount was applied

        [ObservableProperty]
        private int? loyaltyCustomerId; // ID of the loyalty customer (for storno TotalPurchases update)

        [ObservableProperty]
        private string loyaltyCustomerContact = string.Empty; // Masked contact (email NEBO telefon, např. pav***@***.cz nebo +420 7396*****)

        [ObservableProperty]
        private decimal loyaltyDiscountPercent; // Percent of discount applied (0-30)

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(LoyaltyDiscountAmountFormatted))]
        [NotifyPropertyChangedFor(nameof(AmountToPay))]
        [NotifyPropertyChangedFor(nameof(AmountToPayFormatted))]
        private decimal loyaltyDiscountAmount; // Actual discount amount in CZK

        public string LoyaltyDiscountAmountFormatted => LoyaltyDiscountAmount > 0 ? $"-{LoyaltyDiscountAmount:C}" : string.Empty;

        /// <summary>
        /// True pokud je vyplněn kontakt věrnostního zákazníka (email nebo telefon)
        /// </summary>
        public bool HasLoyaltyCustomerContact => !string.IsNullOrEmpty(LoyaltyCustomerContact);

        /// <summary>
        /// True pokud byla aplikována jakákoliv sleva (věrnostní nebo poukaz)
        /// </summary>
        public bool HasAnyDiscount => HasLoyaltyDiscount || ContainsGiftCardRedemption;

        /// <summary>
        /// Částka k úhradě po odečtení věrnostní slevy a dárkového poukazu (PŘESNÁ hodnota s haléři)
        /// </summary>
        public decimal AmountToPay => TotalAmount - LoyaltyDiscountAmount - (ContainsGiftCardRedemption ? GiftCardRedemptionAmount : 0);

        /// <summary>
        /// Matematické zaokrouhlení částky k úhradě na celé koruny (0,50 Kč a více nahoru, méně než 0,50 dolů)
        /// </summary>
        public decimal FinalAmountRounded => Math.Round(AmountToPay, 0, MidpointRounding.AwayFromZero);

        /// <summary>
        /// Rozdíl zaokrouhlení (pro evidenci a tisk na účtence)
        /// Kladná hodnota = zaokrouhleno nahoru, záporná = zaokrouhleno dolů
        /// </summary>
        public decimal RoundingAmount => FinalAmountRounded - AmountToPay;

        /// <summary>
        /// True pokud existuje rozdíl zaokrouhlení (není 0)
        /// </summary>
        public bool HasRounding => RoundingAmount != 0;

        public string AmountToPayFormatted => $"{AmountToPay:C}";

        /// <summary>
        /// Formátovaná finální částka k úhradě (zaokrouhlená na celé koruny)
        /// </summary>
        public string FinalAmountRoundedFormatted => $"{FinalAmountRounded:N0} Kč";

        /// <summary>
        /// Formátovaný rozdíl zaokrouhlení s označením +/- (pro tisk na účtence)
        /// </summary>
        public string RoundingAmountFormatted => RoundingAmount >= 0
            ? $"+{RoundingAmount:F2} Kč"
            : $"{RoundingAmount:F2} Kč";

        [ObservableProperty]
        private ObservableCollection<ReceiptItem> items;
    }
}
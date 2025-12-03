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

        [ObservableProperty]
        private string redeemedGiftCardEan = string.Empty; // EAN of the gift card used for payment

        public string GiftCardRedemptionAmountFormatted => $"{GiftCardRedemptionAmount:C}";

        /// <summary>
        /// Částka k úhradě po odečtení dárkového poukazu (pro zobrazení v seznamech)
        /// </summary>
        public decimal AmountToPay => TotalAmount - (ContainsGiftCardRedemption ? GiftCardRedemptionAmount : 0);

        public string AmountToPayFormatted => $"{AmountToPay:C}";

        [ObservableProperty]
        private ObservableCollection<ReceiptItem> items;
    }
}
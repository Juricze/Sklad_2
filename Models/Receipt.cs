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

        public string FormattedReceiptNumber => $"{ReceiptYear}/{ReceiptSequence:D4}";  // Format: 2025/0001

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SaleDateFormatted))]
        private DateTime saleDate;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TotalAmountFormatted))]
        private decimal totalAmount; // Total with VAT

        [ObservableProperty]
        private string paymentMethod;

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

        [ObservableProperty]
        private ObservableCollection<ReceiptItem> items;
    }
}
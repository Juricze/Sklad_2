using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;

namespace Sklad_2.Models
{
    public partial class Return : ObservableObject
    {
        [Key]
        [ObservableProperty]
        private int returnId;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FormattedReturnNumber))]
        private int returnYear;  // Year of return (e.g., 2025)

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FormattedReturnNumber))]
        private int returnSequence;  // Sequential number within the year (1, 2, 3...)

        public string FormattedReturnNumber => $"D{ReturnSequence:D4}/{ReturnYear}";  // Format: D0001/2025

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ReturnDateFormatted))]
        private DateTime returnDate;

        // Reference to the original receipt
        [ObservableProperty]
        private int originalReceiptId;

        // Seller info at the time of return
        [ObservableProperty]
        private string shopName;

        [ObservableProperty]
        private string shopAddress;

        [ObservableProperty]
        private string companyId;

        [ObservableProperty]
        private string vatId;

        [ObservableProperty]
        private bool isVatPayer;

        // Refund totals
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TotalRefundAmountFormatted))]
        private decimal totalRefundAmount;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TotalRefundAmountWithoutVatFormatted))]
        private decimal totalRefundAmountWithoutVat;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TotalRefundVatAmountFormatted))]
        private decimal totalRefundVatAmount;

        // Formatted properties
        public string TotalRefundAmountFormatted => $"{TotalRefundAmount:C}";
        public string TotalRefundAmountWithoutVatFormatted => $"{TotalRefundAmountWithoutVat:C}";
        public string TotalRefundVatAmountFormatted => $"{TotalRefundVatAmount:C}";
        public string ReturnDateFormatted => $"{ReturnDate:g}";

        [ObservableProperty]
        private ObservableCollection<ReturnItem> items;
    }
}

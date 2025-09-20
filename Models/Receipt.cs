using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Sklad_2.Models
{
    public partial class Receipt : ObservableObject
    {
        [Key]
        [ObservableProperty]
        private int receiptId;

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
        public string SaleDateFormatted => $"{SaleDate.Date:d}";
        public string TotalAmountWithoutVatFormatted => $"{TotalAmountWithoutVat:C}";
        public string TotalVatAmountFormatted => $"{TotalVatAmount:C}";

        [ObservableProperty]
        private ICollection<ReceiptItem> items;
    }
}
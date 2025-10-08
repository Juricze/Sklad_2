using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Sklad_2.Models
{
    public partial class ReturnItem : ObservableObject
    {
        [Key]
        [ObservableProperty]
        private int returnItemId;

        [ObservableProperty]
        private int returnId;
        [ForeignKey("ReturnId")]
        [ObservableProperty]
        private Return @return;

        // Info about the returned product
        [ObservableProperty]
        private string productEan;
        [ObservableProperty]
        private string productName;
        [ObservableProperty]
        private int returnedQuantity;

        // Financial info for this item
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(UnitPriceFormatted))]
        private decimal unitPrice; // Price with VAT
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TotalRefundFormatted))]
        private decimal totalRefund; // Total refund for this line
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(VatRateFormatted))]
        private decimal vatRate;
        [ObservableProperty]
        private decimal priceWithoutVat;
        [ObservableProperty]
        private decimal vatAmount; // The VAT amount being refunded for this line

        // Formatted properties
        public string UnitPriceFormatted => $"{UnitPrice:C}";
        public string TotalRefundFormatted => $"{TotalRefund:C}";
        public string VatRateFormatted => $"{VatRate} %";
    }
}

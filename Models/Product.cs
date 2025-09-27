using CommunityToolkit.Mvvm.ComponentModel;
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

        public string SalePriceFormatted => $"{SalePrice:C}";

        public string VatRateFormatted => $"{vatRate} %";
    }
}
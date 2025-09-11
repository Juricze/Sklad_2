using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sklad_2.Models;
using Sklad_2.Services;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace Sklad_2.ViewModels
{
    public partial class ProdejViewModel : ObservableObject
    {
        private readonly IDataService _dataService;

        

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsProductFound))]
        private Product scannedProduct;

        public bool IsProductFound => ScannedProduct != null;

        public ObservableCollection<Product> ReceiptItems { get; } = new ObservableCollection<Product>();

        public ProdejViewModel(IDataService dataService)
        {
            _dataService = dataService;
        }

        [RelayCommand]
        private async Task FindProductAsync(string eanCodeParam)
        {
            if (string.IsNullOrWhiteSpace(eanCodeParam)) return;
            ScannedProduct = await _dataService.GetProductAsync(eanCodeParam);
            if (ScannedProduct != null)
            {
                AddToReceipt();
            }
        }

        [RelayCommand]
        private void AddToReceipt()
        {
            if (ScannedProduct == null) return;
            ReceiptItems.Add(ScannedProduct);
            // EanCode = string.Empty;
            // ScannedProduct = null;
        }
    }
}

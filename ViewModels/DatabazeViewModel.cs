using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sklad_2.Models;
using Sklad_2.Services;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Sklad_2.ViewModels
{
    public partial class DatabazeViewModel : ObservableObject
    {
        private readonly IDataService _dataService;
        private readonly IAuthService _authService;
        private List<Product> _allProducts = new List<Product>();

        [ObservableProperty]
        private bool isSalesRole;

        public ObservableCollection<Product> FilteredProducts { get; } = new ObservableCollection<Product>();

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(DeleteProductCommand))]
        private Product selectedProduct;

        [ObservableProperty]
        private string searchText;

        public DatabazeViewModel(IDataService dataService, IAuthService authService)
        {
            _dataService = dataService;
            _authService = authService;
            IsSalesRole = _authService.CurrentRole == "Prodej";
        }

        [RelayCommand]
        private async Task LoadProductsAsync()
        {
            _allProducts = await _dataService.GetProductsAsync();
            FilterProducts();
        }

        partial void OnSearchTextChanged(string value)
        {
            FilterProducts();
        }

        private void FilterProducts()
        {
            FilteredProducts.Clear();
            IEnumerable<Product> filtered = _allProducts;

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                filtered = filtered.Where(p => 
                    p.Name.Contains(SearchText, System.StringComparison.OrdinalIgnoreCase) || 
                    p.Ean.Contains(SearchText, System.StringComparison.OrdinalIgnoreCase));
            }

            foreach (var product in filtered)
            {
                FilteredProducts.Add(product);
            }
        }

        private bool CanDeleteProduct() => SelectedProduct != null;

        [RelayCommand(CanExecute = nameof(CanDeleteProduct))]
        private async Task DeleteProductAsync()
        {
            // Zde bude v budoucnu potvrzovací dialog
            await _dataService.DeleteProductAsync(SelectedProduct.Ean);
            await LoadProductsAsync(); // Refresh list
        }
    }
}
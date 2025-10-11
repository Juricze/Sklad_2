using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sklad_2.Models;
using Sklad_2.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Sklad_2.ViewModels
{
    public enum SortColumn
    {
        None,
        Name,
        StockQuantity,
        SalePrice
    }

    public enum SortDirection
    {
        Ascending,
        Descending
    }

    public partial class DatabazeViewModel : ObservableObject
    {
        private readonly IDataService _dataService;
        private readonly IAuthService _authService;
        private List<Product> _allProducts = new List<Product>();

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(DeleteProductCommand))]
        private bool isSalesRole;

        public ObservableCollection<Product> FilteredProducts { get; } = new ObservableCollection<Product>();
        public ObservableCollection<string> Categories { get; } = new ObservableCollection<string>();

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(DeleteProductCommand))]
        private Product selectedProduct;

        [ObservableProperty]
        private string searchText;

        [ObservableProperty]
        private string selectedCategory;

        private SortColumn _currentSortColumn = SortColumn.None;
        private SortDirection _currentSortDirection = SortDirection.Ascending;

        public DatabazeViewModel(IDataService dataService, IAuthService authService)
        {
            _dataService = dataService;
            _authService = authService;
            IsSalesRole = _authService.CurrentRole == "Prodej";

            // Initialize categories
            Categories.Add("Vše");
            foreach (var category in ProductCategories.All)
            {
                Categories.Add(category);
            }
            SelectedCategory = "Vše";
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

        partial void OnSelectedCategoryChanged(string value)
        {
            FilterProducts();
        }

        private void FilterProducts()
        {
            FilteredProducts.Clear();
            IEnumerable<Product> filtered = _allProducts;

            // Filter by search text
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                filtered = filtered.Where(p =>
                    p.Name.StartsWith(SearchText, System.StringComparison.OrdinalIgnoreCase) ||
                    p.Ean.StartsWith(SearchText, System.StringComparison.OrdinalIgnoreCase));
            }

            // Filter by category
            if (!string.IsNullOrWhiteSpace(SelectedCategory) && SelectedCategory != "Vše")
            {
                filtered = filtered.Where(p => p.Category == SelectedCategory);
            }

            // Apply sorting
            filtered = ApplySorting(filtered);

            foreach (var product in filtered)
            {
                FilteredProducts.Add(product);
            }
        }

        private IEnumerable<Product> ApplySorting(IEnumerable<Product> products)
        {
            return _currentSortColumn switch
            {
                SortColumn.Name => _currentSortDirection == SortDirection.Ascending
                    ? products.OrderBy(p => p.Name)
                    : products.OrderByDescending(p => p.Name),
                SortColumn.StockQuantity => _currentSortDirection == SortDirection.Ascending
                    ? products.OrderBy(p => p.StockQuantity)
                    : products.OrderByDescending(p => p.StockQuantity),
                SortColumn.SalePrice => _currentSortDirection == SortDirection.Ascending
                    ? products.OrderBy(p => p.SalePrice)
                    : products.OrderByDescending(p => p.SalePrice),
                _ => products
            };
        }

        [RelayCommand]
        private void SortBy(string columnName)
        {
            var newColumn = columnName switch
            {
                "Name" => SortColumn.Name,
                "StockQuantity" => SortColumn.StockQuantity,
                "SalePrice" => SortColumn.SalePrice,
                _ => SortColumn.None
            };

            if (newColumn == _currentSortColumn)
            {
                // Toggle direction
                _currentSortDirection = _currentSortDirection == SortDirection.Ascending
                    ? SortDirection.Descending
                    : SortDirection.Ascending;
            }
            else
            {
                _currentSortColumn = newColumn;
                _currentSortDirection = SortDirection.Ascending;
            }

            FilterProducts();
        }

        private bool CanDeleteProduct() => SelectedProduct != null && !IsSalesRole;

        [RelayCommand(CanExecute = nameof(CanDeleteProduct))]
        private async Task DeleteProductAsync()
        {
            // Zde bude v budoucnu potvrzovací dialog
            await _dataService.DeleteProductAsync(SelectedProduct.Ean);
            await LoadProductsAsync(); // Refresh list
        }
    }
}
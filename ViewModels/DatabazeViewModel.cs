using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml.Media.Imaging;
using Sklad_2.Messages;
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
        private readonly ISettingsService _settingsService;
        private readonly IMessenger _messenger;
        private readonly IProductImageService _imageService;
        private List<Product> _allProducts = new List<Product>();

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(DeleteProductCommand))]
        [NotifyCanExecuteChangedFor(nameof(EditProductCommand))]
        private bool isSalesRole;

        public bool IsVatPayer => _settingsService.CurrentSettings.IsVatPayer;
        public bool IsAdmin => _authService.CurrentRole == "Admin";
        public bool IsSalesOrAdmin => _authService.CurrentRole == "Cashier" || _authService.CurrentRole == "Admin";

        /// <summary>
        /// Returns the full image of the selected product (for detail view)
        /// </summary>
        public BitmapImage SelectedProductImage => SelectedProduct != null && SelectedProduct.HasImage
            ? _imageService?.GetImage(SelectedProduct.Ean)
            : null;

        /// <summary>
        /// Returns true if a product is selected (for detail panel visibility)
        /// </summary>
        public bool IsProductSelected => SelectedProduct != null;

        public ObservableCollection<Product> FilteredProducts { get; } = new ObservableCollection<Product>();
        public ObservableCollection<string> Categories { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> Brands { get; } = new ObservableCollection<string>();

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(DeleteProductCommand))]
        [NotifyCanExecuteChangedFor(nameof(EditProductCommand))]
        [NotifyPropertyChangedFor(nameof(SelectedProductImage))]
        [NotifyPropertyChangedFor(nameof(IsProductSelected))]
        private Product selectedProduct;

        [ObservableProperty]
        private string searchText;

        [ObservableProperty]
        private string selectedCategory;

        [ObservableProperty]
        private string selectedBrand;

        private SortColumn _currentSortColumn = SortColumn.None;
        private SortDirection _currentSortDirection = SortDirection.Ascending;

        public DatabazeViewModel(IDataService dataService, IAuthService authService, ISettingsService settingsService, IMessenger messenger, IProductImageService imageService)
        {
            _dataService = dataService;
            _authService = authService;
            _settingsService = settingsService;
            _messenger = messenger;
            _imageService = imageService;
            IsSalesRole = _authService.CurrentRole == "Cashier";

            // Initialize categories
            Categories.Add("Vše");
            foreach (var category in ProductCategories.All)
            {
                Categories.Add(category);
            }
            SelectedCategory = "Vše";

            // Initialize brands
            Brands.Add("Vše");
            SelectedBrand = "Vše";

            // Listen for settings changes to update IsVatPayer property
            _messenger.Register<SettingsChangedMessage>(this, (r, m) =>
            {
                OnPropertyChanged(nameof(IsVatPayer));
            });

            // Listen for category/brand changes (Win10 fix)
            _messenger.Register<VatConfigsChangedMessage>(this, async (r, m) =>
            {
                // Small delay for file system flush
                await Task.Delay(100);
                RefreshCategories();
                await RefreshBrandsAsync();
            });
        }

        private void RefreshCategories()
        {
            // Win10 fix: Reload categories from ProductCategories.All
            var currentSelection = SelectedCategory;
            Categories.Clear();
            Categories.Add("Vše");

            foreach (var category in ProductCategories.All)
            {
                Categories.Add(category);
            }

            // Restore selection if it still exists, otherwise select "Vše"
            if (Categories.Contains(currentSelection))
            {
                SelectedCategory = currentSelection;
            }
            else
            {
                SelectedCategory = "Vše";
            }
        }

        private async Task RefreshBrandsAsync()
        {
            // Load brands from database
            var brandsFromDb = await _dataService.GetBrandsAsync();
            var currentSelection = SelectedBrand;

            Brands.Clear();
            Brands.Add("Vše");

            foreach (var brand in brandsFromDb)
            {
                Brands.Add(brand.Name);
            }

            // Restore selection if it still exists, otherwise select "Vše"
            if (Brands.Contains(currentSelection))
            {
                SelectedBrand = currentSelection;
            }
            else
            {
                SelectedBrand = "Vše";
            }
        }

        [RelayCommand]
        private async Task LoadProductsAsync()
        {
            _allProducts = await _dataService.GetProductsAsync();
            await RefreshBrandsAsync();
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

        partial void OnSelectedBrandChanged(string value)
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

            // Filter by brand
            if (!string.IsNullOrWhiteSpace(SelectedBrand) && SelectedBrand != "Vše")
            {
                // Use BrandName helper property for backwards compatibility
                filtered = filtered.Where(p => p.BrandName == SelectedBrand);
            }

            // Filter by category
            if (!string.IsNullOrWhiteSpace(SelectedCategory) && SelectedCategory != "Vše")
            {
                // Use CategoryName helper property (ProductCategory.Name ?? Category) for backwards compatibility
                filtered = filtered.Where(p => p.CategoryName == SelectedCategory);
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
        private bool CanEditProduct() => SelectedProduct != null && IsAdmin;

        [RelayCommand(CanExecute = nameof(CanDeleteProduct))]
        private async Task DeleteProductAsync()
        {
            // Zde bude v budoucnu potvrzovací dialog
            await _dataService.DeleteProductAsync(SelectedProduct.Ean);
            await LoadProductsAsync(); // Refresh list
        }

        [RelayCommand(CanExecute = nameof(CanEditProduct))]
        private async Task EditProductAsync(Product product)
        {
            if (product == null) return;

            // Product is updated by the dialog, just save it
            await _dataService.UpdateProductAsync(product);
            await LoadProductsAsync(); // Refresh list
        }

        // Method to refresh command states after role change
        public void RefreshCommandStates()
        {
            OnPropertyChanged(nameof(IsAdmin));
            EditProductCommand.NotifyCanExecuteChanged();
            DeleteProductCommand.NotifyCanExecuteChanged();
        }
    }
}
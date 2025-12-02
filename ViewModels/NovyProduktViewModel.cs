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
using Windows.Storage;

namespace Sklad_2.ViewModels
{
    public partial class NovyProduktViewModel : ObservableObject, IRecipient<RoleChangedMessage>
    {
        private readonly IDataService _dataService;
        private readonly IAuthService _authService;
        private readonly ISettingsService _settingsService;
        private readonly IProductImageService _imageService;
        private readonly IMessenger _messenger;
        private List<VatConfig> _vatConfigs;
        private StorageFile _pendingImageFile;

        [ObservableProperty]
        private bool isSalesRole;

        public bool IsVatPayer => _settingsService.CurrentSettings.IsVatPayer;
        public bool IsAdmin => _authService.CurrentRole == "Admin";

        [ObservableProperty]
        private string ean = string.Empty;

        [ObservableProperty]
        private string name = string.Empty;

        [ObservableProperty]
        private string description = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FinalPriceDisplay))]
        private string salePrice = string.Empty;

        [ObservableProperty]
        private string purchasePrice = string.Empty;

        [ObservableProperty]
        private string markup = string.Empty;

        // Flag to prevent infinite loop when updating markup/salePrice
        private bool _isUpdatingFromMarkup = false;
        private bool _isUpdatingFromSalePrice = false;

        [ObservableProperty]
        private string selectedCategory;

        // ===== Brand & Category (DB-backed) =====
        [ObservableProperty]
        private Brand selectedBrand;

        [ObservableProperty]
        private ProductCategory selectedProductCategory;

        [ObservableProperty]
        private double vatRate;

        [ObservableProperty]
        private string vatRateDisplay;

        [ObservableProperty]
        private string statusMessage = string.Empty;

        [ObservableProperty]
        private bool isError = false;

        // Image properties
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasPendingImage))]
        private BitmapImage previewImage;

        public bool HasPendingImage => PreviewImage != null;

        // Discount properties
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FinalPriceDisplay))]
        private bool hasDiscount;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FinalPriceDisplay))]
        private double discountPercent;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowDiscountDates))]
        private string selectedDiscountReason;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowDiscountDates))]
        private bool isStockClearance;

        [ObservableProperty]
        private DateTimeOffset discountValidFrom = DateTimeOffset.Now;

        [ObservableProperty]
        private DateTimeOffset discountValidTo = DateTimeOffset.Now.AddDays(30);

        public ObservableCollection<string> DiscountReasons { get; } = new ObservableCollection<string>
        {
            "Akce",
            "Výprodej", 
            "Poškozené",
            "Krátká expirace"
        };

        public bool ShowDiscountDates => !IsStockClearance;

        public string FinalPriceDisplay
        {
            get
            {
                if (!decimal.TryParse(SalePrice, out decimal price) || price <= 0)
                    return "";

                if (!HasDiscount || DiscountPercent <= 0)
                    return "";

                decimal finalPrice = price * (1 - (decimal)DiscountPercent / 100);
                return $"Konečná cena po slevě: {finalPrice:C}";
            }
        }

        // ===== OLD - Keep for backwards compatibility temporarily =====
        public ObservableCollection<string> Categories { get; } = new ObservableCollection<string>(ProductCategories.All);

        // ===== NEW - DB-backed collections =====
        public ObservableCollection<Brand> Brands { get; } = new ObservableCollection<Brand>();
        public ObservableCollection<ProductCategory> ProductCategoriesCollection { get; } = new ObservableCollection<ProductCategory>();

        public NovyProduktViewModel(IDataService dataService, IAuthService authService, ISettingsService settingsService, IProductImageService imageService, IMessenger messenger)
        {
            _dataService = dataService;
            _authService = authService;
            _settingsService = settingsService;
            _imageService = imageService;
            _messenger = messenger;
            SelectedCategory = Categories.FirstOrDefault(c => c == "Ostatní");
            SelectedDiscountReason = DiscountReasons.FirstOrDefault();

            // Initial check
            IsSalesRole = _authService.CurrentRole == "Cashier";

            // Register for messages
            _messenger.Register<RoleChangedMessage>(this);
            _messenger.Register<VatConfigsChangedMessage>(this, async (r, m) =>
            {
                // Reload categories from Database (Win10 compatibility)
                await Task.Delay(100); // Small delay for file system flush
                await LoadBrandsAsync();
                await LoadCategoriesAsync();
                RefreshCategories();
                LoadVatConfigsAsync();
            });

            // Listen for settings changes to update IsVatPayer property
            _messenger.Register<SettingsChangedMessage>(this, (r, m) =>
            {
                OnPropertyChanged(nameof(IsVatPayer));
                UpdateVatRateForSelectedCategory();
            });

            LoadVatConfigsAsync();
        }

        public void Receive(RoleChangedMessage message)
        {
            IsSalesRole = message.Value == "Cashier";
            OnPropertyChanged(nameof(IsAdmin));
        }

        private async void LoadVatConfigsAsync()
        {
            _vatConfigs = await _dataService.GetVatConfigsAsync();
            UpdateVatRateForSelectedCategory();
        }

        public async Task LoadBrandsAsync()
        {
            var brands = await _dataService.GetBrandsAsync();
            Brands.Clear();
            foreach (var brand in brands)
            {
                Brands.Add(brand);
            }
        }

        public async Task LoadCategoriesAsync()
        {
            var categories = await _dataService.GetProductCategoriesAsync();
            ProductCategoriesCollection.Clear();
            foreach (var category in categories)
            {
                ProductCategoriesCollection.Add(category);
            }
        }

        private void RefreshCategories()
        {
            // Win10 fix: Reload categories from ProductCategories.All
            var currentSelection = SelectedCategory;
            Categories.Clear();

            foreach (var category in ProductCategories.All)
            {
                Categories.Add(category);
            }

            // Restore selection if it still exists, otherwise select first
            if (Categories.Contains(currentSelection))
            {
                SelectedCategory = currentSelection;
            }
            else
            {
                SelectedCategory = Categories.FirstOrDefault(c => c == "Ostatní") ?? Categories.FirstOrDefault();
            }
        }

        partial void OnSelectedCategoryChanged(string value)
        {
            UpdateVatRateForSelectedCategory();
        }

        partial void OnSelectedProductCategoryChanged(ProductCategory value)
        {
            UpdateVatRateForSelectedCategory();
        }

        partial void OnPurchasePriceChanged(string value)
        {
            // When purchase price changes, recalculate markup based on current sale price
            RecalculateMarkupFromSalePrice();
        }

        partial void OnMarkupChanged(string value)
        {
            // When markup changes, recalculate sale price
            if (_isUpdatingFromSalePrice) return;

            if (!decimal.TryParse(PurchasePrice, out decimal purchaseValue) || purchaseValue <= 0)
                return;

            if (!decimal.TryParse(value, out decimal markupValue))
                return;

            // Calculate sale price from markup: SalePrice = PurchasePrice * (1 + Markup/100)
            decimal newSalePrice = Math.Round(purchaseValue * (1 + markupValue / 100), 2);

            _isUpdatingFromMarkup = true;
            SalePrice = newSalePrice.ToString("F2");
            _isUpdatingFromMarkup = false;
        }

        partial void OnSalePriceChanged(string value)
        {
            // Recalculate markup when sale price changes (unless we're updating from markup)
            if (!_isUpdatingFromMarkup)
            {
                RecalculateMarkupFromSalePrice();
            }
        }

        private void RecalculateMarkupFromSalePrice()
        {
            if (_isUpdatingFromMarkup) return;

            if (!decimal.TryParse(PurchasePrice, out decimal purchaseValue) || purchaseValue <= 0)
            {
                // Can't calculate markup without valid purchase price
                return;
            }

            if (!decimal.TryParse(SalePrice, out decimal saleValue) || saleValue <= 0)
            {
                return;
            }

            // Calculate markup: Markup = (SalePrice - PurchasePrice) / PurchasePrice * 100
            // Round to whole number for cleaner display (33% instead of 33.3%)
            decimal calculatedMarkup = Math.Round((saleValue - purchaseValue) / purchaseValue * 100, 0);

            _isUpdatingFromSalePrice = true;
            Markup = calculatedMarkup.ToString("F0");
            _isUpdatingFromSalePrice = false;
        }

        private void UpdateVatRateForSelectedCategory()
        {
            // Use new ProductCategory system if available, fallback to old Category string
            var categoryName = SelectedProductCategory?.Name ?? SelectedCategory;
            var config = _vatConfigs?.FirstOrDefault(c => c.CategoryName == categoryName);

            if (config != null)
            {
                VatRate = config.Rate;
                VatRateDisplay = $"{VatRate} %";
            }
            else
            {
                VatRate = 0;
                VatRateDisplay = "Není nastaveno";
            }
        }

        [RelayCommand]
        private async Task AddNewProductAsync()
        {
            ClearStatus();

            if (string.IsNullOrWhiteSpace(Ean) || string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(SalePrice) || string.IsNullOrWhiteSpace(PurchasePrice))
            {
                SetError("Všechna pole kromě počtu kusů musí být vyplněna.");
                return;
            }

            if (!decimal.TryParse(SalePrice, out decimal salePriceValue) || !decimal.TryParse(PurchasePrice, out decimal purchasePriceValue))
            {
                SetError("Cena musí být platné číslo.");
                return;
            }

            if (salePriceValue <= 0 || purchasePriceValue <= 0)
            {
                SetError("Ceny musí být větší než 0.");
                return;
            }

            if (salePriceValue > 1000000 || purchasePriceValue > 1000000)
            {
                SetError("Cena je příliš vysoká (maximum 1 000 000 Kč).");
                return;
            }

            if (string.IsNullOrWhiteSpace(Ean) || Ean.Length < 3)
            {
                SetError("EAN kód musí obsahovat alespoň 3 znaky.");
                return;
            }

            // VAT validation - only required for VAT payers
            decimal vatRateToUse = 0;
            if (_settingsService.CurrentSettings.IsVatPayer)
            {
                // Use new ProductCategory system if available, fallback to old Category string
                var categoryName = SelectedProductCategory?.Name ?? SelectedCategory;
                var vatConfig = _vatConfigs?.FirstOrDefault(c => c.CategoryName == categoryName);
                if (vatConfig == null || vatConfig.Rate == 0)
                {
                    SetError($"Pro kategorii '{categoryName}' není nastavena platná sazba DPH (nesmí být 0 %). Prosím, nastavte ji v menu Nastavení -> Sazby DPH.");
                    return;
                }
                vatRateToUse = (decimal)vatConfig.Rate;
            }
            else
            {
                // For non-VAT payers, set VAT rate to 0
                vatRateToUse = 0;
            }

            // Parse markup value (default to 0 if not valid)
            decimal markupValue = 0;
            if (decimal.TryParse(Markup, out decimal parsedMarkup))
            {
                markupValue = parsedMarkup;
            }

            var newProduct = new Product
            {
                Ean = this.Ean,
                Name = this.Name,
                Description = this.Description ?? string.Empty,
                // ===== NEW: FK approach =====
                BrandId = SelectedBrand?.Id,
                ProductCategoryId = SelectedProductCategory?.Id,
                // ===== Backwards compatibility: synchronize Category string =====
                Category = SelectedProductCategory?.Name ?? this.SelectedCategory,
                SalePrice = salePriceValue,
                PurchasePrice = purchasePriceValue,
                Markup = markupValue,
                StockQuantity = 0, // New products start with 0 stock
                VatRate = vatRateToUse,
                DiscountPercent = HasDiscount && DiscountPercent > 0 ? (decimal?)DiscountPercent : null,
                DiscountReason = HasDiscount ? (IsStockClearance ? "Do vyprodání zásob" : SelectedDiscountReason) : string.Empty,
                DiscountValidFrom = HasDiscount && !IsStockClearance ? DiscountValidFrom.DateTime : (DateTime?)null,
                DiscountValidTo = HasDiscount && !IsStockClearance ? DiscountValidTo.DateTime : (DateTime?)null
            };

            try
            {
                var existingProduct = await _dataService.GetProductAsync(newProduct.Ean);
                if (existingProduct != null)
                {
                    SetError($"Produkt s EAN kódem '{newProduct.Ean}' již existuje. Použijte prosím stránku Příjem zboží pro naskladnění.");
                }
                else
                {
                    // Save image if pending
                    if (_pendingImageFile != null)
                    {
                        var imagePath = await _imageService.SaveImageAsync(newProduct.Ean, _pendingImageFile);
                        if (!string.IsNullOrEmpty(imagePath))
                        {
                            newProduct.ImagePath = imagePath;
                        }
                    }

                    await _dataService.AddProductAsync(newProduct);

                    // Record stock movement - Product Created
                    var stockMovement = new StockMovement
                    {
                        ProductEan = newProduct.Ean,
                        ProductName = newProduct.Name,
                        MovementType = StockMovementType.ProductCreated,
                        QuantityChange = 0,
                        StockBefore = 0,
                        StockAfter = 0,
                        Timestamp = DateTime.Now,
                        UserName = _authService.CurrentUser?.DisplayName ?? "Systém",
                        Notes = "Nový produkt vytvořen"
                    };
                    await _dataService.AddStockMovementAsync(stockMovement);

                    SetSuccess("Nový produkt byl úspěšně přidán do databáze!");

                    // Clear fields
                    ClearFields();
                }
            }
            catch (Exception ex)
            {
                SetError($"Chyba při přidávání produktu: {ex.Message}");
            }
        }

        private void ClearFields()
        {
            Ean = string.Empty;
            Name = string.Empty;
            Description = string.Empty;
            SalePrice = string.Empty;
            PurchasePrice = string.Empty;
            Markup = string.Empty;
            // Reset Brand & Category
            SelectedBrand = null;
            SelectedProductCategory = ProductCategoriesCollection.FirstOrDefault(c => c.Name == "Ostatní");
            SelectedCategory = Categories.FirstOrDefault(c => c == "Ostatní");
            // VatRate will be updated automatically by OnSelectedCategoryChanged
            HasDiscount = false;
            DiscountPercent = 0;
            SelectedDiscountReason = DiscountReasons.FirstOrDefault();
            DiscountValidFrom = DateTimeOffset.Now;
            DiscountValidTo = DateTimeOffset.Now.AddDays(30);
            // Clear image
            _pendingImageFile = null;
            PreviewImage = null;
        }

        /// <summary>
        /// Sets the pending image file from the UI FileOpenPicker.
        /// </summary>
        public async Task SetPendingImageAsync(StorageFile file)
        {
            if (file == null)
            {
                _pendingImageFile = null;
                PreviewImage = null;
                return;
            }

            _pendingImageFile = file;

            // Load preview
            try
            {
                var bitmap = new BitmapImage();
                using var stream = await file.OpenReadAsync();
                await bitmap.SetSourceAsync(stream);
                PreviewImage = bitmap;
            }
            catch
            {
                PreviewImage = null;
            }
        }

        [RelayCommand]
        private void RemoveImage()
        {
            _pendingImageFile = null;
            PreviewImage = null;
        }

        private async void SetError(string message)
        {
            StatusMessage = message;
            IsError = true;

            // Automaticky zavřít po 5 sekundách
            await Task.Delay(5000);
            if (StatusMessage == message)
            {
                ClearStatus();
            }
        }

        private async void SetSuccess(string message)
        {
            StatusMessage = message;
            IsError = false;

            // Automaticky zavřít po 3 sekundách
            await Task.Delay(3000);
            if (StatusMessage == message)
            {
                ClearStatus();
            }
        }

        public void ClearStatus()
        {
            StatusMessage = string.Empty;
            IsError = false;
        }
    }
}
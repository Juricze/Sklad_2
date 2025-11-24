using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
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
    public partial class NovyProduktViewModel : ObservableObject, IRecipient<RoleChangedMessage>
    {
        private readonly IDataService _dataService;
        private readonly IAuthService _authService;
        private readonly ISettingsService _settingsService;
        private readonly IMessenger _messenger;
        private List<VatConfig> _vatConfigs;

        [ObservableProperty]
        private bool isSalesRole;

        public bool IsVatPayer => _settingsService.CurrentSettings.IsVatPayer;
        public bool IsAdmin => _authService.CurrentRole == "Admin";

        [ObservableProperty]
        private string ean = string.Empty;

        [ObservableProperty]
        private string name = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FinalPriceDisplay))]
        private string salePrice = string.Empty;

        [ObservableProperty]
        private string purchasePrice = string.Empty;

        [ObservableProperty]
        private string selectedCategory;

        [ObservableProperty]
        private double vatRate;

        [ObservableProperty]
        private string vatRateDisplay;

        [ObservableProperty]
        private string statusMessage = string.Empty;

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

        public ObservableCollection<string> Categories { get; } = new ObservableCollection<string>(ProductCategories.All);

        public NovyProduktViewModel(IDataService dataService, IAuthService authService, ISettingsService settingsService, IMessenger messenger)
        {
            _dataService = dataService;
            _authService = authService;
            _settingsService = settingsService;
            _messenger = messenger;
            SelectedCategory = Categories.FirstOrDefault(c => c == "Ostatní");
            SelectedDiscountReason = DiscountReasons.FirstOrDefault();

            // Initial check
            IsSalesRole = _authService.CurrentRole == "Prodej";

            // Register for messages
            _messenger.Register<RoleChangedMessage>(this);
            _messenger.Register<VatConfigsChangedMessage>(this, (r, m) =>
            {
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
            IsSalesRole = message.Value == "Prodej";
            OnPropertyChanged(nameof(IsAdmin));
        }

        private async void LoadVatConfigsAsync()
        {
            _vatConfigs = await _dataService.GetVatConfigsAsync();
            UpdateVatRateForSelectedCategory();
        }

        partial void OnSelectedCategoryChanged(string value)
        {
            UpdateVatRateForSelectedCategory();
        }

        private void UpdateVatRateForSelectedCategory()
        {
            var config = _vatConfigs?.FirstOrDefault(c => c.CategoryName == SelectedCategory);

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
            StatusMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(Ean) || string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(SalePrice) || string.IsNullOrWhiteSpace(PurchasePrice))
            {
                StatusMessage = "Všechna pole kromě počtu kusů musí být vyplněna.";
                return;
            }

            if (!decimal.TryParse(SalePrice, out decimal salePriceValue) || !decimal.TryParse(PurchasePrice, out decimal purchasePriceValue))
            {
                StatusMessage = "Cena musí být platné číslo.";
                return;
            }

            if (salePriceValue <= 0 || purchasePriceValue <= 0)
            {
                StatusMessage = "Ceny musí být větší než 0.";
                return;
            }

            if (salePriceValue > 1000000 || purchasePriceValue > 1000000)
            {
                StatusMessage = "Cena je příliš vysoká (maximum 1 000 000 Kč).";
                return;
            }

            if (string.IsNullOrWhiteSpace(Ean) || Ean.Length < 3)
            {
                StatusMessage = "EAN kód musí obsahovat alespoň 3 znaky.";
                return;
            }

            // VAT validation - only required for VAT payers
            decimal vatRateToUse = 0;
            if (_settingsService.CurrentSettings.IsVatPayer)
            {
                var vatConfig = _vatConfigs?.FirstOrDefault(c => c.CategoryName == SelectedCategory);
                if (vatConfig == null || vatConfig.Rate == 0)
                {
                    StatusMessage = $"Pro kategorii '{SelectedCategory}' není nastavena platná sazba DPH (nesmí být 0 %). Prosím, nastavte ji v menu Nastavení -> Sazby DPH.";
                    return;
                }
                vatRateToUse = (decimal)vatConfig.Rate;
            }
            else
            {
                // For non-VAT payers, set VAT rate to 0
                vatRateToUse = 0;
            }

            var newProduct = new Product
            {
                Ean = this.Ean,
                Name = this.Name,
                Category = this.SelectedCategory,
                SalePrice = salePriceValue,
                PurchasePrice = purchasePriceValue,
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
                    StatusMessage = $"Produkt s EAN kódem '{newProduct.Ean}' již existuje. Použijte prosím stránku Příjem zboží pro naskladnění.";
                }
                else
                {
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

                    StatusMessage = "Nový produkt byl úspěšně přidán do databáze!";

                    // Clear fields
                    ClearFields();
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Chyba při přidávání produktu: {ex.Message}";
            }
        }

        private void ClearFields()
        {
            Ean = string.Empty;
            Name = string.Empty;
            SalePrice = string.Empty;
            PurchasePrice = string.Empty;
            SelectedCategory = Categories.FirstOrDefault(c => c == "Ostatní");
            // VatRate will be updated automatically by OnSelectedCategoryChanged
            HasDiscount = false;
            DiscountPercent = 0;
            SelectedDiscountReason = DiscountReasons.FirstOrDefault();
            DiscountValidFrom = DateTimeOffset.Now;
            DiscountValidTo = DateTimeOffset.Now.AddDays(30);
        }
    }
}
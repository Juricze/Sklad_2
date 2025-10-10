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
        private readonly IMessenger _messenger;
        private List<VatConfig> _vatConfigs;

        [ObservableProperty]
        private bool isSalesRole;

        [ObservableProperty]
        private string ean = string.Empty;

        [ObservableProperty]
        private string name = string.Empty;

        [ObservableProperty]
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

        public ObservableCollection<string> Categories { get; } = new ObservableCollection<string>(ProductCategories.All);

        public NovyProduktViewModel(IDataService dataService, IAuthService authService, IMessenger messenger)
        {
            _dataService = dataService;
            _authService = authService;
            _messenger = messenger;
            SelectedCategory = Categories.FirstOrDefault(c => c == "Ostatní");

            // Initial check
            IsSalesRole = _authService.CurrentRole == "Prodej";

            // Register for messages
            _messenger.Register<RoleChangedMessage>(this);
            _messenger.Register<VatConfigsChangedMessage>(this, (r, m) =>
            {
                LoadVatConfigsAsync();
            });

            LoadVatConfigsAsync();
        }

        public void Receive(RoleChangedMessage message)
        {
            IsSalesRole = message.Value == "Prodej";
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

            var vatConfig = _vatConfigs?.FirstOrDefault(c => c.CategoryName == SelectedCategory);
            if (vatConfig == null || vatConfig.Rate == 0)
            {
                StatusMessage = $"Pro kategorii '{SelectedCategory}' není nastavena platná sazba DPH (nesmí být 0 %). Prosím, nastavte ji v menu Nastavení -> Sazby DPH.";
                return;
            }

            var newProduct = new Product
            {
                Ean = this.Ean,
                Name = this.Name,
                Category = this.SelectedCategory,
                SalePrice = salePriceValue,
                PurchasePrice = purchasePriceValue,
                StockQuantity = 0, // New products start with 0 stock
                VatRate = (decimal)this.VatRate
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
        }
    }
}
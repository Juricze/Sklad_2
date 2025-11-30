using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Sklad_2.Messages;
using Sklad_2.Models;
using Sklad_2.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Sklad_2.ViewModels
{
    /// <summary>
    /// REFACTORED: Now uses ProductCategory table instead of AppSettings.Categories
    /// </summary>
    public partial class CategoryManagementViewModel : ObservableObject
    {
        private readonly IDataService _dataService;
        private readonly IMessenger _messenger;

        [ObservableProperty]
        private ObservableCollection<ProductCategory> categories = new();

        [ObservableProperty]
        private ProductCategory selectedCategory;

        [ObservableProperty]
        private string newCategoryName;

        [ObservableProperty]
        private string renameCategoryName;

        [ObservableProperty]
        private string addStatusMessage;

        [ObservableProperty]
        private bool isAddStatusError;

        [ObservableProperty]
        private string renameStatusMessage;

        [ObservableProperty]
        private bool isRenameStatusError;

        [ObservableProperty]
        private string deleteStatusMessage;

        [ObservableProperty]
        private bool isDeleteStatusError;

        public CategoryManagementViewModel(IDataService dataService, IMessenger messenger)
        {
            _dataService = dataService;
            _messenger = messenger;
        }

        public async Task LoadCategoriesAsync()
        {
            Categories.Clear();
            var cats = await _dataService.GetProductCategoriesAsync();
            foreach (var cat in cats)
            {
                Categories.Add(cat);
            }
        }

        [RelayCommand]
        private async Task AddCategoryAsync()
        {
            ClearAddStatus();

            if (string.IsNullOrWhiteSpace(NewCategoryName))
            {
                ShowAddError("Název kategorie nesmí být prázdný.");
                return;
            }

            var existing = await _dataService.GetProductCategoryByNameAsync(NewCategoryName);
            if (existing != null)
            {
                ShowAddError("Kategorie s tímto názvem již existuje.");
                return;
            }

            var newCategory = new ProductCategory { Name = NewCategoryName, Description = string.Empty };
            await _dataService.AddProductCategoryAsync(newCategory);

            await LoadCategoriesAsync();
            NewCategoryName = string.Empty;

            _messenger.Send(new VatConfigsChangedMessage());
            ShowAddSuccess($"Kategorie '{newCategory.Name}' byla přidána.");
        }

        [RelayCommand]
        private async Task RenameCategoryAsync()
        {
            ClearRenameStatus();

            if (SelectedCategory == null)
            {
                ShowRenameError("Vyberte kategorii k přejmenování.");
                return;
            }

            if (string.IsNullOrWhiteSpace(RenameCategoryName))
            {
                ShowRenameError("Zadejte nový název kategorie.");
                return;
            }

            var existing = await _dataService.GetProductCategoryByNameAsync(RenameCategoryName);
            if (existing != null && existing.Id != SelectedCategory.Id)
            {
                ShowRenameError("Kategorie s tímto názvem již existuje.");
                return;
            }

            var oldName = SelectedCategory.Name;
            var newName = RenameCategoryName;

            // Check how many products use this category (via ProductCategoryId)
            var productCount = await _dataService.GetProductCountByCategoryIdAsync(SelectedCategory.Id);

            // Update ProductCategory name
            SelectedCategory.Name = newName;
            await _dataService.UpdateProductCategoryAsync(SelectedCategory);

            // Synchronize Product.Category string for backwards compatibility
            if (productCount > 0)
            {
                await _dataService.UpdateProductsCategoryAsync(oldName, newName);
            }

            // Update VatConfig - delete old and add new (because CategoryName is PRIMARY KEY)
            var vatConfigs = await _dataService.GetVatConfigsAsync();
            var oldVatConfig = vatConfigs.FirstOrDefault(v => v.CategoryName == oldName);
            if (oldVatConfig != null)
            {
                await _dataService.DeleteVatConfigAsync(oldName);
                var newVatConfig = new VatConfig { CategoryName = newName, Rate = oldVatConfig.Rate };
                await _dataService.SaveVatConfigsAsync(new[] { newVatConfig });
            }

            await LoadCategoriesAsync();
            SelectedCategory = Categories.FirstOrDefault(c => c.Name == newName);
            RenameCategoryName = string.Empty;

            _messenger.Send(new VatConfigsChangedMessage());
            ShowRenameSuccess($"Kategorie '{oldName}' byla přejmenována na '{newName}'. Aktualizováno {productCount} produktů.");
        }

        [RelayCommand]
        private async Task DeleteCategoryAsync()
        {
            ClearDeleteStatus();

            if (SelectedCategory == null)
            {
                ShowDeleteError("Vyberte kategorii ke smazání.");
                return;
            }

            // Check if any products use this category
            var productCount = await _dataService.GetProductCountByCategoryIdAsync(SelectedCategory.Id);
            if (productCount > 0)
            {
                ShowDeleteError($"Kategorii '{SelectedCategory.Name}' nelze smazat. Je použita u {productCount} produktů. Nejprve přejmenujte kategorii nebo odstraňte produkty.");
                return;
            }

            var categoryName = SelectedCategory.Name;

            // Delete VatConfig
            await _dataService.DeleteVatConfigAsync(categoryName);

            // Delete category from DB
            await _dataService.DeleteProductCategoryAsync(SelectedCategory.Id);

            await LoadCategoriesAsync();
            SelectedCategory = null;

            _messenger.Send(new VatConfigsChangedMessage());
            ShowDeleteSuccess($"Kategorie '{categoryName}' byla smazána.");
        }

        // Add status methods
        private void ShowAddError(string message)
        {
            AddStatusMessage = message;
            IsAddStatusError = true;
        }

        private void ShowAddSuccess(string message)
        {
            AddStatusMessage = message;
            IsAddStatusError = false;
        }

        [RelayCommand]
        private void ClearAddStatus()
        {
            AddStatusMessage = string.Empty;
            IsAddStatusError = false;
        }

        // Rename status methods
        private void ShowRenameError(string message)
        {
            RenameStatusMessage = message;
            IsRenameStatusError = true;
        }

        private void ShowRenameSuccess(string message)
        {
            RenameStatusMessage = message;
            IsRenameStatusError = false;
        }

        [RelayCommand]
        private void ClearRenameStatus()
        {
            RenameStatusMessage = string.Empty;
            IsRenameStatusError = false;
        }

        // Delete status methods
        private void ShowDeleteError(string message)
        {
            DeleteStatusMessage = message;
            IsDeleteStatusError = true;
        }

        private void ShowDeleteSuccess(string message)
        {
            DeleteStatusMessage = message;
            IsDeleteStatusError = false;
        }

        [RelayCommand]
        private void ClearDeleteStatus()
        {
            DeleteStatusMessage = string.Empty;
            IsDeleteStatusError = false;
        }
    }
}

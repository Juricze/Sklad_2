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
    public partial class CategoryManagementViewModel : ObservableObject
    {
        private readonly ISettingsService _settingsService;
        private readonly IDataService _dataService;
        private readonly IMessenger _messenger;

        [ObservableProperty]
        private ObservableCollection<string> categories = new();

        [ObservableProperty]
        private string selectedCategory;

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

        public CategoryManagementViewModel(ISettingsService settingsService, IDataService dataService, IMessenger messenger)
        {
            _settingsService = settingsService;
            _dataService = dataService;
            _messenger = messenger;

            LoadCategories();
        }

        private void LoadCategories()
        {
            Categories.Clear();
            var cats = _settingsService.CurrentSettings.Categories ?? ProductCategories.All;
            foreach (var cat in cats.OrderBy(c => c))
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

            if (Categories.Contains(NewCategoryName))
            {
                ShowAddError("Kategorie s tímto názvem již existuje.");
                return;
            }

            var categoryName = NewCategoryName;

            // Add to settings
            _settingsService.CurrentSettings.Categories.Add(categoryName);
            await _settingsService.SaveSettingsAsync();

            // Reload
            LoadCategories();
            NewCategoryName = string.Empty;

            // Notify other ViewModels
            _messenger.Send(new VatConfigsChangedMessage());

            ShowAddSuccess($"Kategorie '{categoryName}' byla přidána.");
        }

        [RelayCommand]
        private async Task RenameCategoryAsync()
        {
            ClearRenameStatus();

            if (string.IsNullOrWhiteSpace(SelectedCategory))
            {
                ShowRenameError("Vyberte kategorii k přejmenování.");
                return;
            }

            if (string.IsNullOrWhiteSpace(RenameCategoryName))
            {
                ShowRenameError("Zadejte nový název kategorie.");
                return;
            }

            if (Categories.Contains(RenameCategoryName))
            {
                ShowRenameError("Kategorie s tímto názvem již existuje.");
                return;
            }

            var oldName = SelectedCategory;
            var newName = RenameCategoryName;

            // Check how many products use this category
            var productCount = await _dataService.GetProductCountByCategoryAsync(oldName);

            // Update products
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

            // Update settings
            var index = _settingsService.CurrentSettings.Categories.IndexOf(oldName);
            if (index >= 0)
            {
                _settingsService.CurrentSettings.Categories[index] = newName;
                await _settingsService.SaveSettingsAsync();
            }

            // Reload
            LoadCategories();
            SelectedCategory = newName;
            RenameCategoryName = string.Empty;

            // Notify other ViewModels
            _messenger.Send(new VatConfigsChangedMessage());

            ShowRenameSuccess($"Kategorie '{oldName}' byla přejmenována na '{newName}'. Aktualizováno {productCount} produktů.");
        }

        [RelayCommand]
        private async Task DeleteCategoryAsync()
        {
            ClearDeleteStatus();

            if (string.IsNullOrWhiteSpace(SelectedCategory))
            {
                ShowDeleteError("Vyberte kategorii ke smazání.");
                return;
            }

            // Check if any products use this category
            var productCount = await _dataService.GetProductCountByCategoryAsync(SelectedCategory);
            if (productCount > 0)
            {
                ShowDeleteError($"Kategorii '{SelectedCategory}' nelze smazat. Je použita u {productCount} produktů. Nejprve přejmenujte kategorii nebo odstraňte produkty.");
                return;
            }

            var categoryName = SelectedCategory;

            // Delete VatConfig
            await _dataService.DeleteVatConfigAsync(categoryName);

            // Remove from settings
            _settingsService.CurrentSettings.Categories.Remove(categoryName);
            await _settingsService.SaveSettingsAsync();

            // Reload
            LoadCategories();
            SelectedCategory = null;

            // Notify other ViewModels
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

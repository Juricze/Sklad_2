using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Sklad_2.Models;
using Sklad_2.Services;
using Sklad_2.ViewModels;
using Sklad_2.Views.Dialogs;
using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Sklad_2.Views
{
    public sealed partial class NovyProduktPage : Page
    {
        public NovyProduktViewModel ViewModel { get; }
        private readonly IDataService _dataService;
        private readonly BrandManagementViewModel _brandManagementViewModel;
        private readonly CategoryManagementViewModel _categoryManagementViewModel;

        public NovyProduktPage()
        {
            // IMPORTANT: ViewModel must be set BEFORE InitializeComponent() for x:Bind to work properly
            var app = Application.Current as App;
            ViewModel = app.Services.GetRequiredService<NovyProduktViewModel>();
            _dataService = app.Services.GetRequiredService<IDataService>();
            _brandManagementViewModel = app.Services.GetRequiredService<BrandManagementViewModel>();
            _categoryManagementViewModel = app.Services.GetRequiredService<CategoryManagementViewModel>();

            this.InitializeComponent();
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            await ViewModel.LoadBrandsAsync();
            await ViewModel.LoadCategoriesAsync();
        }

        private async void SelectImageButton_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");
            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".bmp");
            picker.ViewMode = PickerViewMode.Thumbnail;
            picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;

            // Get the window handle and initialize picker
            var app = Application.Current as App;
            var hwnd = WindowNative.GetWindowHandle(app.CurrentWindow);
            InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                await ViewModel.SetPendingImageAsync(file);
            }
        }

        // ===== Brand Management =====
        private async void AddBrand_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new AddBrandDialog();
            dialog.XamlRoot = this.XamlRoot;

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                var newBrand = dialog.GetBrand();

                // Check for duplicate name
                var existing = await _dataService.GetBrandByNameAsync(newBrand.Name);
                if (existing != null)
                {
                    await ShowErrorDialog("Značka s tímto názvem již existuje.");
                    return;
                }

                await _dataService.AddBrandAsync(newBrand);
                await ViewModel.LoadBrandsAsync();
            }
        }

        private async void EditBrand_Click(object sender, RoutedEventArgs e)
        {
            var selectedBrand = BrandsListView.SelectedItem as Brand;
            if (selectedBrand == null)
            {
                await ShowErrorDialog("Vyberte značku k úpravě.");
                return;
            }

            var dialog = new AddBrandDialog();
            dialog.SetEditMode(selectedBrand);
            dialog.XamlRoot = this.XamlRoot;

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                var updatedBrand = dialog.GetBrand();

                // Check for duplicate name (excluding current brand)
                var existing = await _dataService.GetBrandByNameAsync(updatedBrand.Name);
                if (existing != null && existing.Id != updatedBrand.Id)
                {
                    await ShowErrorDialog("Značka s tímto názvem již existuje.");
                    return;
                }

                await _dataService.UpdateBrandAsync(updatedBrand);
                await ViewModel.LoadBrandsAsync();
            }
        }

        private async void DeleteBrand_Click(object sender, RoutedEventArgs e)
        {
            var selectedBrand = BrandsListView.SelectedItem as Brand;
            if (selectedBrand == null)
            {
                await ShowErrorDialog("Vyberte značku ke smazání.");
                return;
            }

            // Check if any products use this brand
            var productCount = await _dataService.GetProductCountByBrandIdAsync(selectedBrand.Id);
            if (productCount > 0)
            {
                await ShowErrorDialog($"Značku '{selectedBrand.Name}' nelze smazat. Je použita u {productCount} produktů.");
                return;
            }

            var confirmDialog = new ContentDialog
            {
                Title = "Potvrdit smazání",
                Content = $"Opravdu chcete smazat značku '{selectedBrand.Name}'?",
                PrimaryButtonText = "Smazat",
                CloseButtonText = "Zrušit",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            var result = await confirmDialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                await _dataService.DeleteBrandAsync(selectedBrand.Id);
                await ViewModel.LoadBrandsAsync();
            }
        }

        // ===== Category Management =====
        private async void AddCategory_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new AddCategoryDialog();
            dialog.XamlRoot = this.XamlRoot;

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                var newCategory = dialog.GetCategory();

                // Check for duplicate name
                var existing = await _dataService.GetProductCategoryByNameAsync(newCategory.Name);
                if (existing != null)
                {
                    await ShowErrorDialog("Kategorie s tímto názvem již existuje.");
                    return;
                }

                await _dataService.AddProductCategoryAsync(newCategory);
                await ViewModel.LoadCategoriesAsync();
            }
        }

        private async void EditCategory_Click(object sender, RoutedEventArgs e)
        {
            var selectedCategory = CategoriesListView.SelectedItem as ProductCategory;
            if (selectedCategory == null)
            {
                await ShowErrorDialog("Vyberte kategorii k úpravě.");
                return;
            }

            var dialog = new AddCategoryDialog();
            dialog.SetEditMode(selectedCategory);
            dialog.XamlRoot = this.XamlRoot;

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                var updatedCategory = dialog.GetCategory();

                // Check for duplicate name (excluding current category)
                var existing = await _dataService.GetProductCategoryByNameAsync(updatedCategory.Name);
                if (existing != null && existing.Id != updatedCategory.Id)
                {
                    await ShowErrorDialog("Kategorie s tímto názvem již existuje.");
                    return;
                }

                var oldName = selectedCategory.Name;
                var newName = updatedCategory.Name;

                // Update ProductCategory
                await _dataService.UpdateProductCategoryAsync(updatedCategory);

                // Check how many products use this category
                var productCount = await _dataService.GetProductCountByCategoryIdAsync(updatedCategory.Id);

                // Synchronize Product.Category string for backwards compatibility
                if (productCount > 0 && oldName != newName)
                {
                    await _dataService.UpdateProductsCategoryAsync(oldName, newName);
                }

                // Update VatConfig if exists
                var vatConfigs = await _dataService.GetVatConfigsAsync();
                var oldVatConfig = vatConfigs.FirstOrDefault(v => v.CategoryName == oldName);
                if (oldVatConfig != null && oldName != newName)
                {
                    await _dataService.DeleteVatConfigAsync(oldName);
                    var newVatConfig = new VatConfig { CategoryName = newName, Rate = oldVatConfig.Rate };
                    await _dataService.SaveVatConfigsAsync(new[] { newVatConfig });
                }

                await ViewModel.LoadCategoriesAsync();
            }
        }

        private async void DeleteCategory_Click(object sender, RoutedEventArgs e)
        {
            var selectedCategory = CategoriesListView.SelectedItem as ProductCategory;
            if (selectedCategory == null)
            {
                await ShowErrorDialog("Vyberte kategorii ke smazání.");
                return;
            }

            // Check if any products use this category
            var productCount = await _dataService.GetProductCountByCategoryIdAsync(selectedCategory.Id);
            if (productCount > 0)
            {
                await ShowErrorDialog($"Kategorii '{selectedCategory.Name}' nelze smazat. Je použita u {productCount} produktů.");
                return;
            }

            var confirmDialog = new ContentDialog
            {
                Title = "Potvrdit smazání",
                Content = $"Opravdu chcete smazat kategorii '{selectedCategory.Name}'?",
                PrimaryButtonText = "Smazat",
                CloseButtonText = "Zrušit",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            var result = await confirmDialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                // Delete VatConfig if exists
                await _dataService.DeleteVatConfigAsync(selectedCategory.Name);

                // Delete category
                await _dataService.DeleteProductCategoryAsync(selectedCategory.Id);
                await ViewModel.LoadCategoriesAsync();
            }
        }

        private async Task ShowErrorDialog(string message)
        {
            var dialog = new ContentDialog
            {
                Title = "Chyba",
                Content = message,
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }
    }
}

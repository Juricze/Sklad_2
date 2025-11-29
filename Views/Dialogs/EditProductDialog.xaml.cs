using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Sklad_2.Models;
using Sklad_2.Services;
using System;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Sklad_2.Views.Dialogs
{
    public sealed partial class EditProductDialog : ContentDialog
    {
        private readonly Product _product;
        private readonly IProductImageService _imageService;
        private readonly bool _isAdmin;
        private bool _isUpdatingFromMarkup = false;
        private bool _isUpdatingFromSalePrice = false;
        private StorageFile _pendingImageFile = null;
        private bool _removeImage = false;

        public EditProductDialog(Product product, bool isAdmin = true)
        {
            this.InitializeComponent();
            _product = product;
            _isAdmin = isAdmin;
            _imageService = ((App)Application.Current).Services.GetRequiredService<IProductImageService>();

            // Display read-only info
            EanText.Text = product.Ean;

            // Editable fields (all roles)
            NameBox.Text = product.Name;
            DescriptionBox.Text = product.Description ?? string.Empty;

            // Populate categories
            foreach (var category in ProductCategories.All)
            {
                CategoryCombo.Items.Add(category);
            }
            CategoryCombo.SelectedItem = product.Category;

            // Price fields (admin only)
            PurchasePriceBox.Text = product.PurchasePrice.ToString("F2");
            SalePriceBox.Text = product.SalePrice.ToString("F2");
            MarkupBox.Text = product.Markup.ToString("F0");
            SalePriceReadOnly.Text = product.SalePriceFormatted;

            // Configure UI based on role
            ConfigureRoleBasedUI();

            // Load existing image if any
            LoadExistingImage();

            // Wire up events for bidirectional calculation (admin only)
            if (_isAdmin)
            {
                PurchasePriceBox.TextChanged += PurchasePriceBox_TextChanged;
                SalePriceBox.TextChanged += SalePriceBox_TextChanged;
                MarkupBox.TextChanged += MarkupBox_TextChanged;
            }
        }

        private void ConfigureRoleBasedUI()
        {
            if (_isAdmin)
            {
                // Admin can see and edit all
                AdminSectionSeparator.Visibility = Visibility.Visible;
                AdminSectionHeader.Visibility = Visibility.Visible;
                PurchasePriceBox.Visibility = Visibility.Visible;
                PriceMarkupGrid.Visibility = Visibility.Visible;
                PriceReadOnlyPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                // Non-admin can only edit name, description, category, image
                // Hide price editing section, show read-only price
                AdminSectionSeparator.Visibility = Visibility.Collapsed;
                AdminSectionHeader.Visibility = Visibility.Collapsed;
                PurchasePriceBox.Visibility = Visibility.Collapsed;
                PriceMarkupGrid.Visibility = Visibility.Collapsed;
                PriceReadOnlyPanel.Visibility = Visibility.Visible;
            }
        }

        private void LoadExistingImage()
        {
            if (_imageService.HasImage(_product.Ean))
            {
                var bitmap = _imageService.GetImage(_product.Ean);
                if (bitmap != null)
                {
                    PreviewImage.Source = bitmap;
                    PreviewImage.Visibility = Visibility.Visible;
                    PlaceholderIcon.Visibility = Visibility.Collapsed;
                    RemoveImageButton.Visibility = Visibility.Visible;
                }
            }
        }

        private void PurchasePriceBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            RecalculateMarkupFromSalePrice();
        }

        private void MarkupBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingFromSalePrice) return;

            if (!decimal.TryParse(PurchasePriceBox.Text, out decimal purchaseValue) || purchaseValue <= 0)
                return;

            if (!decimal.TryParse(MarkupBox.Text, out decimal markupValue))
                return;

            // Calculate sale price from markup: SalePrice = PurchasePrice * (1 + Markup/100)
            decimal newSalePrice = Math.Round(purchaseValue * (1 + markupValue / 100), 2);

            _isUpdatingFromMarkup = true;
            SalePriceBox.Text = newSalePrice.ToString("F2");
            _isUpdatingFromMarkup = false;
        }

        private void SalePriceBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isUpdatingFromMarkup)
            {
                RecalculateMarkupFromSalePrice();
            }
        }

        private void RecalculateMarkupFromSalePrice()
        {
            if (_isUpdatingFromMarkup) return;

            if (!decimal.TryParse(PurchasePriceBox.Text, out decimal purchaseValue) || purchaseValue <= 0)
                return;

            if (!decimal.TryParse(SalePriceBox.Text, out decimal saleValue) || saleValue <= 0)
                return;

            // Calculate markup: Markup = (SalePrice - PurchasePrice) / PurchasePrice * 100
            // Round to whole number for cleaner display (33% instead of 33.3%)
            decimal calculatedMarkup = Math.Round((saleValue - purchaseValue) / purchaseValue * 100, 0);

            _isUpdatingFromSalePrice = true;
            MarkupBox.Text = calculatedMarkup.ToString("F0");
            _isUpdatingFromSalePrice = false;
        }

        public bool ValidateAndApply()
        {
            ErrorText.Visibility = Visibility.Collapsed;

            // Validate name (required for all)
            if (string.IsNullOrWhiteSpace(NameBox.Text))
            {
                ErrorText.Text = "Název je povinný.";
                ErrorText.Visibility = Visibility.Visible;
                return false;
            }

            // Validate category (required for all)
            if (CategoryCombo.SelectedItem == null)
            {
                ErrorText.Text = "Kategorie je povinná.";
                ErrorText.Visibility = Visibility.Visible;
                return false;
            }

            // Admin-only validations
            if (_isAdmin)
            {
                if (!decimal.TryParse(PurchasePriceBox.Text, out decimal purchasePrice) || purchasePrice <= 0)
                {
                    ErrorText.Text = "Nákupní cena musí být platné kladné číslo.";
                    ErrorText.Visibility = Visibility.Visible;
                    return false;
                }

                if (!decimal.TryParse(SalePriceBox.Text, out decimal salePrice) || salePrice <= 0)
                {
                    ErrorText.Text = "Prodejní cena musí být platné kladné číslo.";
                    ErrorText.Visibility = Visibility.Visible;
                    return false;
                }

                if (salePrice > 1000000 || purchasePrice > 1000000)
                {
                    ErrorText.Text = "Cena je příliš vysoká (maximum 1 000 000 Kč).";
                    ErrorText.Visibility = Visibility.Visible;
                    return false;
                }

                decimal markup = 0;
                if (decimal.TryParse(MarkupBox.Text, out decimal parsedMarkup))
                {
                    markup = parsedMarkup;
                }

                // Apply price changes (admin only)
                _product.PurchasePrice = purchasePrice;
                _product.SalePrice = salePrice;
                _product.Markup = markup;
            }

            // Apply changes that all roles can make
            _product.Name = NameBox.Text.Trim();
            _product.Description = DescriptionBox.Text?.Trim() ?? string.Empty;
            _product.Category = CategoryCombo.SelectedItem?.ToString() ?? _product.Category;

            return true;
        }

        public Product GetUpdatedProduct() => _product;

        /// <summary>
        /// Saves or removes image after validation. Call this after ValidateAndApply returns true.
        /// </summary>
        public async System.Threading.Tasks.Task SaveImageChangesAsync()
        {
            // Remove image if requested
            if (_removeImage)
            {
                _imageService.DeleteImage(_product.Ean);
                _product.ImagePath = string.Empty;
            }
            // Save new image if selected
            else if (_pendingImageFile != null)
            {
                var imagePath = await _imageService.SaveImageAsync(_product.Ean, _pendingImageFile);
                if (!string.IsNullOrEmpty(imagePath))
                {
                    _product.ImagePath = imagePath;
                }
            }
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
                _pendingImageFile = file;
                _removeImage = false;

                // Load preview
                try
                {
                    var bitmap = new BitmapImage();
                    using var stream = await file.OpenReadAsync();
                    await bitmap.SetSourceAsync(stream);
                    PreviewImage.Source = bitmap;
                    PreviewImage.Visibility = Visibility.Visible;
                    PlaceholderIcon.Visibility = Visibility.Collapsed;
                    RemoveImageButton.Visibility = Visibility.Visible;
                }
                catch
                {
                    // Ignore preview errors
                }
            }
        }

        private void RemoveImageButton_Click(object sender, RoutedEventArgs e)
        {
            _pendingImageFile = null;
            _removeImage = true;
            PreviewImage.Source = null;
            PreviewImage.Visibility = Visibility.Collapsed;
            PlaceholderIcon.Visibility = Visibility.Visible;
            RemoveImageButton.Visibility = Visibility.Collapsed;
        }
    }
}

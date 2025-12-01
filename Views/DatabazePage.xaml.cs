using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Sklad_2.ViewModels;
using Sklad_2.Views.Dialogs;
using System;
using Windows.ApplicationModel.DataTransfer;

namespace Sklad_2.Views
{
    public sealed partial class DatabazePage : Page
    {
        public DatabazeViewModel ViewModel { get; }

        public DatabazePage()
        {
            ViewModel = (Application.Current as App).Services.GetRequiredService<DatabazeViewModel>();
            ViewModel.LoadProductsCommand.Execute(null);
            this.InitializeComponent();
            this.DataContext = ViewModel;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            // Load products when the page is navigated to
            // ViewModel.LoadProductsCommand.Execute(null); // Odstraněno
        }

        private async void EditButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SelectedProduct == null)
            {
                return;
            }

            var dialog = new EditProductDialog(ViewModel.SelectedProduct, ViewModel.IsAdmin)
            {
                XamlRoot = this.XamlRoot
            };

            dialog.PrimaryButtonClick += async (s, args) =>
            {
                // Prevent auto-close to handle validation
                args.Cancel = true;

                if (dialog.ValidateAndApply())
                {
                    // Save image changes (add/remove)
                    await dialog.SaveImageChangesAsync();

                    var updatedProduct = dialog.GetUpdatedProduct();
                    await ViewModel.EditProductCommand.ExecuteAsync(updatedProduct);
                    dialog.Hide();
                }
            };

            await dialog.ShowAsync();
        }

        private void ClearFilters_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.SearchText = string.Empty;
            ViewModel.SelectedBrand = "Vše";
            ViewModel.SelectedCategory = "Vše";
        }

        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SelectedProduct == null)
            {
                return;
            }

            var product = ViewModel.SelectedProduct;

            // Build warning message
            var warningMessage = $"Opravdu chcete smazat produkt '{product.Name}' (EAN: {product.Ean})?";

            if (product.StockQuantity > 0)
            {
                warningMessage += $"\n\n⚠️ POZOR: Produkt má na skladě {product.StockQuantity} ks!";
            }

            warningMessage += "\n\nTato akce je nevratná!";

            // Confirmation dialog
            var confirmDialog = new ContentDialog
            {
                Title = "Potvrdit smazání produktu",
                Content = warningMessage,
                PrimaryButtonText = "Smazat",
                CloseButtonText = "Zrušit",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            // Style the primary button as destructive (red)
            confirmDialog.PrimaryButtonClick += (s, args) => { };

            var result = await confirmDialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                await ViewModel.DeleteProductCommand.ExecuteAsync(null);
            }
        }

        private void EanButton_Click(object sender, RoutedEventArgs e)
        {
            // Get EAN from HyperlinkButton content or from data context
            string ean = null;
            if (sender is HyperlinkButton button)
            {
                ean = button.Content?.ToString();

                // Copy to clipboard
                if (!string.IsNullOrEmpty(ean))
                {
                    var dataPackage = new DataPackage();
                    dataPackage.SetText(ean);
                    Clipboard.SetContent(dataPackage);

                    // Show subtle notification
                    EanCopiedTeachingTip.Subtitle = $"EAN {ean}";
                    EanCopiedTeachingTip.Target = button;
                    EanCopiedTeachingTip.IsOpen = true;
                }
            }
        }
    }
}
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Sklad_2.ViewModels;
using Sklad_2.Views.Dialogs;
using System;

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
    }
}
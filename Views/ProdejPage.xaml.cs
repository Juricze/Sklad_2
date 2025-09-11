using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Text; // Added this line
using Sklad_2.ViewModels;
using System;

namespace Sklad_2.Views
{
    public sealed partial class ProdejPage : Page
    {
        public ProdejViewModel ViewModel { get; set; }

        public ProdejPage()
        {
            this.InitializeComponent();
        }

        private void EanTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                var textBox = (TextBox)sender;
                ViewModel.FindProductCommand.Execute(textBox.Text);
                textBox.Text = string.Empty;
                e.Handled = true;
            }
        }

        private async void ClearReceiptButton_Click(object sender, RoutedEventArgs e)
        {
            ContentDialog clearReceiptDialog = new ContentDialog
            {
                Title = "Potvrzení smazání účtenky",
                Content = "Opravdu si přejete smazat celou účtenku? Tato akce je nevratná.",
                CloseButtonText = "Zrušit",
                PrimaryButtonText = "Smazat",
                XamlRoot = this.XamlRoot 
            };

            ContentDialogResult result = await clearReceiptDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                ViewModel.ClearReceiptCommand.Execute(null);
            }
        }

        private void IncrementButton_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is Sklad_2.Services.ReceiptItem item)
            {
                ViewModel.IncrementQuantityCommand.Execute(item);
            }
        }

        private void DecrementButton_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is Sklad_2.Services.ReceiptItem item)
            {
                ViewModel.DecrementQuantityCommand.Execute(item);
            }
        }

        private async void CheckoutButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.Receipt.Items.Count == 0)
            {
                ContentDialog emptyReceiptDialog = new ContentDialog
                {
                    Title = "Prázdná účtenka",
                    Content = "Nelze dokončit prodej s prázdnou účtenkou.",
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await emptyReceiptDialog.ShowAsync();
                return;
            }

            // Build summary content
            StackPanel summaryPanel = new StackPanel { Spacing = 8 };
            summaryPanel.Children.Add(new TextBlock { Text = "Souhrn objednávky:", FontWeight = FontWeights.Bold });
            foreach (var item in ViewModel.Receipt.Items)
            {
                summaryPanel.Children.Add(new TextBlock { Text = $"{item.Product.Name} ({item.QuantityFormatted}) - {item.TotalPriceFormatted}" });
            }
            summaryPanel.Children.Add(new TextBlock { Text = ViewModel.GrandTotalFormatted, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 12, 0, 0) });


            ContentDialog checkoutDialog = new ContentDialog
            {
                Title = "Dokončení prodeje",
                Content = summaryPanel,
                CloseButtonText = "Zrušit",
                PrimaryButtonText = "Potvrdit a dokončit", // Changed text
                XamlRoot = this.XamlRoot
            };

            ContentDialogResult result = await checkoutDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                await ViewModel.CheckoutCommand.ExecuteAsync(null); // Pass null parameter
            }
        }
    }
}

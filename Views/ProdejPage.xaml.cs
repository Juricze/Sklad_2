using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
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
    }
}

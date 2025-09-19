using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Sklad_2.ViewModels;
using Sklad_2.Views.Dialogs;
using System;

namespace Sklad_2.Views
{
    public sealed partial class UctenkyPage : Page
    {
        public UctenkyViewModel ViewModel { get; set; }

        public UctenkyPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            ViewModel.LoadReceiptsCommand.Execute(null); // Volání LoadReceiptsCommand zpět
        }

        private async void ShowPreviewButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SelectedReceipt != null)
            {
                var previewDialog = new ReceiptPreviewDialog(ViewModel.SelectedReceipt)
                {
                    XamlRoot = this.XamlRoot,
                };
                await previewDialog.ShowAsync();
            }
        }
    }
}

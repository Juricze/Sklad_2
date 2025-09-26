using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
            ViewModel = (Application.Current as App).Services.GetRequiredService<UctenkyViewModel>();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            ViewModel.LoadReceiptsCommand.Execute(null);
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

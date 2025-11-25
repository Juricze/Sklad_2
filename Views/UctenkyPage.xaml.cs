using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Sklad_2.Models;
using Sklad_2.Services;
using Sklad_2.ViewModels;
using Sklad_2.Views.Dialogs;
using System;

namespace Sklad_2.Views
{
    public sealed partial class UctenkyPage : Page
    {
        public UctenkyViewModel ViewModel { get; }

        public UctenkyPage()
        {
            // IMPORTANT: ViewModel must be set BEFORE InitializeComponent() for x:Bind to work properly
            ViewModel = (Application.Current as App).Services.GetRequiredService<UctenkyViewModel>();

            this.InitializeComponent();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            ViewModel.LoadReceiptsCommand.Execute(null);
        }

        private void Filter_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.Tag is string tag)
            {
                if (System.Enum.TryParse<DateFilterType>(tag, out var filterType))
                {
                    ViewModel.SelectedFilterType = filterType;
                }
            }
        }

        private async void ShowPreviewButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SelectedReceipt != null)
            {
                var printService = (Application.Current as App).Services.GetRequiredService<IPrintService>();
                var previewDialog = new ReceiptPreviewDialog(ViewModel.SelectedReceipt, printService)
                {
                    XamlRoot = this.XamlRoot,
                };
                await previewDialog.ShowAsync();
            }
        }
    }
}

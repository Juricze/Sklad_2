using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Sklad_2.Services;
using Sklad_2.ViewModels;
using Sklad_2.Views.Dialogs;
using System;

namespace Sklad_2.Views
{
    public sealed partial class VratkyPage : Page
    {
        public VratkyViewModel ViewModel { get; }

        public VratkyPage()
        {
            // IMPORTANT: ViewModel must be set BEFORE InitializeComponent() for x:Bind to work properly
            ViewModel = (Application.Current as App).Services.GetRequiredService<VratkyViewModel>();

            this.InitializeComponent();
        }

        private async void ReceiptIdTextBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                var textBox = (TextBox)sender;
                await ViewModel.FindReceiptCommand.ExecuteAsync(null);
                textBox.Text = string.Empty;
                e.Handled = true;
            }
        }

        private async void ProcessReturnButton_Click(object sender, RoutedEventArgs e)
        {
            await ViewModel.ProcessReturnCommand.ExecuteAsync(null);
            var createdReturn = ViewModel.LastCreatedReturn;

            if (createdReturn != null)
            {
                var printService = (Application.Current as App).Services.GetRequiredService<IPrintService>();
                var dataService = (Application.Current as App).Services.GetRequiredService<IDataService>();
                var previewDialog = new ReturnPreviewDialog(createdReturn, printService, dataService)
                {
                    XamlRoot = this.XamlRoot,
                };
                await previewDialog.ShowAsync();
            }
        }
    }
}

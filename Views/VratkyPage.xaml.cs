using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Sklad_2.ViewModels;
using Sklad_2.Views.Dialogs;
using System;

namespace Sklad_2.Views
{
    public sealed partial class VratkyPage : Page
    {
        public VratkyViewModel ViewModel { get; set; }

        public VratkyPage()
        {
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
                var previewDialog = new ReturnPreviewDialog(createdReturn)
                {
                    XamlRoot = this.XamlRoot,
                };
                await previewDialog.ShowAsync();
            }
        }
    }
}

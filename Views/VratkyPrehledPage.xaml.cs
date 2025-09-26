using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Sklad_2.ViewModels;
using Sklad_2.Views.Dialogs;
using System;

namespace Sklad_2.Views
{
    public sealed partial class VratkyPrehledPage : Page
    {
        public VratkyPrehledViewModel ViewModel { get; set; }

        public VratkyPrehledPage()
        {
            this.InitializeComponent();
            ViewModel = (Application.Current as App).Services.GetRequiredService<VratkyPrehledViewModel>();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            ViewModel.LoadReturnsCommand.Execute(null);
        }

        private async void ShowReturnPreviewButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SelectedReturn != null)
            {
                var previewDialog = new ReturnPreviewDialog(ViewModel.SelectedReturn)
                {
                    XamlRoot = this.XamlRoot,
                };
                await previewDialog.ShowAsync();
            }
        }
    }
}
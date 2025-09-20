using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Sklad_2.ViewModels;
using Sklad_2.Views.Dialogs;
using Microsoft.UI.Xaml;
using System;

namespace Sklad_2.Views
{
    public sealed partial class VratkyPrehledPage : Page
    {
        public VratkyPrehledViewModel ViewModel { get; set; }

        public VratkyPrehledPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
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
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
    public sealed partial class VratkyPrehledPage : Page
    {
        public VratkyPrehledViewModel ViewModel { get; }

        public VratkyPrehledPage()
        {
            // IMPORTANT: ViewModel must be set BEFORE InitializeComponent() for x:Bind to work properly
            ViewModel = (Application.Current as App).Services.GetRequiredService<VratkyPrehledViewModel>();

            this.InitializeComponent();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            ViewModel.LoadReturnsCommand.Execute(null);
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

        private async void ShowReturnPreviewButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SelectedReturn != null)
            {
                var printService = (Application.Current as App).Services.GetRequiredService<IPrintService>();
                var dataService = (Application.Current as App).Services.GetRequiredService<IDataService>();
                var previewDialog = new ReturnPreviewDialog(ViewModel.SelectedReturn, printService, dataService)
                {
                    XamlRoot = this.XamlRoot,
                };
                await previewDialog.ShowAsync();
            }
        }
    }
}
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Sklad_2.Models;
using Sklad_2.ViewModels;
using System;
using Windows.ApplicationModel.DataTransfer;

namespace Sklad_2.Views
{
    public sealed partial class SkladPrehledPage : Page
    {
        public SkladPrehledViewModel ViewModel { get; }

        public SkladPrehledPage()
        {
            ViewModel = (Application.Current as App).Services.GetRequiredService<SkladPrehledViewModel>();
            this.InitializeComponent();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                ViewModel.LoadMovementsCommand.Execute(null);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading stock movements: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Inner exception: {ex.InnerException?.Message}");
            }
        }

        private void DateFilter_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton radioButton && radioButton.Tag is string filterTypeString)
            {
                if (Enum.TryParse<DateFilterType>(filterTypeString, out var filterType))
                {
                    ViewModel.SelectedDateFilter = filterType;
                }
            }
        }

        private void CopyEan_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string ean)
            {
                var dataPackage = new DataPackage();
                dataPackage.SetText(ean);
                Clipboard.SetContent(dataPackage);

                // Store original content
                var originalContent = button.Content;

                // Show confirmation
                var confirmText = new TextBlock
                {
                    Text = "✓ Zkopírováno",
                    FontSize = 11,
                    Opacity = 0.7
                };
                button.Content = confirmText;

                // Reset after 1 second
                var timer = new System.Threading.Timer(_ =>
                {
                    this.DispatcherQueue.TryEnqueue(() =>
                    {
                        button.Content = originalContent;
                    });
                }, null, 1000, System.Threading.Timeout.Infinite);
            }
        }
    }
}

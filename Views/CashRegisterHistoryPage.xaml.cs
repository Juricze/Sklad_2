using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Sklad_2.Models;
using Sklad_2.ViewModels;
using System;

namespace Sklad_2.Views
{
    public sealed partial class CashRegisterHistoryPage : Page
    {
        public CashRegisterHistoryViewModel ViewModel { get; }

        public CashRegisterHistoryPage()
        {
            this.InitializeComponent();
            ViewModel = (Application.Current as App).Services.GetRequiredService<CashRegisterHistoryViewModel>();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            ViewModel.LoadHistoryCommand.Execute(null);
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
    }
}
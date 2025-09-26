using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Sklad_2.ViewModels;

namespace Sklad_2.Views
{
    public sealed partial class CashRegisterHistoryPage : Page
    {
        public CashRegisterHistoryViewModel ViewModel { get; set; }

        public CashRegisterHistoryPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            ViewModel.LoadHistoryCommand.Execute(null);
        }
    }
}
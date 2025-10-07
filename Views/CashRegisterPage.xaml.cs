using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Sklad_2.ViewModels;

namespace Sklad_2.Views
{
    public sealed partial class CashRegisterPage : Page
    {
        public CashRegisterViewModel ViewModel { get; }

        public CashRegisterPage()
        {
            this.InitializeComponent();
            ViewModel = (App.Current as App).Services.GetService<CashRegisterViewModel>();
            // this.Loaded += (s, e) => ViewModel.LoadCashRegisterDataCommand.Execute(null);
        }
    }
}
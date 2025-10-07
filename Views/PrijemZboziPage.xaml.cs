using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Sklad_2.ViewModels;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml;
using System.Threading.Tasks;

namespace Sklad_2.Views
{
    public sealed partial class PrijemZboziPage : Page
    {
        public PrijemZboziViewModel ViewModel { get; }

        public PrijemZboziPage()
        {
            this.InitializeComponent();
            ViewModel = (Application.Current as App).Services.GetRequiredService<PrijemZboziViewModel>();
        }

        private async void EanTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                e.Handled = true;
                await ViewModel.FindProductDetailsCommand.ExecuteAsync(null);

                // Počkejte, dokud se produkt nenajde a UI se neaktualizuje
                await Task.Delay(100); // Krátké zpoždění pro jistotu

                var stockTextBox = FindName("StockQuantityTextBox") as TextBox;
                if (stockTextBox != null && stockTextBox.Visibility == Visibility.Visible)
                {
                    stockTextBox.Focus(FocusState.Programmatic);
                }
            }
        }

        private void StockQuantityTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                e.Handled = true;
                if (ViewModel.AddToStockCommand.CanExecute(null))
                {
                    ViewModel.AddToStockCommand.Execute(null);
                    EanTextBox.Focus(FocusState.Programmatic); // Vraťte focus na EAN pro další skenování
                }
            }
        }
    }
}

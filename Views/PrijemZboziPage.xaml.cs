using Microsoft.UI.Xaml.Controls;
using Sklad_2.ViewModels;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml;

namespace Sklad_2.Views
{
    public sealed partial class PrijemZboziPage : Page
    {
        public PrijemZboziViewModel ViewModel { get; set; }

        public PrijemZboziPage()
        {
            this.InitializeComponent();
        }

        private async void EanTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                var textBox = (TextBox)sender;
                await ViewModel.FindProductDetailsCommand.ExecuteAsync(null);
                e.Handled = true;
            }
        }
    }
}

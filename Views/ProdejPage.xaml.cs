using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Sklad_2.ViewModels;

namespace Sklad_2.Views
{
    public sealed partial class ProdejPage : Page
    {
        public ProdejViewModel ViewModel { get; set; }

        public ProdejPage()
        {
            this.InitializeComponent();
        }

        private void EanTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                // Pass the text directly from the TextBox to the command
                ViewModel.FindProductCommand.Execute(((TextBox)sender).Text);
            }
        }
    }
}

using Microsoft.UI.Xaml.Controls;
using Sklad_2.ViewModels;

namespace Sklad_2.Views
{
    public sealed partial class PrehledProdejuPage : Page
    {
        public PrehledProdejuViewModel ViewModel { get; set; }

        public PrehledProdejuPage()
        {
            this.InitializeComponent();
        }
    }
}

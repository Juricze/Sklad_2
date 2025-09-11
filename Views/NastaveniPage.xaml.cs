using Microsoft.UI.Xaml.Controls;
using Sklad_2.ViewModels;

namespace Sklad_2.Views
{
    public sealed partial class NastaveniPage : Page
    {
        public NastaveniViewModel ViewModel { get; set; }

        public NastaveniPage()
        {
            this.InitializeComponent();
        }
    }
}

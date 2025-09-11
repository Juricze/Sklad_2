using Microsoft.UI.Xaml.Controls;
using Sklad_2.ViewModels;

namespace Sklad_2.Views
{
    public sealed partial class NaskladneniPage : Page
    {
        public NaskladneniViewModel ViewModel { get; set; }

        public NaskladneniPage()
        {
            this.InitializeComponent();
        }
    }
}

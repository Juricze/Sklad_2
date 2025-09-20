using Microsoft.UI.Xaml.Controls;
using Sklad_2.ViewModels;

namespace Sklad_2.Views
{
    public sealed partial class NovyProduktPage : Page
    {
        public NovyProduktViewModel ViewModel { get; set; }

        public NovyProduktPage()
        {
            this.InitializeComponent();
        }
    }
}

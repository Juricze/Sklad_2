using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Sklad_2.ViewModels;

namespace Sklad_2.Views
{
    public sealed partial class NovyProduktPage : Page
    {
        public NovyProduktViewModel ViewModel { get; }

        public NovyProduktPage()
        {
            this.InitializeComponent();
            ViewModel = (Application.Current as App).Services.GetRequiredService<NovyProduktViewModel>();
        }
    }
}

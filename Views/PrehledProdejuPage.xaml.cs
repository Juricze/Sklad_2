using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Sklad_2.ViewModels;

namespace Sklad_2.Views
{
    public sealed partial class PrehledProdejuPage : Page
    {
        public PrehledProdejuViewModel ViewModel { get; }

        public PrehledProdejuPage()
        {
            this.InitializeComponent();
            ViewModel = (Application.Current as App).Services.GetRequiredService<PrehledProdejuViewModel>();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            ViewModel.LoadSalesDataCommand.Execute(null);
        }
    }
}

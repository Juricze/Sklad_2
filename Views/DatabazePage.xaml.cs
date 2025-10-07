using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Sklad_2.ViewModels;

namespace Sklad_2.Views
{
    public sealed partial class DatabazePage : Page
    {
        public DatabazeViewModel ViewModel { get; }

        public DatabazePage()
        {
            this.InitializeComponent();
            ViewModel = (Application.Current as App).Services.GetRequiredService<DatabazeViewModel>();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            // Load products when the page is navigated to
            ViewModel.LoadProductsCommand.Execute(null);
        }
    }
}
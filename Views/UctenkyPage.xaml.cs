using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Sklad_2.ViewModels;

namespace Sklad_2.Views
{
    public sealed partial class UctenkyPage : Page
    {
        public UctenkyViewModel ViewModel { get; set; }

        public UctenkyPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            ViewModel.LoadReceiptsCommand.Execute(null); // Volání LoadReceiptsCommand zpět
        }
    }
}
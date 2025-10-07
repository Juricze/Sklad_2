using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Sklad_2.ViewModels;

namespace Sklad_2.Views
{
    public sealed partial class NastaveniPage : Page
    {
        public NastaveniViewModel ViewModel { get; }

        public NastaveniPage()
        {
            this.InitializeComponent();
            ViewModel = ((App)Application.Current).Services.GetRequiredService<NastaveniViewModel>();
        }

        private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (args.InvokedItemContainer != null)
            {
                var tag = args.InvokedItemContainer.Tag.ToString();
                CompanySettingsPanel.Visibility = tag == "Company" ? Visibility.Visible : Visibility.Collapsed;
                VatSettingsPanel.Visibility = tag == "VAT" ? Visibility.Visible : Visibility.Collapsed;
                PasswordSettingsPanel.Visibility = tag == "Passwords" ? Visibility.Visible : Visibility.Collapsed;
                SystemSettingsPanel.Visibility = tag == "System" ? Visibility.Visible : Visibility.Collapsed;
                AboutPanel.Visibility = tag == "About" ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }
}

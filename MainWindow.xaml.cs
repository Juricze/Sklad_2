using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Sklad_2.ViewModels;
using Sklad_2.Views;
using System;

namespace Sklad_2
{
    public sealed partial class MainWindow : Window
    {
        private readonly ProdejViewModel _prodejViewModel;
        private readonly NaskladneniViewModel _naskladneniViewModel;
        private readonly DatabazeViewModel _databazeViewModel;
        private readonly NastaveniViewModel _nastaveniViewModel;

        public MainWindow()
        {
            this.InitializeComponent();
            //this.ExtendsContentIntoTitleBar = true;
            //SetTitleBar(NavView); // Use NavView as title bar drag region

            var services = (Application.Current as App).Services;
            _prodejViewModel = services.GetService<ProdejViewModel>();
            _naskladneniViewModel = services.GetService<NaskladneniViewModel>();
            _databazeViewModel = services.GetService<DatabazeViewModel>();
            _nastaveniViewModel = services.GetService<NastaveniViewModel>();

            var initialPage = ContentFrame.Content as ProdejPage;
            initialPage.ViewModel = _prodejViewModel;
        }

        private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            Page page;
            string tag;

            if (args.IsSettingsInvoked)
            {
                tag = "Nastaveni";
            }
            else if (args.InvokedItemContainer != null)
            {
                tag = args.InvokedItemContainer.Tag.ToString();
            }
            else
            {
                return;
            }

            switch (tag)
            {
                case "Prodej":
                    var prodejPage = new ProdejPage();
                    prodejPage.ViewModel = _prodejViewModel;
                    page = prodejPage;
                    break;
                case "Naskladneni":
                    var naskladneniPage = new NaskladneniPage();
                    naskladneniPage.ViewModel = _naskladneniViewModel;
                    page = naskladneniPage;
                    break;
                case "Databaze":
                    var databazePage = new DatabazePage();
                    databazePage.ViewModel = _databazeViewModel;
                    page = databazePage;
                    break;
                case "Nastaveni":
                    var nastaveniPage = new NastaveniPage();
                    nastaveniPage.ViewModel = _nastaveniViewModel;
                    page = nastaveniPage;
                    break;
                default:
                    var defaultPage = new ProdejPage();
                    defaultPage.ViewModel = _prodejViewModel;
                    page = defaultPage;
                    break;
            }
            ContentFrame.Content = page;
        }
    }
}

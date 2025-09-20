using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Sklad_2.ViewModels;
using Sklad_2.Views;
using System;
using WinRT; // Required for Window.As<ICompositionSupportsSystemBackdrop>()
using Microsoft.UI.Composition.SystemBackdrops;

namespace Sklad_2
{
    public sealed partial class MainWindow : Window
    {
        private readonly ProdejViewModel _prodejViewModel;
        private readonly PrijemZboziViewModel _prijemZboziViewModel;
        private readonly DatabazeViewModel _databazeViewModel;
        private readonly NastaveniViewModel _nastaveniViewModel;
        private readonly VratkyViewModel _vratkyViewModel;
        private readonly VratkyPrehledViewModel _vratkyPrehledViewModel;
        private readonly NovyProduktViewModel _novyProduktViewModel;
        private readonly PrehledProdejuViewModel _prehledProdejuViewModel;

        WindowsSystemDispatcherQueueHelper m_wsdqHelper; // See below for implementation.
        MicaController m_micaController;
        SystemBackdropConfiguration m_configurationSource;

        public MainWindow()
        {
            this.InitializeComponent();

            TrySetSystemBackdrop();

            var services = (Application.Current as App).Services;
            _prodejViewModel = services.GetService<ProdejViewModel>();
            _prijemZboziViewModel = services.GetService<PrijemZboziViewModel>();
            _databazeViewModel = services.GetService<DatabazeViewModel>();
            _nastaveniViewModel = services.GetService<NastaveniViewModel>();
            _vratkyViewModel = services.GetService<VratkyViewModel>();
            _vratkyPrehledViewModel = services.GetService<VratkyPrehledViewModel>();
            _novyProduktViewModel = services.GetService<NovyProduktViewModel>();
            _prehledProdejuViewModel = services.GetService<PrehledProdejuViewModel>();

            var initialPage = ContentFrame.Content as ProdejPage;
            initialPage.ViewModel = _prodejViewModel;
        }

        bool TrySetSystemBackdrop()
        {
            if (Microsoft.UI.Composition.SystemBackdrops.MicaController.IsSupported())
            {
                m_wsdqHelper = new WindowsSystemDispatcherQueueHelper();
                m_wsdqHelper.EnsureWindowsSystemDispatcherQueueController();

                // Hooking up the policy object.
                m_configurationSource = new SystemBackdropConfiguration();
                this.Activated += Window_Activated;
                this.Closed += Window_Closed;
                ((FrameworkElement)this.Content).ActualThemeChanged += Window_ThemeChanged;

                // Initial configuration state.
                m_configurationSource.IsInputActive = true;
                SetConfigurationSourceTheme();

                m_micaController = new Microsoft.UI.Composition.SystemBackdrops.MicaController();

                // Enable the system backdrop.
                // Note: Be sure to have "using WinRT;" to use the Window.As<...>() extension method.
                m_micaController.AddSystemBackdropTarget(this.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>());
                m_micaController.SetSystemBackdropConfiguration(m_configurationSource);
                return true; // succeeded
            }

            return false; // Mica is not supported on this system
        }

        private void Window_Activated(object sender, WindowActivatedEventArgs args)
        {
            m_configurationSource.IsInputActive = args.WindowActivationState != WindowActivationState.Deactivated;
        }

        private void Window_Closed(object sender, WindowEventArgs args)
        {
            // Make sure any Mica/Acrylic controller is disposed so it doesn't try to
            // use this closed window.
            if (m_micaController != null)
            {
                m_micaController.Dispose();
                m_micaController = null;
            }
            this.Activated -= Window_Activated;
            m_configurationSource = null;
        }

        private void Window_ThemeChanged(FrameworkElement sender, object args)
        {
            if (m_configurationSource != null)
            {
                SetConfigurationSourceTheme();
            }
        }

        private void SetConfigurationSourceTheme()
        {
            switch (((FrameworkElement)this.Content).ActualTheme)
            {
                case ElementTheme.Dark: m_configurationSource.Theme = Microsoft.UI.Composition.SystemBackdrops.SystemBackdropTheme.Dark; break;
                case ElementTheme.Light: m_configurationSource.Theme = Microsoft.UI.Composition.SystemBackdrops.SystemBackdropTheme.Light; break;
                case ElementTheme.Default: m_configurationSource.Theme = Microsoft.UI.Composition.SystemBackdrops.SystemBackdropTheme.Default; break;
            }
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
                    var prijemZboziPage = new PrijemZboziPage();
                    prijemZboziPage.ViewModel = _prijemZboziViewModel;
                    page = prijemZboziPage;
                    break;
                case "Vratky":
                    var vratkyPage = new VratkyPage();
                    vratkyPage.ViewModel = _vratkyViewModel;
                    page = vratkyPage;
                    break;
                case "Produkty": // Changed from "Databaze"
                    var databazePage = new DatabazePage();
                    databazePage.ViewModel = _databazeViewModel;
                    page = databazePage;
                    _databazeViewModel.LoadProductsCommand.Execute(null); // Explicitně načíst produkty
                    break;
                case "NovyProdukt":
                    var novyProduktPage = new NovyProduktPage();
                    novyProduktPage.ViewModel = _novyProduktViewModel;
                    page = novyProduktPage;
                    break;
                case "Uctenky": // New case
                    var uctenkyPage = new UctenkyPage();
                    uctenkyPage.ViewModel = (Application.Current as App).Services.GetService<UctenkyViewModel>(); // Get from DI
                    page = uctenkyPage;
                    break;
                case "VratkyPrehled":
                    var vratkyPrehledPage = new VratkyPrehledPage();
                    vratkyPrehledPage.ViewModel = _vratkyPrehledViewModel;
                    page = vratkyPrehledPage;
                    break;
                case "PrehledProdeju":
                    var prehledProdejuPage = new PrehledProdejuPage();
                    prehledProdejuPage.ViewModel = _prehledProdejuViewModel;
                    page = prehledProdejuPage;
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

    // This is a helper class for the Mica controller.
    class WindowsSystemDispatcherQueueHelper
    {
        [System.Runtime.InteropServices.DllImport("CoreMessaging.dll")]
        private static extern int CreateDispatcherQueueController([System.Runtime.InteropServices.In] DispatcherQueueOptions options, [System.Runtime.InteropServices.In, System.Runtime.InteropServices.Out] ref System.IntPtr dispatcherQueueController);

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        struct DispatcherQueueOptions
        {
            public int dwSize;
            public int threadType;
            public int apartmentType;
        }

        private object m_dispatcherQueueController = null;
        public void EnsureWindowsSystemDispatcherQueueController()
        {
            if (Windows.System.DispatcherQueue.GetForCurrentThread() != null)
            {
                // one already exists, so we'll just use it.
                return;
            }

            if (m_dispatcherQueueController == null)
            {
                DispatcherQueueOptions options;
                options.dwSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(DispatcherQueueOptions));
                options.threadType = 2;    // DQTYPE_THREAD_CURRENT
                options.apartmentType = 2; // DQTAT_COM_STA

                System.IntPtr dispatcherQueueController_ptr = System.IntPtr.Zero;
                CreateDispatcherQueueController(options, ref dispatcherQueueController_ptr);
            }
        }
    }
}
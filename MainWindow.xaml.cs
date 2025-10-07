using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Sklad_2.Services;
using Sklad_2.ViewModels;
using Sklad_2.Views;
using System;
using WinRT; // Required for Window.As<ICompositionSupportsSystemBackdrop>()
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml.Input;

namespace Sklad_2
{
    public sealed partial class MainWindow : Window
    {
        WindowsSystemDispatcherQueueHelper m_wsdqHelper; // See below for implementation.
        MicaController m_micaController;
        SystemBackdropConfiguration m_configurationSource;

        public MainWindow()
        {
            this.InitializeComponent();

            TrySetSystemBackdrop();

            // The initial page will get its ViewModel in its constructor.
            ContentFrame.Content = new ProdejPage();
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
            ((FrameworkElement)this.Content).ActualThemeChanged -= Window_ThemeChanged;
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
                    page = new ProdejPage();
                    break;
                case "Naskladneni":
                    page = new PrijemZboziPage();
                    break;
                case "Vratky":
                    page = new VratkyPage();
                    break;
                case "Pokladna":
                    page = new CashRegisterPage();
                    break;
                case "Produkty":
                    page = new DatabazePage();
                    break;
                case "NovyProdukt":
                    page = new NovyProduktPage();
                    break;
                case "Uctenky":
                    page = new UctenkyPage();
                    break;
                case "VratkyPrehled":
                    page = new VratkyPrehledPage();
                    break;
                case "PrehledProdeju":
                    page = new PrehledProdejuPage();
                    break;
                case "HistoriePokladny":
                    page = new CashRegisterHistoryPage();
                    break;
                case "Nastaveni":
                    page = new NastaveniPage();
                    break;
                default:
                    page = new ProdejPage();
                    break;
            }
            ContentFrame.Content = page;
        }

        private void Logout_Tapped(object sender, TappedRoutedEventArgs e)
        {
            var authService = (Application.Current as App).Services.GetService<IAuthService>();
            authService.Logout();
            var loginWindow = new LoginWindow();
            loginWindow.Activate();
            this.Close();
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
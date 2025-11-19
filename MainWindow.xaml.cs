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
        private readonly IAuthService _authService;
        private readonly ISettingsService _settingsService;
        private readonly bool IsSalesRole;
        private readonly bool IsAdmin;
        private bool _isClosing;

        public StatusBarViewModel StatusBarVM { get; }

        public MainWindow()
        {
            var app = Application.Current as App;
            var serviceProvider = app.Services;
            _authService = serviceProvider.GetRequiredService<IAuthService>();
            _settingsService = serviceProvider.GetRequiredService<ISettingsService>();
            StatusBarVM = serviceProvider.GetRequiredService<StatusBarViewModel>();
            IsSalesRole = _authService.CurrentUser?.Role == "Cashier";
            IsAdmin = _authService.CurrentUser?.Role == "Admin";

            this.InitializeComponent();
            StatusBarBorder.DataContext = this;

            // Update app's current window reference for pickers/dialogs in pages
            app.CurrentWindow = this;

            TrySetSystemBackdrop();

            // Refresh status bar
            _ = StatusBarVM.RefreshStatusAsync();

            // Load initial page when ContentFrame is ready
            ContentFrame.Loaded += OnContentFrameLoaded;

            // Handle new day dialog after window is activated
            this.Activated += OnFirstActivated;

            // Hide menu items based on role
            if (IsSalesRole)
            {
                foreach (var item in NavView.MenuItems)
                {
                    if (item is NavigationViewItem mainItem && mainItem.Tag as string == "Databaze")
                    {
                        foreach (var subItem in mainItem.MenuItems)
                        {
                            if (subItem is NavigationViewItem navItem)
                            {
                                var tag = navItem.Tag as string;
                                // Hide for Cashier role
                                if (tag == "PrehledProdeju" || tag == "NovyProdukt" || tag == "HistoriePokladny")
                                {
                                    navItem.Visibility = Visibility.Collapsed;
                                }
                            }
                        }
                        break;
                    }
                }
            }
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

        private bool _hasHandledNewDay = false;

        private void OnContentFrameLoaded(object sender, RoutedEventArgs e)
        {
            // Unsubscribe to prevent multiple calls
            ContentFrame.Loaded -= OnContentFrameLoaded;

            // The initial page will get its ViewModel in its constructor.
            ContentFrame.Content = new ProdejPage();

            // Set initial selected item in NavigationView
            foreach (var item in NavView.MenuItems)
            {
                if (item is NavigationViewItem navItem && navItem.Tag as string == "Prodej")
                {
                    NavView.SelectedItem = navItem;
                    break;
                }
            }
        }

        private async void OnFirstActivated(object sender, WindowActivatedEventArgs args)
        {
            // Only run once when window is first activated (not deactivated)
            if (_hasHandledNewDay || args.WindowActivationState == WindowActivationState.Deactivated)
                return;

            _hasHandledNewDay = true;
            this.Activated -= OnFirstActivated; // Unsubscribe

            // Check for new day if user is in Sales role
            if (IsSalesRole)
            {
                var currentDate = DateTime.Today;
                var lastLoginDate = _settingsService.CurrentSettings.LastSaleLoginDate?.Date;

                bool isNewDay = false;
                string promptMessage = "";

                if (lastLoginDate == null)
                {
                    isNewDay = true;
                    promptMessage = "Vítejte v novém obchodním dni! Pro zahájení prosím zadejte počáteční stav pokladny.";
                }
                else if (currentDate > lastLoginDate)
                {
                    isNewDay = true;
                    promptMessage = "Vítejte v novém obchodním dni! Pro zahájení prosím zadejte počáteční stav pokladny.";
                }
                else if (currentDate < lastLoginDate)
                {
                    isNewDay = true;
                    promptMessage = $"⚠️ VAROVÁNÍ: Detekována změna systémového času!\n\n" +
                                  $"Poslední přihlášení: {lastLoginDate:dd.MM.yyyy}\n" +
                                  $"Aktuální datum: {currentDate:dd.MM.yyyy}\n\n" +
                                  $"Systémový čas byl posunut zpět, což může být způsobeno chybou synchronizace nebo manipulací. " +
                                  $"Pro zachování integrity účetních dat je nutné zahájit nový obchodní den.\n\n" +
                                  $"Zadejte počáteční stav pokladny:";
                }

                if (isNewDay)
                {
                    // Ensure XamlRoot is available before showing dialog
                    // On slower machines, window might not be fully rendered yet
                    int retries = 0;
                    while (this.Content?.XamlRoot == null && retries < 20)
                    {
                        await System.Threading.Tasks.Task.Delay(50);
                        retries++;
                    }

                    var newDayDialog = new Views.Dialogs.NewDayConfirmationDialog();
                    newDayDialog.SetPromptText(promptMessage);
                    newDayDialog.XamlRoot = this.Content.XamlRoot;

                    var result = await newDayDialog.ShowAsync();

                    if (result == ContentDialogResult.Primary)
                    {
                        // Set day start cash with the entered amount
                        var cashRegisterService = (Application.Current as App).Services.GetRequiredService<ICashRegisterService>();
                        await cashRegisterService.SetDayStartCashAsync(newDayDialog.InitialAmount);

                        // Update last login date
                        _settingsService.CurrentSettings.LastSaleLoginDate = currentDate;
                        await _settingsService.SaveSettingsAsync();

                        // Note: CashRegisterViewModel will load data when user navigates to Pokladna page
                        // (via Loaded event in CashRegisterPage)
                    }
                    else
                    {
                        // User cancelled, close the app as initial amount is mandatory
                        Application.Current.Exit();
                    }
                }
            }
        }

        private void Window_Activated(object sender, WindowActivatedEventArgs args)
        {
            m_configurationSource.IsInputActive = args.WindowActivationState != WindowActivationState.Deactivated;
        }

        private async void Window_Closed(object sender, WindowEventArgs args)
        {
            // Prevent multiple executions
            if (_isClosing)
                return;

            _isClosing = true;

            // Always cancel initial close to show backup dialog
            args.Handled = true;

            // Check if day close was performed (only for Sales role)
            if (IsSalesRole)
            {
                var lastDayCloseDate = _settingsService.CurrentSettings.LastDayCloseDate;
                bool isDayClosedToday = lastDayCloseDate?.Date == DateTime.Today;

                if (!isDayClosedToday)
                {
                    // Show warning dialog
                    var dialog = new ContentDialog
                    {
                        Title = "Uzavírka dne nebyla provedena",
                        Content = "Nebyla provedena uzavírka dne.\n\nPřejděte do Pokladny a proveďte uzavírku dne před zavřením aplikace.\n\nOpravdu si přejete zavřít aplikaci?",
                        PrimaryButtonText = "Zavřít aplikaci",
                        CloseButtonText = "Zrušit",
                        DefaultButton = ContentDialogButton.Close,
                        XamlRoot = this.Content.XamlRoot
                    };

                    var result = await dialog.ShowAsync();

                    if (result != ContentDialogResult.Primary)
                    {
                        // User cancelled - window stays open, reset flag
                        _isClosing = false;
                        return;
                    }
                }
            }

            // Perform backup in background
            await System.Threading.Tasks.Task.Run(() => PerformDatabaseSync());

            // Show completion dialog
            var completionDialog = new ContentDialog
            {
                Title = "Záloha dokončena",
                Content = "Databáze byla úspěšně zálohována.\n\nAplikace bude nyní zavřena.",
                CloseButtonText = "OK",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.Content.XamlRoot
            };

            await completionDialog.ShowAsync();

            // Unsubscribe from Closed event
            this.Closed -= Window_Closed;

            // Exit application with success code
            this.DispatcherQueue.TryEnqueue(() => System.Environment.Exit(0));
        }

        private void PerformDatabaseSync()
        {
            try
            {
                var appDataPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData);
                var sourceFolderPath = System.IO.Path.Combine(appDataPath, "Sklad_2_Data");
                var sourceDbPath = System.IO.Path.Combine(sourceFolderPath, "sklad.db");

                // Determine backup path with inline logic (avoid service calls during disposal)
                string backupFolderPath;

                // Try to read custom path from settings file directly
                var settingsPath = System.IO.Path.Combine(sourceFolderPath, "AppSettings.json");
                string customBackupPath = null;

                if (System.IO.File.Exists(settingsPath))
                {
                    try
                    {
                        var json = System.IO.File.ReadAllText(settingsPath);
                        var settings = System.Text.Json.JsonSerializer.Deserialize<Models.Settings.AppSettings>(json);
                        customBackupPath = settings?.BackupPath;
                    }
                    catch { /* Ignore parsing errors */ }
                }

                // Priority 1: Custom BackupPath from settings
                if (!string.IsNullOrWhiteSpace(customBackupPath) && System.IO.Directory.Exists(customBackupPath))
                {
                    backupFolderPath = System.IO.Path.Combine(customBackupPath, "Sklad_2_Data");
                }
                // Priority 2: OneDrive
                else
                {
                    string oneDrivePath = System.Environment.GetEnvironmentVariable("OneDrive");
                    if (!string.IsNullOrEmpty(oneDrivePath) && System.IO.Directory.Exists(oneDrivePath))
                    {
                        backupFolderPath = System.IO.Path.Combine(oneDrivePath, "Sklad_2_Data");
                    }
                    // Priority 3: Documents (fallback)
                    else
                    {
                        backupFolderPath = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), "Sklad_2_Backups");
                    }
                }

                System.IO.Directory.CreateDirectory(backupFolderPath);
                var backupFilePath = System.IO.Path.Combine(backupFolderPath, "sklad.db");

                if (System.IO.File.Exists(sourceDbPath))
                {
                    System.IO.File.Copy(sourceDbPath, backupFilePath, true);

                    var sourceSettingsPath = System.IO.Path.Combine(sourceFolderPath, "AppSettings.json");
                    var backupSettingsPath = System.IO.Path.Combine(backupFolderPath, "AppSettings.json");
                    if (System.IO.File.Exists(sourceSettingsPath))
                    {
                        System.IO.File.Copy(sourceSettingsPath, backupSettingsPath, true);
                    }
                }
            }
            catch
            {
                // Silent fail - don't block app close
            }
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
            m_configurationSource.Theme = ((FrameworkElement)this.Content).ActualTheme switch
            {
                ElementTheme.Dark => Microsoft.UI.Composition.SystemBackdrops.SystemBackdropTheme.Dark,
                ElementTheme.Light => Microsoft.UI.Composition.SystemBackdrops.SystemBackdropTheme.Light,
                ElementTheme.Default => Microsoft.UI.Composition.SystemBackdrops.SystemBackdropTheme.Default,
                _ => Microsoft.UI.Composition.SystemBackdrops.SystemBackdropTheme.Default
            };
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

            // Ignore clicks on "Databaze" parent item (it should only expand/collapse)
            if (tag == "Databaze")
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

            // Set SelectedItem to the correct NavigationViewItem
            SetSelectedNavigationItem(tag);

            // Refresh status bar after navigation
            _ = StatusBarVM.RefreshStatusAsync();
        }

        private void SetSelectedNavigationItem(string tag)
        {
            // First check main menu items
            foreach (var item in NavView.MenuItems)
            {
                if (item is NavigationViewItem navItem)
                {
                    if (navItem.Tag as string == tag)
                    {
                        NavView.SelectedItem = navItem;
                        return;
                    }

                    // Check sub-menu items
                    foreach (var subItem in navItem.MenuItems)
                    {
                        if (subItem is NavigationViewItem subNavItem && subNavItem.Tag as string == tag)
                        {
                            NavView.SelectedItem = subNavItem;
                            return;
                        }
                    }
                }
            }

            // Check if it's settings
            if (tag == "Nastaveni")
            {
                NavView.SelectedItem = NavView.SettingsItem;
            }
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
                var options = new DispatcherQueueOptions
                {
                    dwSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(DispatcherQueueOptions)),
                    threadType = 2,    // DQTYPE_THREAD_CURRENT
                    apartmentType = 2  // DQTAT_COM_STA
                };

                System.IntPtr dispatcherQueueController_ptr = System.IntPtr.Zero;
                CreateDispatcherQueueController(options, ref dispatcherQueueController_ptr);
            }
        }
    }
}
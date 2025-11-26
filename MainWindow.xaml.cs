using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Sklad_2.Services;
using Sklad_2.ViewModels;
using Sklad_2.Views;
using Sklad_2.Messages;
using System;
using System.Linq;
using System.Threading.Tasks;
using WinRT; // Required for Window.As<ICompositionSupportsSystemBackdrop>()
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Windowing;

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
        private Storyboard _statusBarBlinkAnimation;

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
            RootGrid.DataContext = this;

            // Update app's current window reference for pickers/dialogs in pages
            app.CurrentWindow = this;

            // Use AppWindow.Closing event for reliable close handling on Win10/Win11
            var appWindow = GetAppWindowForCurrentWindow();
            if (appWindow != null)
            {
                appWindow.Closing += AppWindow_Closing;
            }

            TrySetSystemBackdrop();

            // Refresh status bar
            _ = StatusBarVM.RefreshStatusAsync();

            // Listen for settings changes to refresh blinking animations
            var messenger = serviceProvider.GetRequiredService<IMessenger>();
            messenger.Register<SettingsChangedMessage>(this, (r, m) => RefreshStatusBarBlink());

            // Load initial page when ContentFrame is ready
            ContentFrame.Loaded += OnContentFrameLoaded;

            // Handle new day dialog after window is activated
            this.Activated += OnFirstActivated;

            // Hide menu items based on role
            if (IsSalesRole || !IsAdmin)
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
                                // Hide "Sklad (Přehled)" for non-Admin
                                if (tag == "SkladPrehled" && !IsAdmin)
                                {
                                    navItem.Visibility = Visibility.Collapsed;
                                }
                                // Hide "Dárkové poukazy" for non-Admin
                                if (tag == "Poukazy" && !IsAdmin)
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

            // Check if backup path is configured - mandatory for all users
            if (!_settingsService.IsBackupPathConfigured())
            {
                await ShowBackupPathRequiredDialog();
                return;
            }

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

        private void BackupPathStatusPanel_Loaded(object sender, RoutedEventArgs e)
        {
            // Start or stop blinking animation based on backup path configuration
            var panel = sender as StackPanel;
            if (panel != null)
            {
                if (!StatusBarVM.IsBackupPathConfigured)
                {
                    // Start blinking for error state
                    var storyboard = new Storyboard();
                    var animation = new DoubleAnimationUsingKeyFrames
                    {
                        Duration = new Duration(TimeSpan.FromSeconds(1)),
                        RepeatBehavior = RepeatBehavior.Forever
                    };
                    animation.KeyFrames.Add(new DiscreteDoubleKeyFrame { KeyTime = TimeSpan.Zero, Value = 1.0 });
                    animation.KeyFrames.Add(new DiscreteDoubleKeyFrame { KeyTime = TimeSpan.FromSeconds(0.5), Value = 0.3 });
                    
                    Storyboard.SetTarget(animation, panel);
                    Storyboard.SetTargetProperty(animation, "Opacity");
                    storyboard.Children.Add(animation);
                    _statusBarBlinkAnimation = storyboard;
                    storyboard.Begin();
                }
                else
                {
                    // Stop blinking for normal state
                    if (_statusBarBlinkAnimation != null)
                    {
                        _statusBarBlinkAnimation.Stop();
                        _statusBarBlinkAnimation = null;
                        panel.Opacity = 1.0; // Reset opacity
                    }
                }
            }
        }

        public void RefreshStatusBarBlink()
        {
            // Method to refresh blinking state - called when backup path changes
            if (BackupPathStatusPanel != null)
            {
                BackupPathStatusPanel_Loaded(BackupPathStatusPanel, null);
            }
        }


        private async Task ShowBackupPathRequiredDialog()
        {
            // Wait for XamlRoot to be available (same as new day dialog)
            int attempts = 0;
            while (this.Content?.XamlRoot == null && attempts < 20)
            {
                await Task.Delay(50);
                attempts++;
            }

            if (this.Content?.XamlRoot == null)
            {
                System.Diagnostics.Debug.WriteLine("Failed to get XamlRoot for backup path dialog");
                return;
            }

            var dialog = new ContentDialog()
            {
                Title = "Nastavení cesty pro zálohy",
                Content = "Pro používání aplikace je nutné nastavit cestu pro zálohy databáze.\n\n" +
                         "Bez této cesty není možné:\n" +
                         "• Provádět prodeje\n" +
                         "• Zálohovat data\n" +
                         "• Exportovat účtenky\n\n" +
                         "Přejděte do Nastavení → Systém a nastavte cestu pro zálohy.",
                PrimaryButtonText = "Jít do Nastavení",
                SecondaryButtonText = "Zavřít aplikaci",
                XamlRoot = this.Content.XamlRoot
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                // Navigate to Settings
                NavView.SelectedItem = NavView.MenuItems.Cast<NavigationViewItem>()
                    .FirstOrDefault(item => item.Tag?.ToString() == "Nastaveni");
                var settingsPage = new Views.NastaveniPage();
                ContentFrame.Content = settingsPage;
                
                // Navigate directly to System panel
                settingsPage.NavigateToSystemPanel();
            }
            else
            {
                // Close application
                Application.Current.Exit();
            }
        }

        private AppWindow GetAppWindowForCurrentWindow()
        {
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var winId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            return AppWindow.GetFromWindowId(winId);
        }

        private async void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
        {
            // Log to file for debugging
            var logPath = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "Sklad_2_Data", "backup_log.txt");
            void Log(string msg)
            {
                try { System.IO.File.AppendAllText(logPath, $"{DateTime.Now:HH:mm:ss} - {msg}\n"); } catch { }
            }

            Log("AppWindow_Closing: Event triggered");

            // Prevent multiple executions
            if (_isClosing)
            {
                Log("AppWindow_Closing: Already closing, returning");
                return;
            }

            // Cancel close to show dialogs
            args.Cancel = true;
            _isClosing = true;
            Log("AppWindow_Closing: Set _isClosing = true, args.Cancel = true");

            // Check if day close was performed (only for Sales role)
            if (IsSalesRole)
            {
                var lastDayCloseDate = _settingsService.CurrentSettings.LastDayCloseDate;
                bool isDayClosedToday = lastDayCloseDate?.Date == DateTime.Today;

                if (!isDayClosedToday)
                {
                    Log("AppWindow_Closing: Day not closed, showing warning dialog");
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
                        Log("AppWindow_Closing: User cancelled, resetting _isClosing");
                        return;
                    }
                }
            }

            Log("AppWindow_Closing: Performing backup...");
            // Perform backup in background
            await System.Threading.Tasks.Task.Run(() => PerformDatabaseSync());

            Log("AppWindow_Closing: Showing completion dialog...");
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

            Log("AppWindow_Closing: Exiting application...");
            // Exit application with success code
            System.Environment.Exit(0);
        }

        // Keep old handler for backwards compatibility but it shouldn't be called
        private async void Window_Closed(object sender, WindowEventArgs args)
        {
            // This is now handled by AppWindow_Closing
            // Keep this as fallback
        }

        private void PerformDatabaseSync()
        {
            var logPath = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "Sklad_2_Data", "backup_log.txt");
            void Log(string msg)
            {
                try { System.IO.File.AppendAllText(logPath, $"{DateTime.Now:HH:mm:ss} - {msg}\n"); } catch { }
                System.Diagnostics.Debug.WriteLine(msg);
            }

            try
            {
                var appDataPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData);
                var sourceFolderPath = System.IO.Path.Combine(appDataPath, "Sklad_2_Data");
                var sourceDbPath = System.IO.Path.Combine(sourceFolderPath, "sklad.db");

                Log("PerformDatabaseSync: Starting backup...");
                Log($"PerformDatabaseSync: Source folder: {sourceFolderPath}");
                Log($"PerformDatabaseSync: Source DB exists: {System.IO.File.Exists(sourceDbPath)}");

                // Determine backup path with inline logic (avoid service calls during disposal)
                string backupFolderPath;

                // Try to read custom path from settings file directly
                var settingsPath = System.IO.Path.Combine(sourceFolderPath, "settings.json");
                string customBackupPath = null;

                Log($"PerformDatabaseSync: Settings file path: {settingsPath}");
                Log($"PerformDatabaseSync: Settings file exists: {System.IO.File.Exists(settingsPath)}");

                if (System.IO.File.Exists(settingsPath))
                {
                    try
                    {
                        var json = System.IO.File.ReadAllText(settingsPath);
                        Log($"PerformDatabaseSync: Settings JSON loaded, length: {json.Length}");
                        var settings = System.Text.Json.JsonSerializer.Deserialize<Models.Settings.AppSettings>(json);
                        customBackupPath = settings?.BackupPath;
                        Log($"PerformDatabaseSync: BackupPath from settings: '{customBackupPath}'");
                    }
                    catch (Exception ex)
                    {
                        Log($"PerformDatabaseSync: Error parsing settings: {ex.Message}");
                    }
                }

                // Only backup if custom backup path is configured
                if (!string.IsNullOrWhiteSpace(customBackupPath))
                {
                    backupFolderPath = System.IO.Path.Combine(customBackupPath, "Sklad_2_Data");
                    Log($"PerformDatabaseSync: Backup folder path: {backupFolderPath}");
                }
                else
                {
                    // No backup path configured - skip backup
                    Log("PerformDatabaseSync: Backup path not configured - skipping backup on close");
                    return;
                }

                System.IO.Directory.CreateDirectory(backupFolderPath);
                var backupFilePath = System.IO.Path.Combine(backupFolderPath, "sklad.db");

                if (System.IO.File.Exists(sourceDbPath))
                {
                    System.IO.File.Copy(sourceDbPath, backupFilePath, true);
                    Log($"PerformDatabaseSync: Database copied to {backupFilePath}");

                    var sourceSettingsPath = System.IO.Path.Combine(sourceFolderPath, "settings.json");
                    var backupSettingsPath = System.IO.Path.Combine(backupFolderPath, "settings.json");
                    if (System.IO.File.Exists(sourceSettingsPath))
                    {
                        System.IO.File.Copy(sourceSettingsPath, backupSettingsPath, true);
                        Log($"PerformDatabaseSync: Settings copied to {backupSettingsPath}");
                    }

                    Log("PerformDatabaseSync: Backup completed successfully!");
                }
                else
                {
                    Log($"PerformDatabaseSync: Source database not found at {sourceDbPath}");
                }
            }
            catch (Exception ex)
            {
                Log($"PerformDatabaseSync: ERROR - {ex.Message}");
                Log($"PerformDatabaseSync: Stack trace - {ex.StackTrace}");
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
                case "SkladPrehled":
                    page = new SkladPrehledPage();
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
                case "Poukazy":
                    page = new PoukazyPage();
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
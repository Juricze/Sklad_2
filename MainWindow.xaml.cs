using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Sklad_2.Services;
using Sklad_2.ViewModels;
using Sklad_2.Views;
using Sklad_2.Messages;
using System;
using System.Diagnostics;
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
        private readonly IDailyCloseService _dailyCloseService;
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
            _dailyCloseService = serviceProvider.GetRequiredService<IDailyCloseService>();
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

        private async void OnContentFrameLoaded(object sender, RoutedEventArgs e)
        {
            // Unsubscribe to prevent multiple calls
            ContentFrame.Loaded -= OnContentFrameLoaded;

            // Check if session day is closed - if yes, navigate to TrzbyUzavirky instead of Prodej
            var sessionDate = _settingsService.CurrentSettings.LastSaleLoginDate?.Date ?? DateTime.Today;
            bool isDayClosed = await _dailyCloseService.IsDayClosedAsync(sessionDate);

            if (IsSalesRole && isDayClosed)
            {
                // Day is closed → navigate to TrzbyUzavirky
                ContentFrame.Content = new Views.TrzbyUzavirkPage();
                foreach (var item in NavView.MenuItems)
                {
                    if (item is NavigationViewItem navItem && navItem.Tag as string == "TrzbyUzavirky")
                    {
                        NavView.SelectedItem = navItem;
                        break;
                    }
                }
            }
            else
            {
                // Normal flow → navigate to Prodej
                ContentFrame.Content = new ProdejPage();
                foreach (var item in NavView.MenuItems)
                {
                    if (item is NavigationViewItem navItem && navItem.Tag as string == "Prodej")
                    {
                        NavView.SelectedItem = navItem;
                        break;
                    }
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

            // Check for new day if user is in Cashier role (Sales role)
            if (IsSalesRole)
            {
                await HandleNewDayCheckAsync();
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

        private async Task HandleNewDayCheckAsync()
        {
            // Wait for XamlRoot to be available
            int attempts = 0;
            while (this.Content?.XamlRoot == null && attempts < 20)
            {
                await Task.Delay(50);
                attempts++;
            }

            if (this.Content?.XamlRoot == null)
            {
                System.Diagnostics.Debug.WriteLine("Failed to get XamlRoot for new day dialog");
                return;
            }

            var currentDate = DateTime.Today;
            var lastLoginDate = _settingsService.CurrentSettings.LastSaleLoginDate?.Date;

            // Kontrola systémového času (zda nebyl posunut zpět)
            bool timeWarning = false;
            if (lastLoginDate.HasValue && currentDate < lastLoginDate)
            {
                timeWarning = true;
            }

            // Kontrola, zda byl den uzavřen
            bool wasDayClosed = await _dailyCloseService.IsDayClosedAsync(currentDate);

            // Rozhodnout, zda zobrazit dialog
            bool showDialog = false;
            string dialogMessage = "";

            if (timeWarning)
            {
                // Systémový čas byl posunut zpět - důrazné varování
                showDialog = true;
                dialogMessage = $"⚠️ KRITICKÉ VAROVÁNÍ: Detekována změna systémového času!\n\n" +
                              $"Poslední přihlášení: {lastLoginDate:dd.MM.yyyy}\n" +
                              $"Aktuální datum: {currentDate:dd.MM.yyyy}\n\n" +
                              $"Systémový čas byl posunut zpět. Toto může být způsobeno:\n" +
                              $"• Chybou synchronizace času\n" +
                              $"• Nesprávným nastavením systémového data\n\n" +
                              $"DŮRAZNĚ DOPORUČUJEME:\n" +
                              $"1. Zkontrolovat a opravit systémové datum\n" +
                              $"2. Restartovat aplikaci\n\n" +
                              $"Chcete přesto zahájit nový obchodní den?";
            }
            else if (wasDayClosed)
            {
                // Den byl uzavřen - dotaz na nový den s varováním
                showDialog = true;
                dialogMessage = $"🔒 Den byl již uzavřen!\n\n" +
                              $"Pro dnešní den ({currentDate:dd.MM.yyyy}) byla již provedena uzavírka.\n\n" +
                              $"⚠️ VAROVÁNÍ:\n" +
                              $"Vytvoření nového obchodního dne je vhodné pouze pokud:\n" +
                              $"• Začíná nový kalendářní den\n" +
                              $"• Systémové datum je správné\n\n" +
                              $"Pokud máte pochybnosti, zkontrolujte systémové datum a restartujte aplikaci.\n\n" +
                              $"Jste si jisti, že chcete zahájit nový obchodní den?";
            }
            else if (!lastLoginDate.HasValue || currentDate > lastLoginDate)
            {
                // KRITICKÁ KONTROLA NEJDŘÍV: Je uzavřen předchozí den?
                if (lastLoginDate.HasValue && lastLoginDate.Value < currentDate)
                {
                    // Zkontrolovat, zda existuje uzavírka pro lastLoginDate
                    bool isPreviousDayClosed = await _dailyCloseService.IsDayClosedAsync(lastLoginDate.Value);

                    if (!isPreviousDayClosed)
                    {
                        // BLOKACE: Předchozí den není uzavřen!
                        var blockDialog = new ContentDialog
                        {
                            Title = "⚠️ Nelze zahájit nový den",
                            Content = $"Nebyla provedena uzavírka pro předchozí den!\n\n" +
                                     $"Datum předchozího dne: {lastLoginDate.Value:dd.MM.yyyy}\n" +
                                     $"Aktuální datum: {currentDate:dd.MM.yyyy}\n\n" +
                                     $"POVINNÝ POSTUP:\n" +
                                     $"1. Nejprve proveďte uzavírku pro den {lastLoginDate.Value:dd.MM.yyyy}\n" +
                                     $"2. Poté můžete zahájit nový den {currentDate:dd.MM.yyyy}\n\n" +
                                     $"Aplikace vás nyní přesměruje na stránku Tržby/Uzavírky.",
                            CloseButtonText = "Rozumím, provést uzavírku",
                            XamlRoot = this.Content.XamlRoot
                        };

                        await blockDialog.ShowAsync();
                        await Task.Delay(300); // Dialog close delay

                        // Navigovat na Tržby/Uzavírky stránku
                        NavView.SelectedItem = NavView.MenuItems.Cast<NavigationViewItem>()
                            .FirstOrDefault(item => item.Tag?.ToString() == "TrzbyUzavirky");
                        ContentFrame.Content = new Views.TrzbyUzavirkPage();

                        return; // ZASTAVIT - nejdřív musí uzavřít předchozí den
                    }
                }

                // Předchozí den je uzavřen (nebo neexistuje) → lze zahájit nový den
                showDialog = true;
                dialogMessage = $"📅 Je nový obchodní den?\n\n" +
                              $"Dnešní datum: {currentDate:dd.MM.yyyy}\n\n" +
                              $"Chcete zahájit nový obchodní den?";
            }

            if (showDialog)
            {
                var dialog = new ContentDialog
                {
                    Title = timeWarning || wasDayClosed ? "⚠️ Upozornění" : "Nový den",
                    Content = dialogMessage,
                    PrimaryButtonText = "Ano, zahájit nový den",
                    CloseButtonText = wasDayClosed || timeWarning ? "Ne, ukončit aplikaci" : "Ne",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = this.Content.XamlRoot
                };

                var result = await dialog.ShowAsync();

                if (result == ContentDialogResult.Primary)
                {
                    // Potvrzovací dialog
                    var confirmDialog = new ContentDialog
                    {
                        Title = "Potvrzení nového dne",
                        Content = "Opravdu si přejete zahájit nový obchodní den?\n\n" +
                                 "Tato akce je nevratná.",
                        PrimaryButtonText = "Ano, potvrdit",
                        CloseButtonText = "Zrušit",
                        DefaultButton = ContentDialogButton.Close,
                        XamlRoot = this.Content.XamlRoot
                    };

                    var confirmResult = await confirmDialog.ShowAsync();

                    if (confirmResult == ContentDialogResult.Primary)
                    {
                        // Kontrola uzavírky už proběhla výše → můžeme zahájit nový den
                        // Update last login date
                        _settingsService.CurrentSettings.LastSaleLoginDate = currentDate;
                        await _settingsService.SaveSettingsAsync();
                        await Task.Delay(300); // Win10 file flush + settings propagation

                        // Notify all ViewModels about settings change (especially TrzbyUzavirkViewModel)
                        CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new Sklad_2.Messages.SettingsChangedMessage());
                        await Task.Delay(300); // Win10 UI refresh

                        System.Diagnostics.Debug.WriteLine($"New day started: {currentDate:yyyy-MM-dd}");
                    }
                    else
                    {
                        // User cancelled confirmation
                        Application.Current.Exit();
                    }
                }
                else
                {
                    // User clicked "Ne" or closed dialog
                    if (wasDayClosed || timeWarning)
                    {
                        // Exit application if day was closed or time warning
                        Application.Current.Exit();
                    }
                    else
                    {
                        // User declined to start new day → redirect to TrzbyUzavirky
                        // so they must close previous day first
                        NavView.SelectedItem = NavView.MenuItems.Cast<NavigationViewItem>()
                            .FirstOrDefault(item => item.Tag?.ToString() == "TrzbyUzavirky");
                        ContentFrame.Content = new Views.TrzbyUzavirkPage();
                    }
                }
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
                var sessionDate = _settingsService.CurrentSettings.LastSaleLoginDate?.Date ?? DateTime.Today;

                // Use DailyCloseService to check if session day is closed
                bool isDayClosedToday = await _dailyCloseService.IsDayClosedAsync(sessionDate);

                if (!isDayClosedToday)
                {
                    Log("AppWindow_Closing: Day not closed, showing warning dialog");
                    // Show warning dialog
                    var dialog = new ContentDialog
                    {
                        Title = "Uzavírka dne nebyla provedena",
                        Content = $"Nebyla provedena uzavírka pro den {sessionDate:dd.MM.yyyy}.\n\n" +
                                 "Přejděte do Tržby/Uzavírky a proveďte uzavírku dne před zavřením aplikace.\n\n" +
                                 "Opravdu si přejete zavřít aplikaci bez uzavírky?",
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
        private void Window_Closed(object sender, WindowEventArgs args)
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


        private async void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
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

            // Block Prodej/Vratky for Admin role - must use Cashier account
            if (IsAdmin && (tag == "Prodej" || tag == "Vratky"))
            {
                // Wait for XamlRoot to be available (max 1 second)
                int retryCount = 0;
                while (this.Content?.XamlRoot == null && retryCount < 20)
                {
                    await Task.Delay(50);
                    retryCount++;
                }

                if (this.Content?.XamlRoot != null)
                {
                    try
                    {
                        var dialog = new ContentDialog
                        {
                            Title = "⚠️ Přístup odepřen",
                            Content = "Administrátor nemůže provádět prodeje a vratky.\n\n" +
                                      "Pro provedení prodeje nebo vratky se přihlaste na účet pokladního.",
                            CloseButtonText = "OK",
                            XamlRoot = this.Content.XamlRoot
                        };

                        await dialog.ShowAsync();
                        await Task.Delay(300); // Win10 compatibility - ensure dialog fully closes
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"MainWindow: Error showing admin block dialog: {ex.Message}");
                    }
                }
                return; // Prevent navigation
            }

            // Block Prodej/Vratky if day is closed for Cashier role
            if (IsSalesRole && (tag == "Prodej" || tag == "Vratky"))
            {
                var sessionDate = _settingsService.CurrentSettings.LastSaleLoginDate?.Date ?? DateTime.Today;
                var isDayClosed = await _dailyCloseService.IsDayClosedAsync(sessionDate);
                if (isDayClosed)
                {
                    // Wait for XamlRoot to be available (max 1 second)
                    int retryCount = 0;
                    while (this.Content?.XamlRoot == null && retryCount < 20)
                    {
                        await Task.Delay(50);
                        retryCount++;
                    }

                    if (this.Content?.XamlRoot != null)
                    {
                        try
                        {
                            var dialog = new ContentDialog
                            {
                                Title = "🔒 Den uzavřen",
                                Content = $"Den {sessionDate:dd.MM.yyyy} byl již uzavřen.\n\n" +
                                          "Prodej a vratky jsou uzamčeny.\n\n" +
                                          "Pro pokračování se odhlaste a znovu přihlaste pro zahájení nového dne.",
                                CloseButtonText = "OK",
                                XamlRoot = this.Content.XamlRoot
                            };

                            await dialog.ShowAsync();
                            await Task.Delay(300); // Win10 compatibility - ensure dialog fully closes
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"MainWindow: Error showing day closed dialog: {ex.Message}");
                        }
                    }
                    return; // Prevent navigation
                }
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
                case "Vernostni":
                    page = new LoyaltyPage();
                    break;
                case "TrzbyUzavirky":
                    page = new TrzbyUzavirkPage();
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

        private async void Logout_Tapped(object sender, TappedRoutedEventArgs e)
        {
            try
            {
                var authService = (Application.Current as App).Services.GetService<IAuthService>();
                authService.Logout();

                var loginWindow = new LoginWindow();

                // Win11 compatibility - ensure LoginWindow is fully activated before closing MainWindow
                loginWindow.Activate();
                await Task.Delay(500); // Give Win11 time to fully activate the new window

                this.Close();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MainWindow: Error during logout: {ex.Message}");
                // Ensure window closes even if there's an error
                try
                {
                    this.Close();
                }
                catch { }
            }
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
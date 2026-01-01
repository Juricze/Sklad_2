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
using Microsoft.EntityFrameworkCore;
using Sklad_2.Data;

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
        private readonly IDbContextFactory<DatabaseContext> _contextFactory;
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
            _contextFactory = serviceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();
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

                // Maximize window with delay (after XamlRoot is ready for dialogs)
                this.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                {
                    if (appWindow.Presenter is OverlappedPresenter presenter)
                    {
                        presenter.Maximize();
                    }
                });
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

        /// <summary>
        /// Kontroluje, zda měl den nějakou aktivitu (prodeje nebo vratky)
        /// </summary>
        private async Task<bool> WasDayActiveAsync(DateTime date)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                // Kontrola prodejů
                var hasReceipts = await context.Receipts
                    .AsNoTracking()
                    .AnyAsync(r => r.SaleDate.Date == date.Date);

                if (hasReceipts) return true;

                // Kontrola vratek
                var hasReturns = await context.Returns
                    .AsNoTracking()
                    .AnyAsync(r => r.ReturnDate.Date == date.Date);

                return hasReturns;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MainWindow: Error checking day activity: {ex.Message}");
                return false; // V případě chyby předpokládej, že den nebyl aktivní
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
                        // Detekovat, zda předchozí den byl vůbec zahájený (měl aktivitu)
                        bool wasPreviousDayActive = await WasDayActiveAsync(lastLoginDate.Value);

                        // Loop pro možnost vrácení z potvrzovacího dialogu zpět
                        bool shouldContinue = true;
                        while (shouldContinue)
                        {
                            string errorMessage;
                            string title;

                            if (wasPreviousDayActive)
                            {
                                // Den BYL zahájený (měl aktivitu) → vyžadovat uzavírku
                                title = "⚠️ Nelze zahájit nový den";
                                errorMessage = $"Nebyla provedena uzavírka pro předchozí den!\n\n" +
                                              $"Datum předchozího dne: {lastLoginDate.Value:dd.MM.yyyy}\n" +
                                              $"Aktuální datum: {currentDate:dd.MM.yyyy}\n\n" +
                                              $"POVINNÝ POSTUP:\n" +
                                              $"1. Nejprve proveďte uzavírku pro den {lastLoginDate.Value:dd.MM.yyyy}\n" +
                                              $"2. Poté můžete zahájit nový den {currentDate:dd.MM.yyyy}\n\n" +
                                              $"Aplikace vás nyní přesměruje na stránku Tržby/Uzavírky.";
                            }
                            else
                            {
                                // Den NEBYL zahájený (žádná aktivita) → pravděpodobně otevřeno bez prodejů
                                title = "⚠️ Neuzavřený den bez prodejů";
                                errorMessage = $"Pro den {lastLoginDate.Value:dd.MM.yyyy} nebyla provedena uzavírka.\n\n" +
                                              $"V tento den nebyly zaznamenány žádné prodeje ani vratky.\n\n" +
                                              $"Co chcete udělat?\n\n" +
                                              $"• PROVÉST UZAVÍRKU\n" +
                                              $"  Den byl otevřený, ale bez prodejů (běžná situace)\n\n" +
                                              $"• OPRAVIT NASTAVENÍ\n" +
                                              $"  Den nebyl nikdy zahájen - chyba v datech\n" +
                                              $"  (používejte pouze pokud víte, že je to chyba!)";
                            }

                            ContentDialog blockDialog;

                            if (wasPreviousDayActive)
                            {
                                // Den MĚL aktivitu → pouze uzavírka (POVINNÉ)
                                blockDialog = new ContentDialog
                                {
                                    Title = title,
                                    Content = errorMessage,
                                    PrimaryButtonText = "Rozumím, provést uzavírku",
                                    CloseButtonText = "Zrušit",
                                    DefaultButton = ContentDialogButton.Primary,
                                    XamlRoot = this.Content.XamlRoot
                                };
                            }
                            else
                            {
                                // Den NEMĚL aktivitu → nabídnout obě možnosti
                                blockDialog = new ContentDialog
                                {
                                    Title = title,
                                    Content = errorMessage,
                                    PrimaryButtonText = "Provést uzavírku",
                                    SecondaryButtonText = "Opravit nastavení",
                                    CloseButtonText = "Zrušit",
                                    DefaultButton = ContentDialogButton.Primary,
                                    XamlRoot = this.Content.XamlRoot
                                };
                            }

                            var result = await blockDialog.ShowAsync();

                            if (result == ContentDialogResult.Secondary && !wasPreviousDayActive)
                            {
                                // SECONDARY = Opravit nastavení (resetovat na poslední uzavřený den)
                                // POTVRZOVACÍ DIALOG - ochrana proti náhodnému kliknutí
                                var confirmDialog = new ContentDialog
                                {
                                    Title = "⚠️ Potvrdit opravu nastavení",
                                    Content = $"OPRAVDU chcete opravit nastavení?\n\n" +
                                             $"⚠️ Tato akce SMAŽE záznam o dni {lastLoginDate.Value:dd.MM.yyyy}\n\n" +
                                             $"Použijte tuto možnost POUZE pokud:\n" +
                                             $"• Den {lastLoginDate.Value:dd.MM.yyyy} nebyl nikdy otevřený\n" +
                                             $"• Je to chyba v datech (např. bug v aplikaci)\n\n" +
                                             $"Pokud jste si NEJSTE jistí, klikněte ZRUŠIT\n" +
                                             $"a zvolte \"Provést uzavírku\"!",
                                    PrimaryButtonText = "ANO, SMAZAT DEN",
                                    CloseButtonText = "Zrušit",
                                    DefaultButton = ContentDialogButton.Close,
                                    XamlRoot = this.Content.XamlRoot
                                };

                                var confirmResult = await confirmDialog.ShowAsync();

                                if (confirmResult == ContentDialogResult.Primary)
                                {
                                    // Potvrzeno → resetovat
                                    var lastClosedDate = await _dailyCloseService.GetLastCloseDateAsync();
                                    if (lastClosedDate.HasValue)
                                    {
                                        _settingsService.CurrentSettings.LastSaleLoginDate = lastClosedDate.Value;
                                    }
                                    else
                                    {
                                        _settingsService.CurrentSettings.LastSaleLoginDate = null;
                                    }
                                    await _settingsService.SaveSettingsAsync();
                                    await Task.Delay(300);

                                    // Znovu spustit new day check s opravenými daty
                                    await HandleNewDayCheckAsync();
                                    return;
                                }
                                else
                                {
                                    // Zrušil potvrzovací dialog → ZPĚT na hlavní dialog (continue loop)
                                    await Task.Delay(300); // Dialog close delay
                                    continue; // ← Znovu zobrazit hlavní dialog
                                }
                            }
                            else if (result == ContentDialogResult.Primary)
                            {
                                // PRIMARY = Provést uzavírku → navigovat na Tržby/Uzavírky
                                await Task.Delay(300); // Dialog close delay

                                NavView.SelectedItem = NavView.MenuItems.Cast<NavigationViewItem>()
                                    .FirstOrDefault(item => item.Tag?.ToString() == "TrzbyUzavirky");
                                ContentFrame.Content = new Views.TrzbyUzavirkPage();

                                return; // ZASTAVIT
                            }
                            else
                            {
                                // CLOSE = Zrušit hlavní dialog → exit aplikace
                                Application.Current.Exit();
                                return;
                            }
                        }
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

            // Perform backup and show dialog
            await PerformBackupWithDialogAsync("Aplikace bude nyní zavřena.");

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

        private async Task<bool> PerformBackupWithDialogAsync(string additionalMessage)
        {
            var appDataPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData);
            var sourceFolderPath = System.IO.Path.Combine(appDataPath, "Sklad_2_Data");
            var sourceDbPath = System.IO.Path.Combine(sourceFolderPath, "sklad.db");

            // Variables to track database dates for settings sync
            DateTime? lastDailyCloseDateFromDb = null;
            DateTime? lastActivityFromDb = null;
            bool wasTimeShiftConfirmed = false;

            // CRITICAL: Check database content before backup to prevent overwriting good backups with empty DB
            if (System.IO.File.Exists(sourceDbPath))
            {
                var sourceDbInfo = new System.IO.FileInfo(sourceDbPath);
                long currentDbSize = sourceDbInfo.Length;

                // KRITICKÉ: Kontrola obsahu databáze (ne jen velikosti!)
                // Prázdná SQLite databáze s tabulkami má ~140 KB, takže size check nefunguje
                bool isDatabaseEmpty = false;
                int productCount = 0;
                int receiptCount = 0;
                DateTime? lastReceiptDate = null;
                DateTime? lastReturnDate = null;
                DateTime? lastDailyCloseDate = null;

                try
                {
                    using var context = await _contextFactory.CreateDbContextAsync();
                    productCount = await context.Products.CountAsync();
                    receiptCount = await context.Receipts.CountAsync();
                    isDatabaseEmpty = (productCount == 0 && receiptCount == 0);

                    // KRITICKÉ: Kontrola časového posunu - detekce obnovení staré zálohy
                    lastReceiptDate = await context.Receipts
                        .AsNoTracking()
                        .OrderByDescending(r => r.SaleDate)
                        .Select(r => (DateTime?)r.SaleDate)
                        .FirstOrDefaultAsync();

                    lastReturnDate = await context.Returns
                        .AsNoTracking()
                        .OrderByDescending(r => r.ReturnDate)
                        .Select(r => (DateTime?)r.ReturnDate)
                        .FirstOrDefaultAsync();

                    lastDailyCloseDate = await context.DailyCloses
                        .AsNoTracking()
                        .OrderByDescending(dc => dc.Date)
                        .Select(dc => (DateTime?)dc.Date)
                        .FirstOrDefaultAsync();

                    // Store values for later settings sync
                    lastDailyCloseDateFromDb = lastDailyCloseDate;
                }
                catch
                {
                    // Pokud nelze otevřít DB, považuj ji za corrupted
                    isDatabaseEmpty = true;
                }

                // Check 1: Prázdná databáze (0 produktů a 0 účtenek)
                if (isDatabaseEmpty)
                {
                    // KRITICKÉ VAROVÁNÍ: Prázdná databáze je velmi podezřelá!
                    var emptyDbDialog = new ContentDialog
                    {
                        Title = "⚠️ KRITICKÉ VAROVÁNÍ",
                        Content = $"Databáze je prázdná!\n\n" +
                                 $"• Počet produktů: {productCount}\n" +
                                 $"• Počet účtenek: {receiptCount}\n" +
                                 $"• Velikost souboru: {currentDbSize:N0} bytů\n\n" +
                                 "⚠️ VAROVÁNÍ: Prázdná databáze přepíše všechna uložená data v zálohách!\n\n" +
                                 "Co dělat dál:\n" +
                                 "• DOPORUČENO: Obnovte databázi ze zálohy (Nastavení → Systém)\n" +
                                 "• Nebo pokračujte bez zálohy (zálohy zůstanou nedotčené)\n\n" +
                                 "Zálohovat POUZE pokud VÍTE, že databáze je správně prázdná!\n\n" +
                                 "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n" +
                                 "⚠️ NEJSTE SI JISTÍ? ZAVOLEJTE!\n" +
                                 "📞 Majitel/Admin: +420 739 639 484\n" +
                                 "❌ NEPOKRAČUJTE bez konzultace!\n" +
                                 "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n" +
                                 additionalMessage,
                        PrimaryButtonText = "Ano, zálohovat",
                        SecondaryButtonText = "Ne, nezálohovat",
                        CloseButtonText = "Zrušit",
                        DefaultButton = ContentDialogButton.Secondary,
                        XamlRoot = this.Content.XamlRoot
                    };

                    var result = await emptyDbDialog.ShowAsync();

                    if (result == ContentDialogResult.Primary)
                    {
                        // EXTRA POTVRZENÍ - uživatel opravdu chce zálohovat prázdnou databázi
                        var confirmDialog = new ContentDialog
                        {
                            Title = "⚠️ POSLEDNÍ POTVRZENÍ",
                            Content = "OPRAVDU chcete zálohovat prázdnou databázi?\n\n" +
                                     "⚠️ Tato akce PŘEPÍŠE VŠECHNA DATA v zálohách!\n\n" +
                                     "Pokud si nejste 100% jistí, klikněte ZRUŠIT a zavolejte:\n" +
                                     "📞 +420 739 639 484",
                            PrimaryButtonText = "ANO, POTVRDIT ZÁLOHU",
                            CloseButtonText = "Zrušit",
                            DefaultButton = ContentDialogButton.Close,
                            XamlRoot = this.Content.XamlRoot
                        };

                        var confirmResult = await confirmDialog.ShowAsync();
                        if (confirmResult != ContentDialogResult.Primary)
                        {
                            return false; // ŽÁDNÁ ZÁLOHA
                        }
                    }
                    else
                    {
                        // User chose not to backup
                        return false; // ŽÁDNÁ ZÁLOHA
                    }
                    // User explicitly confirmed TWICE → continue with backup
                }
                else
                {
                    // Check 2 & 4: Porovnání s existující zálohou (velikost + počet záznamů)
                    // KRITICKÉ: I když DB není prázdná, můžeme ztratit část dat!
                    var settingsPath = System.IO.Path.Combine(sourceFolderPath, "settings.json");
                    if (System.IO.File.Exists(settingsPath))
                    {
                        try
                        {
                            var json = System.IO.File.ReadAllText(settingsPath);
                            var settings = System.Text.Json.JsonSerializer.Deserialize<Models.Settings.AppSettings>(json);
                            string primaryBackupPath = settings?.BackupPath;

                            if (!string.IsNullOrWhiteSpace(primaryBackupPath))
                            {
                                var backupDbPath = System.IO.Path.Combine(primaryBackupPath, "Sklad_2_Data", "sklad.db");
                                if (System.IO.File.Exists(backupDbPath))
                                {
                                    var backupDbInfo = new System.IO.FileInfo(backupDbPath);
                                    long backupDbSize = backupDbInfo.Length;

                                    // Check 2: Výrazný pokles velikosti (> 50%)
                                    if (currentDbSize < backupDbSize * 0.5)
                                    {
                                        var sizeWarningDialog = new ContentDialog
                                        {
                                            Title = "⚠️ VAROVÁNÍ: Výrazný pokles velikosti databáze",
                                            Content = $"Databáze je výrazně menší než záloha!\n\n" +
                                                     $"• Aktuální DB: {currentDbSize:N0} bytů ({productCount} produktů, {receiptCount} účtenek)\n" +
                                                     $"• Záloha: {backupDbSize:N0} bytů\n" +
                                                     $"• Rozdíl: {((1 - (double)currentDbSize / backupDbSize) * 100):F0}% menší\n\n" +
                                                     "⚠️ VAROVÁNÍ: Toto může znamenat ztrátu dat!\n\n" +
                                                     "Možné příčiny:\n" +
                                                     "• Poškozená databáze\n" +
                                                     "• Smazání velkého množství dat\n" +
                                                     "• Obnovení staré zálohy\n\n" +
                                                     "Co dělat:\n" +
                                                     "• DOPORUČENO: Zkontrolujte databázi (SQLite Browser)\n" +
                                                     "• Obnovte ze zálohy, pokud jsou data chybná\n" +
                                                     "• Zálohujte POUZE pokud VÍTE, že změna je správná!\n\n" +
                                                     "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n" +
                                                     "⚠️ NEJSTE SI JISTÍ? ZAVOLEJTE!\n" +
                                                     "📞 Majitel/Admin: +420 739 639 484\n" +
                                                     "❌ NEPOKRAČUJTE bez konzultace!\n" +
                                                     "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n" +
                                                     additionalMessage,
                                            PrimaryButtonText = "Ano, zálohovat",
                                            SecondaryButtonText = "Ne, nezálohovat",
                                            CloseButtonText = "Zrušit",
                                            DefaultButton = ContentDialogButton.Secondary,
                                            XamlRoot = this.Content.XamlRoot
                                        };

                                        var result = await sizeWarningDialog.ShowAsync();

                                        if (result == ContentDialogResult.Primary)
                                        {
                                            // EXTRA POTVRZENÍ - databáze je výrazně menší
                                            var confirmDialog = new ContentDialog
                                            {
                                                Title = "⚠️ POSLEDNÍ POTVRZENÍ",
                                                Content = "OPRAVDU chcete zálohovat menší databázi?\n\n" +
                                                         "⚠️ Tato akce PŘEPÍŠE větší zálohu!\n\n" +
                                                         "Pokud si nejste 100% jistí, klikněte ZRUŠIT a zavolejte:\n" +
                                                         "📞 +420 739 639 484",
                                                PrimaryButtonText = "ANO, POTVRDIT ZÁLOHU",
                                                CloseButtonText = "Zrušit",
                                                DefaultButton = ContentDialogButton.Close,
                                                XamlRoot = this.Content.XamlRoot
                                            };

                                            var confirmResult = await confirmDialog.ShowAsync();
                                            if (confirmResult != ContentDialogResult.Primary)
                                            {
                                                return false;
                                            }
                                        }
                                        else
                                        {
                                            // User chose not to backup
                                            return false;
                                        }
                                        // User explicitly confirmed TWICE → continue with backup
                                    }

                                    // Check 4: Porovnání POČTU ZÁZNAMŮ se zálohou
                                    // KRITICKÉ: Malé ztráty (10 účtenek) mají malý dopad na velikost,
                                    // ale můžou znamenat ztrátu důležitých dat!
                                    try
                                    {
                                        // Otevři zálohu jako SQLite databázi (read-only)
                                        var backupConnectionString = $"Data Source={backupDbPath};Mode=ReadOnly";
                                        var backupOptions = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<DatabaseContext>()
                                            .UseSqlite(backupConnectionString)
                                            .Options;

                                        int backupProductCount = 0;
                                        int backupReceiptCount = 0;

                                        using (var backupContext = new DatabaseContext(backupOptions))
                                        {
                                            backupProductCount = await backupContext.Products.AsNoTracking().CountAsync();
                                            backupReceiptCount = await backupContext.Receipts.AsNoTracking().CountAsync();
                                        }

                                        // Porovnej počty - pokud pokles > 5% → VAROVÁNÍ!
                                        bool hasProductLoss = backupProductCount > 0 && productCount < backupProductCount * 0.95;
                                        bool hasReceiptLoss = backupReceiptCount > 0 && receiptCount < backupReceiptCount * 0.95;

                                        if (hasProductLoss || hasReceiptLoss)
                                        {
                                            int productDiff = backupProductCount - productCount;
                                            int receiptDiff = backupReceiptCount - receiptCount;
                                            double productLossPercent = backupProductCount > 0 ? ((double)productDiff / backupProductCount * 100) : 0;
                                            double receiptLossPercent = backupReceiptCount > 0 ? ((double)receiptDiff / backupReceiptCount * 100) : 0;

                                            var dataLossDialog = new ContentDialog
                                            {
                                                Title = "⚠️ VAROVÁNÍ: Pokles počtu záznamů",
                                                Content = $"Databáze obsahuje MÉNĚ záznamů než záloha!\n\n" +
                                                         $"Aktuální databáze vs Záloha:\n\n" +
                                                         $"📦 Produkty:\n" +
                                                         $"   • Aktuální: {productCount}\n" +
                                                         $"   • Záloha: {backupProductCount}\n" +
                                                         (hasProductLoss ? $"   • ⚠️ Rozdíl: -{productDiff} ({productLossPercent:F1}%)\n\n" : "   • ✅ Stejně\n\n") +
                                                         $"🧾 Účtenky:\n" +
                                                         $"   • Aktuální: {receiptCount}\n" +
                                                         $"   • Záloha: {backupReceiptCount}\n" +
                                                         (hasReceiptLoss ? $"   • ⚠️ Rozdíl: -{receiptDiff} ({receiptLossPercent:F1}%)\n\n" : "   • ✅ Stejně\n\n") +
                                                         "⚠️ VAROVÁNÍ: Pokles může znamenat ztrátu dat!\n\n" +
                                                         "Možné příčiny:\n" +
                                                         "• Běžné: Smazání produktů/storno účtenek (normální provoz)\n" +
                                                         "• Problém: Corrupted databáze nebo rollback\n\n" +
                                                         "Co dělat:\n" +
                                                         "• DOPORUČENO: Zkontrolujte databázi (SQLite Browser)\n" +
                                                         "• Pokud je smazání ZÁMĚRNÉ → Zálohujte\n" +
                                                         "• Pokud je smazání CHYBA → Obnovte ze zálohy\n\n" +
                                                         "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n" +
                                                         "⚠️ NEJSTE SI JISTÍ? ZAVOLEJTE!\n" +
                                                         "📞 Majitel/Admin: +420 739 639 484\n" +
                                                         "❌ NEPOKRAČUJTE bez konzultace!\n" +
                                                         "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n" +
                                                         additionalMessage,
                                                PrimaryButtonText = "Ano, zálohovat",
                                                SecondaryButtonText = "Ne, nezálohovat",
                                                CloseButtonText = "Zrušit",
                                                DefaultButton = ContentDialogButton.Secondary,
                                                XamlRoot = this.Content.XamlRoot
                                            };

                                            var result = await dataLossDialog.ShowAsync();

                                            if (result == ContentDialogResult.Primary)
                                            {
                                                // EXTRA POTVRZENÍ - pokles počtu záznamů
                                                var confirmDialog = new ContentDialog
                                                {
                                                    Title = "⚠️ POSLEDNÍ POTVRZENÍ",
                                                    Content = "OPRAVDU chcete zálohovat databázi s MÉNĚ záznamy?\n\n" +
                                                             "⚠️ Tato akce PŘEPÍŠE zálohu s VĚTŠÍM počtem dat!\n\n" +
                                                             "Pokud si nejste 100% jistí, klikněte ZRUŠIT a zavolejte:\n" +
                                                             "📞 +420 739 639 484",
                                                    PrimaryButtonText = "ANO, POTVRDIT ZÁLOHU",
                                                    CloseButtonText = "Zrušit",
                                                    DefaultButton = ContentDialogButton.Close,
                                                    XamlRoot = this.Content.XamlRoot
                                                };

                                                var confirmResult = await confirmDialog.ShowAsync();
                                                if (confirmResult != ContentDialogResult.Primary)
                                                {
                                                    return false;
                                                }
                                            }
                                            else
                                            {
                                                // User chose not to backup
                                                return false;
                                            }
                                            // User explicitly confirmed TWICE → continue with backup
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        // Pokud nelze otevřít zálohu, pokračuj (lepší než blokovat)
                                        Debug.WriteLine($"Backup comparison failed: {ex.Message}");
                                    }
                                }
                            }
                        }
                        catch { /* Ignore errors in size comparison */ }
                    }

                    // Check 3: Časový posun - detekce obnovení staré zálohy
                    // KRITICKÉ: Pokud poslední data v DB jsou výrazně starší než očekávané → stará záloha!
                    var settingsLastDayClose = _settingsService.CurrentSettings.LastDayCloseDate;
                    var settingsLastSaleLogin = _settingsService.CurrentSettings.LastSaleLoginDate;

                    // Najdi nejnovější aktivitu v databázi
                    DateTime? lastActivity = null;
                    if (lastReceiptDate.HasValue || lastReturnDate.HasValue || lastDailyCloseDate.HasValue)
                    {
                        lastActivity = new[] { lastReceiptDate, lastReturnDate, lastDailyCloseDate }
                            .Where(d => d.HasValue)
                            .Select(d => d.Value)
                            .DefaultIfEmpty(DateTime.MinValue)
                            .Max();
                    }

                    // Store for later settings sync
                    lastActivityFromDb = lastActivity;

                    // Pokud settings má novější LastDayCloseDate než DB → JISTĚ stará záloha!
                    bool isOldBackupRestored = false;
                    string timeTravelMessage = "";

                    if (settingsLastDayClose.HasValue && lastDailyCloseDate.HasValue &&
                        settingsLastDayClose.Value.Date > lastDailyCloseDate.Value.Date)
                    {
                        isOldBackupRestored = true;
                        var daysDiff = (settingsLastDayClose.Value.Date - lastDailyCloseDate.Value.Date).Days;
                        timeTravelMessage = $"⏰ ČASOVÝ POSUN DETEKOVÁN!\n\n" +
                                          $"• Poslední uzavírka v NASTAVENÍ: {settingsLastDayClose.Value:dd.MM.yyyy}\n" +
                                          $"• Poslední uzavírka v DATABÁZI: {lastDailyCloseDate.Value:dd.MM.yyyy}\n" +
                                          $"• Rozdíl: {daysDiff} dní zpět\n\n";
                    }

                    if (isOldBackupRestored)
                    {
                        var timeWarpDialog = new ContentDialog
                        {
                            Title = "⚠️ VAROVÁNÍ: Časový posun v datech",
                            Content = timeTravelMessage +
                                     "⚠️ VAROVÁNÍ: Pravděpodobně byla obnovena stará záloha!\n\n" +
                                     "Důvod: Data v databázi jsou výrazně starší než očekávané datum.\n\n" +
                                     "Možné příčiny:\n" +
                                     "• Obnovení staré zálohy (nečekané)\n" +
                                     "• Špatné systémové datum při záloze\n" +
                                     "• Záměrný rollback\n\n" +
                                     "Co dělat:\n" +
                                     "• DOPORUČENO: Zkontrolujte databázi (SQLite Browser)\n" +
                                     "• Obnovte správnou (nejnovější) zálohu, pokud je to chyba\n" +
                                     "• Zálohujte POUZE pokud VÍTE, že starší data jsou správná!\n\n" +
                                     "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n" +
                                     "⚠️ NEJSTE SI JISTÍ? ZAVOLEJTE!\n" +
                                     "📞 Majitel/Admin: +420 739 639 484\n" +
                                     "❌ NEPOKRAČUJTE bez konzultace!\n" +
                                     "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n" +
                                     additionalMessage,
                            PrimaryButtonText = "Ano, zálohovat",
                            SecondaryButtonText = "Ne, nezálohovat",
                            CloseButtonText = "Zrušit",
                            DefaultButton = ContentDialogButton.Secondary,
                            XamlRoot = this.Content.XamlRoot
                        };

                        var result = await timeWarpDialog.ShowAsync();

                        if (result == ContentDialogResult.Primary)
                        {
                            // EXTRA POTVRZENÍ - stará data v databázi
                            var confirmDialog = new ContentDialog
                            {
                                Title = "⚠️ POSLEDNÍ POTVRZENÍ",
                                Content = "OPRAVDU chcete zálohovat STARŠÍ data?\n\n" +
                                         "⚠️ Tato akce může PŘEPSAT NOVĚJŠÍ zálohy!\n\n" +
                                         "Pokud si nejste 100% jistí, klikněte ZRUŠIT a zavolejte:\n" +
                                         "📞 +420 739 639 484",
                                PrimaryButtonText = "ANO, POTVRDIT ZÁLOHU",
                                CloseButtonText = "Zrušit",
                                DefaultButton = ContentDialogButton.Close,
                                XamlRoot = this.Content.XamlRoot
                            };

                            var confirmResult = await confirmDialog.ShowAsync();
                            if (confirmResult != ContentDialogResult.Primary)
                            {
                                return false;
                            }

                            // User explicitly confirmed TWICE → flag for settings sync
                            wasTimeShiftConfirmed = true;
                        }
                        else
                        {
                            // User chose not to backup
                            return false;
                        }
                        // User explicitly confirmed TWICE → continue with backup
                    }
                }
            }

            // IMPORTANT: Update settings BEFORE backup (only if time shift was confirmed)
            // so the backup contains the updated timestamps
            try
            {
                bool settingsChanged = false;

                // KRITICKÉ: Update LastSaleLoginDate POUZE při potvrzení time shift!
                // DŮVOD 1: Pokud se mění vždy, způsobuje deadlock (původní bug)
                // DŮVOD 2: Pokud se neaktualizuje po time shift, detekce se OPAKUJE každé spuštění (regrese)
                // ŘEŠENÍ: Update POUZE pokud uživatel EXPLICITNĚ potvrdil obnovení staré zálohy (2× ANO)
                if (wasTimeShiftConfirmed)
                {
                    _settingsService.CurrentSettings.LastSaleLoginDate = DateTime.Today;
                    settingsChanged = true;
                    Debug.WriteLine("MainWindow: Updated LastSaleLoginDate after time shift confirmation");
                }
                // else - NEZMĚNĚNO při normálním zavření → opravuje původní bug!

                // If user confirmed time shift, sync LastDayCloseDate with database value
                if (wasTimeShiftConfirmed && lastDailyCloseDateFromDb.HasValue)
                {
                    _settingsService.CurrentSettings.LastDayCloseDate = lastDailyCloseDateFromDb.Value;
                    settingsChanged = true;
                    Debug.WriteLine($"MainWindow: Synced LastDayCloseDate with DB value: {lastDailyCloseDateFromDb.Value:dd.MM.yyyy}");
                }

                // Only save if settings actually changed
                if (settingsChanged)
                {
                    await _settingsService.SaveSettingsAsync();
                    await Task.Delay(200); // Win10 file flush
                    Debug.WriteLine("MainWindow: Updated settings after time shift confirmation BEFORE backup");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MainWindow: Failed to update settings before backup: {ex.Message}");
            }

            // Perform backup in background (will backup updated settings)
            await System.Threading.Tasks.Task.Run(() => PerformDatabaseSync());

            // Read backup status from file
            var statusPath = System.IO.Path.Combine(appDataPath, "Sklad_2_Data", "backup_status.txt");
            string backupStatus = "Záloha byla provedena.";

            if (System.IO.File.Exists(statusPath))
            {
                try
                {
                    backupStatus = System.IO.File.ReadAllText(statusPath);
                }
                catch
                {
                    backupStatus = "Záloha byla provedena.\n\nNepodařilo se načíst detailní stav.";
                }
            }

            // Show completion dialog with backup status
            var completionDialog = new ContentDialog
            {
                Title = "Záloha dokončena",
                Content = backupStatus + "\n\n" + additionalMessage,
                CloseButtonText = "OK",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.Content.XamlRoot
            };

            await completionDialog.ShowAsync();
            return true; // Backup was performed
        }

        private void PerformDatabaseSync()
        {
            var appDataPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData);
            var sourceFolderPath = System.IO.Path.Combine(appDataPath, "Sklad_2_Data");
            var logPath = System.IO.Path.Combine(sourceFolderPath, "backup_log.txt");
            var statusPath = System.IO.Path.Combine(sourceFolderPath, "backup_status.txt");

            void Log(string msg)
            {
                try { System.IO.File.AppendAllText(logPath, $"{DateTime.Now:HH:mm:ss} - {msg}\n"); } catch { }
                System.Diagnostics.Debug.WriteLine(msg);
            }

            bool primarySuccess = false;
            bool secondarySuccess = false;
            bool primaryConfigured = false;
            bool secondaryConfigured = false;

            try
            {
                var sourceDbPath = System.IO.Path.Combine(sourceFolderPath, "sklad.db");

                Log("PerformDatabaseSync: Starting dual backup...");
                Log($"PerformDatabaseSync: Source folder: {sourceFolderPath}");
                Log($"PerformDatabaseSync: Source DB exists: {System.IO.File.Exists(sourceDbPath)}");

                if (!System.IO.File.Exists(sourceDbPath))
                {
                    Log($"PerformDatabaseSync: Source database not found - skipping backup");
                    // Write status file
                    System.IO.File.WriteAllText(statusPath, "CHYBA: Zdrojová databáze nebyla nalezena.");
                    return;
                }

                // Read backup paths from settings file directly (avoid service calls during disposal)
                var settingsPath = System.IO.Path.Combine(sourceFolderPath, "settings.json");
                string primaryBackupPath = null;
                string secondaryBackupPath = null;

                Log($"PerformDatabaseSync: Settings file path: {settingsPath}");
                Log($"PerformDatabaseSync: Settings file exists: {System.IO.File.Exists(settingsPath)}");

                if (System.IO.File.Exists(settingsPath))
                {
                    try
                    {
                        var json = System.IO.File.ReadAllText(settingsPath);
                        var settings = System.Text.Json.JsonSerializer.Deserialize<Models.Settings.AppSettings>(json);
                        primaryBackupPath = settings?.BackupPath;
                        secondaryBackupPath = settings?.SecondaryBackupPath;
                        Log($"PerformDatabaseSync: Primary BackupPath: '{primaryBackupPath}'");
                        Log($"PerformDatabaseSync: Secondary BackupPath: '{secondaryBackupPath}'");
                    }
                    catch (Exception ex)
                    {
                        Log($"PerformDatabaseSync: Error parsing settings: {ex.Message}");
                    }
                }

                // Backup to primary path
                if (!string.IsNullOrWhiteSpace(primaryBackupPath))
                {
                    primaryConfigured = true;
                    primarySuccess = BackupToPath(sourceFolderPath, primaryBackupPath, "Primary", Log);
                }
                else
                {
                    Log("PerformDatabaseSync: Primary backup path not configured - skipping");
                }

                // Backup to secondary path (if configured)
                if (!string.IsNullOrWhiteSpace(secondaryBackupPath))
                {
                    secondaryConfigured = true;
                    secondarySuccess = BackupToPath(sourceFolderPath, secondaryBackupPath, "Secondary", Log);
                }
                else
                {
                    Log("PerformDatabaseSync: Secondary backup path not configured - skipping");
                }

                // Write status summary
                var statusBuilder = new System.Text.StringBuilder();
                statusBuilder.AppendLine($"Záloha dokončena: {DateTime.Now:dd.MM.yyyy HH:mm:ss}");
                statusBuilder.AppendLine();

                if (primaryConfigured)
                {
                    statusBuilder.AppendLine($"{(primarySuccess ? "✅" : "❌")} Primární záloha: {(primarySuccess ? "OK" : "CHYBA")}");
                }
                else
                {
                    statusBuilder.AppendLine("❌ Primární záloha: Není nakonfigurována");
                }

                if (secondaryConfigured)
                {
                    statusBuilder.AppendLine($"{(secondarySuccess ? "✅" : "❌")} Sekundární záloha: {(secondarySuccess ? "OK" : "CHYBA")}");
                }
                else
                {
                    statusBuilder.AppendLine("❌ Sekundární záloha: Není nakonfigurována");
                }

                statusBuilder.AppendLine();

                // Overall status
                bool hasAnyConfigured = primaryConfigured || secondaryConfigured;
                bool allConfiguredSuccess = (!primaryConfigured || primarySuccess) && (!secondaryConfigured || secondarySuccess);

                if (!hasAnyConfigured)
                {
                    // No backup paths configured at all
                    statusBuilder.AppendLine("Stav: ⚠️ Žádné zálohy nebyly nakonfigurovány.");
                    statusBuilder.AppendLine("Databáze zůstává pouze v lokálním úložišti.");
                    statusBuilder.AppendLine();
                    statusBuilder.AppendLine("DOPORUČENÍ: Nastavte cestu pro zálohy v Nastavení → Systém.");
                }
                else if (allConfiguredSuccess)
                {
                    // All configured backups succeeded
                    if (primaryConfigured && secondaryConfigured)
                    {
                        statusBuilder.AppendLine("Stav: ✅ Obě zálohy proběhly úspěšně");
                    }
                    else
                    {
                        statusBuilder.AppendLine("Stav: ✅ Záloha proběhla úspěšně");
                    }
                }
                else
                {
                    // Some configured backups failed
                    statusBuilder.AppendLine("Stav: ⚠️ Některé zálohy selhaly - zkontrolujte backup_log.txt");
                }

                System.IO.File.WriteAllText(statusPath, statusBuilder.ToString());
                Log("PerformDatabaseSync: Dual backup completed!");
            }
            catch (Exception ex)
            {
                Log($"PerformDatabaseSync: ERROR - {ex.Message}");
                Log($"PerformDatabaseSync: Stack trace - {ex.StackTrace}");

                // Write error status
                try
                {
                    System.IO.File.WriteAllText(statusPath, $"KRITICKÁ CHYBA: {ex.Message}\n\nZkontrolujte backup_log.txt pro detaily.");
                }
                catch { }
            }
        }

        private bool BackupToPath(string sourceFolderPath, string backupBasePath, string label, Action<string> log)
        {
            try
            {
                var backupFolderPath = System.IO.Path.Combine(backupBasePath, "Sklad_2_Data");
                log($"{label} backup: Target folder: {backupFolderPath}");

                System.IO.Directory.CreateDirectory(backupFolderPath);

                bool hasErrors = false;

                // Copy database (Win10 compatible - with flush + verification)
                var sourceDbPath = System.IO.Path.Combine(sourceFolderPath, "sklad.db");
                var backupDbPath = System.IO.Path.Combine(backupFolderPath, "sklad.db");

                var sourceDbInfo = new System.IO.FileInfo(sourceDbPath);
                log($"{label} backup: Database source size: {sourceDbInfo.Length:N0} bytes");

                System.IO.File.Copy(sourceDbPath, backupDbPath, true);
                // Win10: Force OS buffer flush
                using (var fs = new System.IO.FileStream(backupDbPath, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read))
                {
                    fs.Flush(true);
                }

                // Verify database backup
                if (System.IO.File.Exists(backupDbPath))
                {
                    var backupDbInfo = new System.IO.FileInfo(backupDbPath);
                    if (backupDbInfo.Length == sourceDbInfo.Length)
                    {
                        log($"{label} backup: Database copied and verified OK ({backupDbInfo.Length:N0} bytes)");
                    }
                    else
                    {
                        log($"{label} backup: Database copied but SIZE MISMATCH (source: {sourceDbInfo.Length:N0}, backup: {backupDbInfo.Length:N0})");
                        hasErrors = true;
                    }
                }
                else
                {
                    log($"{label} backup: Database VERIFICATION FAILED - file does not exist after copy!");
                    hasErrors = true;
                }

                // Copy settings.json (Win10 compatible - with flush + verification)
                var sourceSettingsPath = System.IO.Path.Combine(sourceFolderPath, "settings.json");
                var backupSettingsPath = System.IO.Path.Combine(backupFolderPath, "settings.json");
                if (System.IO.File.Exists(sourceSettingsPath))
                {
                    var sourceSettingsInfo = new System.IO.FileInfo(sourceSettingsPath);
                    log($"{label} backup: Settings source size: {sourceSettingsInfo.Length:N0} bytes");

                    System.IO.File.Copy(sourceSettingsPath, backupSettingsPath, true);
                    // Win10: Force OS buffer flush
                    using (var fs = new System.IO.FileStream(backupSettingsPath, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read))
                    {
                        fs.Flush(true);
                    }

                    // Verify settings backup
                    if (System.IO.File.Exists(backupSettingsPath))
                    {
                        var backupSettingsInfo = new System.IO.FileInfo(backupSettingsPath);
                        if (backupSettingsInfo.Length == sourceSettingsInfo.Length)
                        {
                            log($"{label} backup: Settings copied and verified OK ({backupSettingsInfo.Length:N0} bytes)");
                        }
                        else
                        {
                            log($"{label} backup: Settings copied but SIZE MISMATCH (source: {sourceSettingsInfo.Length:N0}, backup: {backupSettingsInfo.Length:N0})");
                            hasErrors = true;
                        }
                    }
                    else
                    {
                        log($"{label} backup: Settings VERIFICATION FAILED - file does not exist after copy!");
                        hasErrors = true;
                    }
                }

                // Copy ProductImages folder (Win10 compatible - with flush + verification)
                var sourceImagesPath = System.IO.Path.Combine(sourceFolderPath, "ProductImages");
                var backupImagesPath = System.IO.Path.Combine(backupFolderPath, "ProductImages");
                if (System.IO.Directory.Exists(sourceImagesPath))
                {
                    System.IO.Directory.CreateDirectory(backupImagesPath);
                    var imageFiles = System.IO.Directory.GetFiles(sourceImagesPath);
                    int verifiedCount = 0;
                    int failedCount = 0;
                    long totalBytes = 0;

                    foreach (var file in imageFiles)
                    {
                        var fileName = System.IO.Path.GetFileName(file);
                        var destPath = System.IO.Path.Combine(backupImagesPath, fileName);

                        var sourceFileInfo = new System.IO.FileInfo(file);
                        System.IO.File.Copy(file, destPath, true);
                        // Win10: Force OS buffer flush for each image
                        using (var fs = new System.IO.FileStream(destPath, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read))
                        {
                            fs.Flush(true);
                        }

                        // Verify each image
                        if (System.IO.File.Exists(destPath))
                        {
                            var destFileInfo = new System.IO.FileInfo(destPath);
                            if (destFileInfo.Length == sourceFileInfo.Length)
                            {
                                verifiedCount++;
                                totalBytes += destFileInfo.Length;
                            }
                            else
                            {
                                failedCount++;
                                log($"{label} backup: Image '{fileName}' SIZE MISMATCH (source: {sourceFileInfo.Length:N0}, backup: {destFileInfo.Length:N0})");
                            }
                        }
                        else
                        {
                            failedCount++;
                            log($"{label} backup: Image '{fileName}' VERIFICATION FAILED - file does not exist after copy!");
                        }
                    }

                    if (failedCount == 0)
                    {
                        log($"{label} backup: ProductImages copied and verified OK ({verifiedCount} files, {totalBytes:N0} bytes total)");
                    }
                    else
                    {
                        log($"{label} backup: ProductImages copied with ERRORS (OK: {verifiedCount}, FAILED: {failedCount})");
                        hasErrors = true;
                    }
                }

                if (!hasErrors)
                {
                    log($"{label} backup: Completed successfully!");
                    return true;
                }
                else
                {
                    log($"{label} backup: Completed with ERRORS!");
                    return false;
                }
            }
            catch (Exception ex)
            {
                log($"{label} backup: ERROR - {ex.Message}");
                return false;
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
                // Perform backup before logout
                await PerformBackupWithDialogAsync("Budete odhlášeni.");

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
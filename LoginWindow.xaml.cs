using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Sklad_2.Services;
using Sklad_2.ViewModels;
using Sklad_2.Views.Dialogs;
using Microsoft.UI.Dispatching;

namespace Sklad_2
{
    public sealed partial class LoginWindow : Window
    {
        public LoginViewModel ViewModel { get; }
        private readonly IAuthService _authService;
        private readonly ISettingsService _settingsService;
        private readonly IUpdateService _updateService;
        private DispatcherTimer _timer;
        private UpdateInfo _pendingUpdate;

        public LoginWindow()
        {
            var serviceProvider = (Application.Current as App).Services;
            ViewModel = serviceProvider.GetRequiredService<LoginViewModel>();
            _authService = serviceProvider.GetRequiredService<IAuthService>();
            _settingsService = serviceProvider.GetRequiredService<ISettingsService>();
            _updateService = serviceProvider.GetRequiredService<IUpdateService>();

            this.InitializeComponent();
            LoginGrid.DataContext = this;
            ExtendsContentIntoTitleBar = true;

            // Set version text
            VersionText.Text = $"Verze: {_updateService.CurrentVersion}";

            // Initialize timer
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Tick;

            // Subscribe to ViewModel events
            ViewModel.RequestPasswordAsync += HandleRequestPasswordAsync;
            ViewModel.RequestFirstAdminAsync += HandleRequestFirstAdminAsync;
            ViewModel.LoginSucceeded += HandleLoginSucceeded;
            ViewModel.LoginFailed += HandleLoginFailed;

            // Subscribe to Unloaded event to stop timer
            this.Closed += LoginWindow_Closed;
        }

        private void LoginWindow_Loaded(object sender, RoutedEventArgs e)
        {
            this.DispatcherQueue.TryEnqueue(async () =>
            {
                await _settingsService.LoadSettingsAsync();
                await ViewModel.LoadUsersAsync();
                _timer.Start();
                UpdateDateTime(); // Initial update

                // Check for updates in background
                _ = CheckForUpdatesAsync();
            });
        }

        private void LoginWindow_Closed(object sender, WindowEventArgs args)
        {
            _timer.Stop();
        }

        private void Timer_Tick(object sender, object e)
        {
            UpdateDateTime();
        }

        private void UpdateDateTime()
        {
            DateTextBlock.Text = DateTime.Now.ToString("dd.MM.yyyy");
            TimeTextBlock.Text = DateTime.Now.ToString("HH:mm:ss");
            DayOfWeekTextBlock.Text = DateTime.Now.ToString("dddd");
        }

        private async Task<string> HandleRequestPasswordAsync(string prompt)
        {
            var dialog = new PasswordPromptDialog();
            dialog.SetPromptText(prompt);
            dialog.XamlRoot = this.Content.XamlRoot;

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                return dialog.Password;
            }
            return null; // User cancelled
        }

        private async Task<(bool confirmed, string username, string displayName, string password)> HandleRequestFirstAdminAsync(
            string title, string defaultUsername, string defaultDisplayName)
        {
            // Wait for XamlRoot to be available
            int retries = 0;
            while (this.Content?.XamlRoot == null && retries < 20)
            {
                await Task.Delay(50);
                retries++;
            }

            var dialog = new CreateFirstAdminDialog();
            dialog.SetDefaults(defaultUsername, defaultDisplayName);
            dialog.XamlRoot = this.Content.XamlRoot;

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                return (true, dialog.Username, dialog.DisplayName, dialog.Password);
            }

            return (false, null, null, null);
        }

        private async void HandleLoginSucceeded()
        {
            try
            {
                Debug.WriteLine($"LoginSucceeded: User = {_authService.CurrentUser?.DisplayName}");

                // Create and show MainWindow (it will handle new day logic itself)
                var mainWindow = new MainWindow();

                // CRITICAL: Set CurrentWindow for FolderPicker and other dialogs to work on Win10
                var app = Application.Current as App;
                app.CurrentWindow = mainWindow;

                // Win11 compatibility - ensure MainWindow is fully activated before closing LoginWindow
                mainWindow.Activate();
                await Task.Delay(500); // Give Win11 time to fully activate the new window

                this.Close();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoginWindow: Error during login window switch: {ex.Message}");
                // Ensure window closes even if there's an error
                try
                {
                    this.Close();
                }
                catch { }
            }
        }

        private void HandleLoginFailed()
        {
            // Error is already shown in ViewModel's StatusMessage
            Debug.WriteLine("Login failed");
        }

        private async void UserButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Models.User user)
            {
                await ViewModel.SelectUserCommand.ExecuteAsync(user);
            }
        }

        // ========== UPDATE MANAGEMENT ==========

        private async Task CheckForUpdatesAsync()
        {
            try
            {
                // Show checking status
                ShowUpdateStatus("Kontroluji aktualizace...", showProgress: false);

                // Small delay for UI to render
                await Task.Delay(500);

                var updateInfo = await _updateService.CheckForUpdatesAsync();

                if (updateInfo == null)
                {
                    // Failed to check - hide status
                    HideUpdateStatus();
                    return;
                }

                if (updateInfo.IsNewerVersion && !string.IsNullOrEmpty(updateInfo.DownloadUrl))
                {
                    // New version available
                    _pendingUpdate = updateInfo;
                    ShowUpdateAvailable(updateInfo);
                }
                else
                {
                    // No update available - hide after delay
                    ShowUpdateStatus("✓ Aplikace je aktuální", showProgress: false);
                    await Task.Delay(2000);
                    HideUpdateStatus();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Update check failed: {ex.Message}");
                HideUpdateStatus();
            }
        }

        private void ShowUpdateStatus(string message, bool showProgress)
        {
            this.DispatcherQueue.TryEnqueue(() =>
            {
                UpdateStatusPanel.Visibility = Visibility.Visible;
                UpdateStatusText.Text = message;
                UpdateProgressBar.Visibility = showProgress ? Visibility.Visible : Visibility.Collapsed;
                UpdateNowButton.Visibility = Visibility.Collapsed;
                ContinueWithoutUpdateButton.Visibility = Visibility.Collapsed;
            });
        }

        private void ShowUpdateAvailable(UpdateInfo updateInfo)
        {
            this.DispatcherQueue.TryEnqueue(() =>
            {
                UpdateStatusPanel.Visibility = Visibility.Visible;
                UpdateStatusText.Text = $"📦 Dostupná nová verze: {updateInfo.Version}";
                UpdateProgressBar.Visibility = Visibility.Collapsed;
                UpdateProgressBar.IsIndeterminate = false;
                UpdateProgressBar.Value = 0;
                UpdateNowButton.Visibility = Visibility.Visible;
                ContinueWithoutUpdateButton.Visibility = Visibility.Visible;
            });
        }

        private void HideUpdateStatus()
        {
            this.DispatcherQueue.TryEnqueue(() =>
            {
                UpdateStatusPanel.Visibility = Visibility.Collapsed;
            });
        }

        private async void UpdateNowButton_Click(object sender, RoutedEventArgs e)
        {
            if (_pendingUpdate == null)
                return;

            try
            {
                // Show downloading status
                UpdateStatusText.Text = "Stahuji aktualizaci...";
                UpdateProgressBar.Visibility = Visibility.Visible;
                UpdateProgressBar.IsIndeterminate = true;
                UpdateNowButton.Visibility = Visibility.Collapsed;
                ContinueWithoutUpdateButton.Visibility = Visibility.Collapsed;

                var progress = new Progress<int>(percent =>
                {
                    this.DispatcherQueue.TryEnqueue(() =>
                    {
                        UpdateProgressBar.IsIndeterminate = false;
                        UpdateProgressBar.Value = percent;
                        UpdateStatusText.Text = $"Stahuji aktualizaci... {percent}%";
                    });
                });

                bool success = await _updateService.DownloadAndInstallUpdateAsync(_pendingUpdate, progress);

                if (success)
                {
                    // Update successful - app will restart
                    UpdateStatusText.Text = "✓ Aktualizace připravena. Aplikace se nyní restartuje...";
                    UpdateProgressBar.Visibility = Visibility.Collapsed;

                    // Wait a moment for user to see the message
                    await Task.Delay(2000);

                    // Close app - batch script will restart it
                    Environment.Exit(0);
                }
                else
                {
                    // Download failed
                    UpdateStatusText.Text = "❌ Chyba při stahování aktualizace";
                    UpdateProgressBar.Visibility = Visibility.Collapsed;
                    ContinueWithoutUpdateButton.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Update download failed: {ex.Message}");
                UpdateStatusText.Text = "❌ Chyba při stahování aktualizace";
                UpdateProgressBar.Visibility = Visibility.Collapsed;
                ContinueWithoutUpdateButton.Visibility = Visibility.Visible;
            }
        }

        private void ContinueWithoutUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            // User chose to continue without updating
            HideUpdateStatus();
            _pendingUpdate = null;
        }
    }
}

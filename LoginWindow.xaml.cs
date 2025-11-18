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
        private DispatcherTimer _timer;

        public LoginWindow()
        {
            var serviceProvider = (Application.Current as App).Services;
            ViewModel = serviceProvider.GetRequiredService<LoginViewModel>();
            _authService = serviceProvider.GetRequiredService<IAuthService>();
            _settingsService = serviceProvider.GetRequiredService<ISettingsService>();

            this.InitializeComponent();
            LoginGrid.DataContext = this;
            ExtendsContentIntoTitleBar = true;

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

        private void HandleLoginSucceeded()
        {
            Debug.WriteLine($"LoginSucceeded: User = {_authService.CurrentUser?.DisplayName}");

            // Create and show MainWindow (it will handle new day logic itself)
            var mainWindow = new MainWindow();
            mainWindow.Activate();
            this.Close();
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
    }
}

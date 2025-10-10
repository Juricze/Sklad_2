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
            this.InitializeComponent();
            var serviceProvider = (Application.Current as App).Services;
            ViewModel = serviceProvider.GetRequiredService<LoginViewModel>();
            _authService = serviceProvider.GetRequiredService<IAuthService>();
            _settingsService = serviceProvider.GetRequiredService<ISettingsService>();
            ExtendsContentIntoTitleBar = true;

            // Initialize timer
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Tick;

            // Subscribe to ViewModel events
            ViewModel.RequestPasswordAsync += HandleRequestPasswordAsync;
            ViewModel.CreatePasswordAsync += HandleCreatePasswordAsync;
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

        private async Task<string> HandleCreatePasswordAsync(string prompt)
        {
            var dialog = new PasswordPromptDialog();
            dialog.Title = "Vytvořit heslo";
            dialog.SetPromptText(prompt);
            dialog.XamlRoot = this.Content.XamlRoot;

            while (true)
            {
                var result = await dialog.ShowAsync();

                if (result == ContentDialogResult.Primary)
                {
                    if (string.IsNullOrWhiteSpace(dialog.Password))
                    {
                        dialog.SetErrorText("Heslo nemůže být prázdné.");
                        continue; // Show the dialog again
                    }
                    return dialog.Password;
                }
                return null; // User cancelled
            }
        }

        private void HandleLoginSucceeded(string role)
        {
            _authService.SetCurrentRole(role);

            Debug.WriteLine($"LoginSucceeded: Role = {role}");

            // Create and show MainWindow (it will handle new day logic itself)
            var mainWindow = new MainWindow();
            mainWindow.Activate();
            this.Close();
        }

        private async void HandleLoginFailed(string errorMessage)
        {
            var errorDialog = new ContentDialog
            {
                Title = "Přihlášení selhalo",
                Content = errorMessage,
                CloseButtonText = "OK",
                XamlRoot = this.Content.XamlRoot
            };
            await errorDialog.ShowAsync();
        }
    }
}

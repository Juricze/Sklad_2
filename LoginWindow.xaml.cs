using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Sklad_2.Services;
using Sklad_2.ViewModels;
using Sklad_2.Views.Dialogs;
using System;
using System.Threading.Tasks;

namespace Sklad_2
{
    public sealed partial class LoginWindow : Window
    {
        public LoginViewModel ViewModel { get; }
        private readonly IAuthService _authService;

        public LoginWindow()
        {
            this.InitializeComponent();
            var serviceProvider = (Application.Current as App).Services;
            ViewModel = serviceProvider.GetRequiredService<LoginViewModel>();
            _authService = serviceProvider.GetRequiredService<IAuthService>();
            ExtendsContentIntoTitleBar = true;

            // Subscribe to ViewModel events
            ViewModel.RequestPasswordAsync += HandleRequestPasswordAsync;
            ViewModel.CreatePasswordAsync += HandleCreatePasswordAsync;
            ViewModel.LoginSucceeded += HandleLoginSucceeded;
            ViewModel.LoginFailed += HandleLoginFailed;
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

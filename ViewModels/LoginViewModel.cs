using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sklad_2.Services;
using System;
using System.Threading.Tasks;

namespace Sklad_2.ViewModels
{
    public partial class LoginViewModel : ObservableObject
    {
        private readonly ISettingsService _settingsService;

        [ObservableProperty]
        private string statusMessage;

        // Events for View interaction
        public Func<string, Task<string>> RequestPasswordAsync { get; set; }
        public Func<string, Task<string>> CreatePasswordAsync { get; set; }
        public Action<string> LoginFailed { get; set; }
        public Action<string> LoginSucceeded { get; set; }


        public LoginViewModel(ISettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        [RelayCommand]
        private async Task SelectAdminAsync()
        {
            var adminPassword = _settingsService.CurrentSettings.AdminPassword;

            if (string.IsNullOrEmpty(adminPassword))
            {
                // First time login for Admin: Create password
                var newPassword = await CreatePasswordAsync?.Invoke("Vytvořte heslo pro Admina:");
                if (!string.IsNullOrEmpty(newPassword))
                {
                    _settingsService.CurrentSettings.AdminPassword = newPassword;
                    await _settingsService.SaveSettingsAsync();
                    LoginSucceeded?.Invoke("Admin");
                }
            }
            else
            {
                // Normal Admin login
                var enteredPassword = await RequestPasswordAsync?.Invoke("Zadejte heslo pro Admina:");
                if (enteredPassword == adminPassword)
                {
                    LoginSucceeded?.Invoke("Admin");
                }
                else if (enteredPassword != null) // User entered a password, but it was wrong
                {
                    LoginFailed?.Invoke("Nesprávné heslo.");
                }
            }
        }

        [RelayCommand]
        private async Task SelectProdejAsync()
        {
            var salePassword = _settingsService.CurrentSettings.SalePassword;

            if (string.IsNullOrEmpty(salePassword))
            {
                LoginFailed?.Invoke("Profil 'Prodej' je uzamčen. Admin musí nejprve nastavit heslo v nastavení.");
            }
            else
            {
                var enteredPassword = await RequestPasswordAsync?.Invoke("Zadejte heslo pro Prodej:");
                if (enteredPassword == salePassword)
                {
                    LoginSucceeded?.Invoke("Prodej");
                }
                else if (enteredPassword != null)
                {
                    LoginFailed?.Invoke("Nesprávné heslo.");
                }
            }
            await Task.CompletedTask; // To satisfy async method signature, can be removed if not needed
        }
    }
}

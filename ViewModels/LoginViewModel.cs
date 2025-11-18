using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sklad_2.Models;
using Sklad_2.Services;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace Sklad_2.ViewModels
{
    public partial class LoginViewModel : ObservableObject
    {
        private readonly IDataService _dataService;
        private readonly IAuthService _authService;

        [ObservableProperty]
        private ObservableCollection<User> users = new();

        [ObservableProperty]
        private string statusMessage;

        [ObservableProperty]
        private bool isLoading;

        // Events for View interaction
        public Func<string, Task<string>> RequestPasswordAsync { get; set; }
        public Func<string, string, string, Task<(bool confirmed, string username, string displayName, string password)>> RequestFirstAdminAsync { get; set; }
        public Action LoginFailed { get; set; }
        public Action LoginSucceeded { get; set; }

        public LoginViewModel(IDataService dataService, IAuthService authService)
        {
            _dataService = dataService;
            _authService = authService;
        }

        public async Task LoadUsersAsync()
        {
            IsLoading = true;
            try
            {
                var allUsers = await _dataService.GetAllUsersAsync();
                Users.Clear();

                // Only show active users
                foreach (var user in allUsers)
                {
                    if (user.IsActive)
                    {
                        Users.Add(user);
                    }
                }

                // If no users exist, prompt to create first admin
                if (Users.Count == 0)
                {
                    StatusMessage = "Žádní uživatelé nenalezeni. Vytvořte prvního administrátora.";
                    await CreateFirstAdminAsync();
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Chyba načítání uživatelů: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task CreateFirstAdminAsync()
        {
            var result = await RequestFirstAdminAsync?.Invoke(
                "Vytvořte prvního administrátora",
                "admin",
                "Administrátor");

            if (result.confirmed)
            {
                var admin = new User
                {
                    Username = result.username,
                    DisplayName = result.displayName,
                    Password = result.password,
                    Role = "Admin",
                    IsActive = true,
                    CreatedDate = DateTime.Now
                };

                await _dataService.CreateUserAsync(admin);
                Users.Add(admin);
                StatusMessage = "Administrátor vytvořen úspěšně.";
            }
            else
            {
                // User cancelled - close app
                System.Environment.Exit(0);
            }
        }

        [RelayCommand]
        private async Task SelectUserAsync(User user)
        {
            if (user == null) return;

            var enteredPassword = await RequestPasswordAsync?.Invoke($"Zadejte heslo pro {user.DisplayName}:");

            if (enteredPassword == null) return; // User cancelled

            var success = await _authService.LoginAsync(user.Username, enteredPassword);

            if (success)
            {
                LoginSucceeded?.Invoke();
            }
            else
            {
                StatusMessage = "Nesprávné heslo.";
                LoginFailed?.Invoke();
            }
        }
    }
}

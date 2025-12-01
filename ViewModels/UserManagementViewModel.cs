using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sklad_2.Models;
using Sklad_2.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Sklad_2.ViewModels
{
    public partial class UserManagementViewModel : ObservableObject
    {
        private readonly IDataService _dataService;
        private readonly IAuthService _authService;

        [ObservableProperty]
        private ObservableCollection<User> users = new();

        [ObservableProperty]
        private User selectedUser;

        [ObservableProperty]
        private string statusMessage;

        public bool IsUserSelected => SelectedUser != null;

        // Events for dialogs
        public Func<Task<(bool confirmed, string username, string displayName, string password, string role)>> RequestAddUserAsync { get; set; }
        public Func<User, Task<(bool confirmed, string username, string displayName, string password, string role)>> RequestEditUserAsync { get; set; }

        public UserManagementViewModel(IDataService dataService, IAuthService authService)
        {
            _dataService = dataService;
            _authService = authService;
        }

        public async Task LoadUsersAsync()
        {
            try
            {
                var allUsers = await _dataService.GetAllUsersAsync();
                Users.Clear();
                foreach (var user in allUsers.OrderBy(u => u.Username))
                {
                    Users.Add(user);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Chyba načítání uživatelů: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task AddUserAsync()
        {
            var result = await RequestAddUserAsync?.Invoke();

            if (result.confirmed)
            {
                try
                {
                    // Check if username already exists
                    var existingUser = await _dataService.GetUserByUsernameAsync(result.username);
                    if (existingUser != null)
                    {
                        StatusMessage = $"Uživatel s přihlašovacím jménem '{result.username}' již existuje.";
                        return;
                    }

                    var newUser = new User
                    {
                        Username = result.username,
                        DisplayName = result.displayName,
                        Password = result.password,
                        Role = result.role,
                        IsActive = true,
                        CreatedDate = DateTime.Now
                    };

                    await _dataService.CreateUserAsync(newUser);
                    await Task.Delay(100); // Win10 file system flush
                    await LoadUsersAsync();
                    await Task.Delay(200); // Win10 UI refresh
                    StatusMessage = $"Uživatel '{newUser.DisplayName}' byl úspěšně přidán.";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Chyba při vytváření uživatele: {ex.Message}";
                }
            }
        }

        [RelayCommand]
        private async Task EditUserAsync()
        {
            if (SelectedUser == null)
            {
                StatusMessage = "Vyberte uživatele ze seznamu.";
                return;
            }

            var result = await RequestEditUserAsync?.Invoke(SelectedUser);

            if (result.confirmed)
            {
                try
                {
                    // Check if new username conflicts with another user
                    if (result.username != SelectedUser.Username)
                    {
                        var existingUser = await _dataService.GetUserByUsernameAsync(result.username);
                        if (existingUser != null && existingUser.UserId != SelectedUser.UserId)
                        {
                            StatusMessage = $"Uživatel s přihlašovacím jménem '{result.username}' již existuje.";
                            return;
                        }
                    }

                    SelectedUser.Username = result.username;
                    SelectedUser.DisplayName = result.displayName;
                    if (!string.IsNullOrWhiteSpace(result.password))
                    {
                        SelectedUser.Password = result.password;
                    }
                    SelectedUser.Role = result.role;

                    await _dataService.UpdateUserAsync(SelectedUser);
                    await Task.Delay(100); // Win10 file system flush
                    await LoadUsersAsync();
                    await Task.Delay(200); // Win10 UI refresh
                    StatusMessage = $"Uživatel '{SelectedUser.DisplayName}' byl úspěšně aktualizován.";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Chyba při aktualizaci uživatele: {ex.Message}";
                }
            }
        }

        [RelayCommand]
        private async Task ToggleUserActiveAsync()
        {
            if (SelectedUser == null)
            {
                StatusMessage = "Vyberte uživatele ze seznamu.";
                return;
            }

            try
            {
                var newStatus = !SelectedUser.IsActive;
                await _dataService.SetUserActiveAsync(SelectedUser.UserId, newStatus);
                SelectedUser.IsActive = newStatus;

                await Task.Delay(100); // Win10 file system flush

                var statusText = newStatus ? "aktivován" : "deaktivován";
                StatusMessage = $"Uživatel '{SelectedUser.DisplayName}' byl {statusText}.";

                await LoadUsersAsync();
                await Task.Delay(200); // Win10 UI refresh
            }
            catch (Exception ex)
            {
                StatusMessage = $"Chyba při změně stavu uživatele: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task DeleteUserAsync()
        {
            if (SelectedUser == null)
            {
                StatusMessage = "Vyberte uživatele ze seznamu.";
                return;
            }

            // Prevent deleting yourself
            if (SelectedUser.UserId == _authService.CurrentUser?.UserId)
            {
                StatusMessage = "Nelze smazat vlastní účet. Kontaktujte jiného administrátora.";
                return;
            }

            try
            {
                var userToDelete = SelectedUser.DisplayName;
                await _dataService.DeleteUserAsync(SelectedUser.UserId);
                await Task.Delay(100); // Win10 file system flush
                await LoadUsersAsync();
                await Task.Delay(200); // Win10 UI refresh
                StatusMessage = $"Uživatel '{userToDelete}' byl úspěšně smazán.";
                SelectedUser = null; // Clear selection
            }
            catch (Exception ex)
            {
                StatusMessage = $"Chyba při mazání uživatele: {ex.Message}";
            }
        }

        partial void OnSelectedUserChanged(User value)
        {
            OnPropertyChanged(nameof(IsUserSelected));
        }
    }
}

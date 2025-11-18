using CommunityToolkit.Mvvm.Messaging;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Sklad_2.Messages;
using Sklad_2.Models;

namespace Sklad_2.Services
{
    public class AuthService : IAuthService
    {
        private readonly IMessenger _messenger;
        private readonly IDataService _dataService;

        public User CurrentUser { get; private set; }
        public string CurrentRole => CurrentUser?.Role; // Backward compatibility

        public AuthService(IMessenger messenger, IDataService dataService)
        {
            _messenger = messenger;
            _dataService = dataService;
        }

        public async Task<bool> LoginAsync(string username, string password)
        {
            try
            {
                var users = await _dataService.GetAllUsersAsync();
                var user = users.FirstOrDefault(u => u.Username == username && u.IsActive);

                if (user != null && user.Password == password)
                {
                    CurrentUser = user;
                    Debug.WriteLine($"AuthService: User {user.DisplayName} ({user.Role}) logged in");
                    _messenger.Send(new RoleChangedMessage(user.Role));
                    return true;
                }

                Debug.WriteLine($"AuthService: Login failed for username {username}");
                return false;
            }
            catch (System.Exception ex)
            {
                Debug.WriteLine($"AuthService: Login error: {ex.Message}");
                return false;
            }
        }

        public void Logout()
        {
            CurrentUser = null;
            _messenger.Send(new RoleChangedMessage(null));
        }

        // For backward compatibility with old code
        public void SetCurrentRole(string role)
        {
            Debug.WriteLine($"AuthService: SetCurrentRole called with {role} (deprecated, use LoginAsync)");
            // This is deprecated but kept for backward compatibility during migration
        }
    }
}

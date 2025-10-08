using CommunityToolkit.Mvvm.Messaging;
using System.Diagnostics;
using Sklad_2.Messages;

namespace Sklad_2.Services
{
    public class AuthService : IAuthService
    {
        private readonly IMessenger _messenger;

        public string CurrentRole { get; private set; }

        public AuthService(IMessenger messenger)
        {
            _messenger = messenger;
        }

        public void SetCurrentRole(string role)
        {
            CurrentRole = role;
            Debug.WriteLine($"AuthService: Setting CurrentRole to {role}");
            _messenger.Send(new RoleChangedMessage(role));
        }

        public void Logout()
        {
            CurrentRole = null;
            _messenger.Send(new RoleChangedMessage(null)); // Send null or a specific logout message
        }
    }
}

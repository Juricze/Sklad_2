namespace Sklad_2.Services
{
    public class AuthService : IAuthService
    {
        public string CurrentRole { get; private set; }

        public void SetCurrentRole(string role)
        {
            CurrentRole = role;
        }

        public void Logout()
        {
            CurrentRole = null;
        }
    }
}

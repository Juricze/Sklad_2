namespace Sklad_2.Services
{
    public interface IAuthService
    {
        string CurrentRole { get; }
        void SetCurrentRole(string role);
    }
}

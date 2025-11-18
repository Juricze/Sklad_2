using Sklad_2.Models;
using System.Threading.Tasks;

namespace Sklad_2.Services
{
    public interface IAuthService
    {
        User CurrentUser { get; }
        string CurrentRole { get; } // Kept for backward compatibility during migration
        Task<bool> LoginAsync(string username, string password);
        void Logout();

        // For backward compatibility with old code
        void SetCurrentRole(string role);
    }
}

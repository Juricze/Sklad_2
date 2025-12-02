
using Sklad_2.Models.Settings;
using System.Threading.Tasks;

namespace Sklad_2.Services
{
    public interface ISettingsService
    {
        AppSettings CurrentSettings { get; set; }
        Task LoadSettingsAsync();
        Task SaveSettingsAsync();
        bool IsBackupPathConfigured();
        string GetBackupFolderPath();
        bool IsSecondaryBackupPathConfigured();
        string GetSecondaryBackupFolderPath();
    }
}

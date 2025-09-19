
using Sklad_2.Models.Settings;
using System.Threading.Tasks;

namespace Sklad_2.Services
{
    public interface ISettingsService
    {
        AppSettings CurrentSettings { get; }
        Task LoadSettingsAsync();
        Task SaveSettingsAsync();
    }
}

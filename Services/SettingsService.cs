
using Sklad_2.Models.Settings;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Sklad_2.Services
{
    public class SettingsService : ISettingsService
    {
        private const string SettingsFileName = "settings.json";
        private readonly string _settingsPath;
        public AppSettings CurrentSettings { get; private set; }

        public SettingsService()
        {
            var baseDirectory = AppContext.BaseDirectory;
            var dbFolderPath = Path.Combine(baseDirectory, "db");
            Directory.CreateDirectory(dbFolderPath);
            _settingsPath = Path.Combine(dbFolderPath, SettingsFileName);
            CurrentSettings = new AppSettings();
        }

        public async Task LoadSettingsAsync()
        {
            if (File.Exists(_settingsPath))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(_settingsPath);
                    CurrentSettings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
                catch (Exception)
                {
                    // In case of deserialization error, start with default settings
                    CurrentSettings = new AppSettings();
                }
            }
            else
            {
                CurrentSettings = new AppSettings();
            }
        }

        public async Task SaveSettingsAsync()
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(CurrentSettings, options);
            await File.WriteAllTextAsync(_settingsPath, json);
        }
    }
}

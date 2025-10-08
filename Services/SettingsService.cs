
using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Storage;
using Sklad_2.Models.Settings;

namespace Sklad_2.Services
{
    public class SettingsService : ISettingsService
    {
        private const string SettingsFileName = "settings.json";
        private string _settingsFilePath;
        public AppSettings CurrentSettings { get; private set; }

        private async Task<string> GetSettingsFilePath()
        {
            if (string.IsNullOrEmpty(_settingsFilePath))
            {
                var localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var appDataPath = Path.Combine(localAppDataPath, "Sklad_2_Data");
                Directory.CreateDirectory(appDataPath);
                _settingsFilePath = Path.Combine(appDataPath, SettingsFileName);
            }
            return _settingsFilePath;
        }

        public SettingsService()
        {
            CurrentSettings = new AppSettings();
        }

        public async Task LoadSettingsAsync()
        {
            var settingsFilePath = await GetSettingsFilePath();

            if (File.Exists(settingsFilePath))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(settingsFilePath);
                    CurrentSettings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                    Debug.WriteLine($"Settings loaded. LastSaleLoginDate: {CurrentSettings.LastSaleLoginDate}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error loading settings: {ex.Message}");
                    CurrentSettings = new AppSettings(); // Ensure CurrentSettings is initialized even on error
                }
            }
            else
            {
                CurrentSettings = new AppSettings();
                Debug.WriteLine("Settings file not found. Using default settings.");
            }
        }

        public async Task SaveSettingsAsync()
        {
            var settingsFilePath = await GetSettingsFilePath();
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(CurrentSettings, options);
            await File.WriteAllTextAsync(settingsFilePath, json);
            Debug.WriteLine($"Settings saved. LastSaleLoginDate: {CurrentSettings.LastSaleLoginDate}");
        }
    }
}

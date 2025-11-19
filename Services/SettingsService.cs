
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
        private readonly JsonSerializerOptions _jsonSerializerOptions = new() { WriteIndented = true };

        public AppSettings CurrentSettings { get; set; }

        private string GetSettingsFilePath()
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
            var settingsFilePath = GetSettingsFilePath();

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
            var settingsFilePath = GetSettingsFilePath();
            var json = JsonSerializer.Serialize(CurrentSettings, _jsonSerializerOptions);
            await File.WriteAllTextAsync(settingsFilePath, json);
            Debug.WriteLine($"Settings saved. LastSaleLoginDate: {CurrentSettings.LastSaleLoginDate}");
        }

        public string GetBackupFolderPath()
        {
            // Priority 1: Custom BackupPath from settings
            if (!string.IsNullOrWhiteSpace(CurrentSettings.BackupPath) && Directory.Exists(CurrentSettings.BackupPath))
            {
                return Path.Combine(CurrentSettings.BackupPath, "Sklad_2_Data");
            }

            // Priority 2: OneDrive
            string oneDrivePath = Environment.GetEnvironmentVariable("OneDrive");
            if (!string.IsNullOrEmpty(oneDrivePath) && Directory.Exists(oneDrivePath))
            {
                return Path.Combine(oneDrivePath, "Sklad_2_Data");
            }

            // Priority 3: Documents (fallback)
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Sklad_2_Backups");
        }
    }
}


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

            // Force flush to disk (important for Win10 compatibility)
            using (var fs = new FileStream(settingsFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                fs.Flush(true); // Force OS to flush buffers
            }

            Debug.WriteLine($"Settings saved and flushed. LastSaleLoginDate: {CurrentSettings.LastSaleLoginDate}");
        }

        public bool IsBackupPathConfigured()
        {
            // Check if path is configured (not empty), don't require it to exist yet
            // The directory will be created during backup if needed
            return !string.IsNullOrWhiteSpace(CurrentSettings.BackupPath);
        }

        public string GetBackupFolderPath()
        {
            // Only return path if explicitly configured by user
            if (IsBackupPathConfigured())
            {
                return Path.Combine(CurrentSettings.BackupPath, "Sklad_2_Data");
            }

            // No fallback - force user to configure backup path
            throw new InvalidOperationException("Backup path is not configured. Please set backup path in Settings.");
        }
    }
}

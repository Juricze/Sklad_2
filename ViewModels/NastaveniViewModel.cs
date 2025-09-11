using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace Sklad_2.ViewModels
{
    public partial class NastaveniViewModel : ObservableObject
    {
        [ObservableProperty]
        private string appVersion;

        [ObservableProperty]
        private string gitHubLink = "https://github.com/your-repo-link"; // Placeholder

        [ObservableProperty]
        private string backupStatusMessage;

        public NastaveniViewModel()
        {
            // Get app version
            AppVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
        }

        [RelayCommand]
        private async Task BackupDatabaseAsync()
        {
            BackupStatusMessage = string.Empty;
            try
            {
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var sourceFolderPath = Path.Combine(appDataPath, "Sklad_2_Data");
                var sourceDbPath = Path.Combine(sourceFolderPath, "sklad.db");

                // Prompt user for backup location (simplified for now)
                // For a real app, use FileSavePicker
                var backupFolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Sklad_2_Backups");
                Directory.CreateDirectory(backupFolderPath);
                var backupFilePath = Path.Combine(backupFolderPath, $"sklad_backup_{DateTime.Now:yyyyMMdd_HHmmss}.db");

                if (File.Exists(sourceDbPath))
                {
                    File.Copy(sourceDbPath, backupFilePath, true);
                    BackupStatusMessage = $"Databáze úspěšně zálohována do: {backupFilePath}";
                }
                else
                {
                    BackupStatusMessage = "Chyba: Soubor databáze nebyl nalezen.";
                }
            }
            catch (Exception ex)
            {
                BackupStatusMessage = $"Chyba při zálohování databáze: {ex.Message}";
            }
            await Task.CompletedTask;
        }
    }
}
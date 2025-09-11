using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sklad_2.Services; 
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace Sklad_2.ViewModels
{
    public partial class NastaveniViewModel : ObservableObject
    {
        private readonly IPrintService _printService; 

        [ObservableProperty]
        private string appVersion;

        [ObservableProperty]
        private string gitHubLink = "https://github.com/your-repo-link"; // Placeholder

        [ObservableProperty]
        private string backupStatusMessage;

        [ObservableProperty]
        private string printerPath; // Added

        [ObservableProperty]
        private string testPrintStatusMessage; // Added

        public NastaveniViewModel(IPrintService printService) 
        {
            _printService = printService; 
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

        [RelayCommand]
        private async Task TestPrintAsync()
        {
            TestPrintStatusMessage = string.Empty;
            if (string.IsNullOrWhiteSpace(PrinterPath))
            {
                TestPrintStatusMessage = "Zadejte prosím cestu k tiskárně.";
                return;
            }

            try
            {
                bool success = await _printService.TestPrintAsync(PrinterPath);
                if (success)
                {
                    TestPrintStatusMessage = "Testovací tisk úspěšný!";
                }
                else
                {
                    TestPrintStatusMessage = "Testovací tisk selhal. Zkontrolujte cestu k tiskárně a její stav.";
                }
            }
            catch (Exception ex)
            {
                TestPrintStatusMessage = $"Chyba při testovacím tisku: {ex.Message}";
            }
        }
    }
}

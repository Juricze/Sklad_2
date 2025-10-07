using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Sklad_2.Models;
using Sklad_2.Messages;
using Sklad_2.Models.Settings;
using Sklad_2.Services;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Sklad_2.ViewModels
{
    public partial class NastaveniViewModel : ObservableObject
    {
        private readonly ISettingsService _settingsService;
        private readonly IPrintService _printService;
        private readonly IDataService _dataService;
        private readonly IMessenger _messenger;
        private readonly IAuthService _authService;

        public bool IsAdmin => _authService.CurrentRole == "Admin";

        [ObservableProperty]
        private AppSettings settings;

        [ObservableProperty]
        private string appVersion;

        [ObservableProperty]
        private string gitHubLink = "https://github.com/your-repo-link"; // Placeholder

        [ObservableProperty]
        private string backupStatusMessage;

        [ObservableProperty]
        private string testPrintStatusMessage;
        
        [ObservableProperty]
        private string saveStatusMessage;

        // Password Management Properties
        [ObservableProperty]
        private string newAdminPassword;

        [ObservableProperty]
        private string confirmAdminPassword;

        [ObservableProperty]
        private string newSalePassword;

        [ObservableProperty]
        private string adminPasswordStatus;

        [ObservableProperty]
        private string salePasswordStatus;

        public ObservableCollection<VatConfig> VatConfigs { get; } = new();

        public NastaveniViewModel(ISettingsService settingsService, IPrintService printService, IDataService dataService, IMessenger messenger, IAuthService authService)
        {
            _settingsService = settingsService;
            _printService = printService;
            _dataService = dataService;
            _messenger = messenger;
            _authService = authService;
            Settings = _settingsService.CurrentSettings;
            AppVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();

            // Pre-fill the sale password box with the current password
            NewSalePassword = Settings.SalePassword;

            LoadVatConfigsCommand.Execute(null);
        }

        [RelayCommand]
        private async Task LoadVatConfigsAsync()
        {
            VatConfigs.Clear();
            var categories = ProductCategories.All; // Use static list
            var savedConfigs = await _dataService.GetVatConfigsAsync();

            foreach (var category in categories.OrderBy(c => c))
            {
                var savedConfig = savedConfigs.FirstOrDefault(c => c.CategoryName == category);
                if (savedConfig != null)
                {
                    VatConfigs.Add(savedConfig);
                }
                else
                {
                    VatConfigs.Add(new VatConfig { CategoryName = category, Rate = 0 });
                }
            }
        }


        [RelayCommand]
        private async Task SaveSettingsAsync()
        {
            SaveStatusMessage = string.Empty;
            try
            {
                await _settingsService.SaveSettingsAsync();
                await _dataService.SaveVatConfigsAsync(VatConfigs);
                _messenger.Send(new VatConfigsChangedMessage());
                SaveStatusMessage = "Nastavení bylo úspěšně uloženo.";
            }
            catch (Exception ex)
            {
                SaveStatusMessage = $"Chyba při ukládání nastavení: {ex.Message}";
            }
            finally
            {
                await Task.Delay(3000);
                SaveStatusMessage = string.Empty;
            }
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
        private async Task ChangeAdminPasswordAsync()
        {
            AdminPasswordStatus = string.Empty;
            if (string.IsNullOrWhiteSpace(NewAdminPassword) || string.IsNullOrWhiteSpace(ConfirmAdminPassword))
            {
                AdminPasswordStatus = "Heslo ani jeho potvrzení nesmí být prázdné.";
                return;
            }

            if (NewAdminPassword != ConfirmAdminPassword)
            {
                AdminPasswordStatus = "Zadaná hesla se neshodují.";
                return;
            }

            try
            {
                Settings.AdminPassword = NewAdminPassword;
                await _settingsService.SaveSettingsAsync();
                AdminPasswordStatus = "Heslo pro Admina bylo úspěšně změněno.";
                NewAdminPassword = string.Empty;
                ConfirmAdminPassword = string.Empty;
            }
            catch (Exception ex)
            {
                AdminPasswordStatus = $"Chyba při ukládání hesla: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task ChangeSalePasswordAsync()
        {
            SalePasswordStatus = string.Empty;
            try
            {
                Settings.SalePassword = NewSalePassword;
                await _settingsService.SaveSettingsAsync();
                SalePasswordStatus = "Heslo pro roli Prodej bylo úspěšně nastaveno.";
            }
            catch (Exception ex)
            {
                SalePasswordStatus = $"Chyba při ukládání hesla: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task TestPrintAsync()
        {
            TestPrintStatusMessage = string.Empty;
            if (string.IsNullOrWhiteSpace(Settings.PrinterPath))
            {
                TestPrintStatusMessage = "Zadejte prosím cestu k tiskárně.";
                return;
            }

            try
            {
                bool success = await _printService.TestPrintAsync(Settings.PrinterPath);
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

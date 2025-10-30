using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Sklad_2.Models;
using Sklad_2.Messages;
using Sklad_2.Models.Settings;
using Sklad_2.Services;
using System;
using System.Collections.Generic;
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
        private string appVersion = "0.5.0";

        [ObservableProperty]
        private string appName = "Sklad 2 - Skladový a prodejní systém";

        [ObservableProperty]
        private string author = "Jiří Hejda";

        [ObservableProperty]
        private string contactEmail = "ji.hejda@gmail.com";

        [ObservableProperty]
        private string copyright = "Copyright © 2025 Jiří Hejda";

        [ObservableProperty]
        private string description = "Moderní aplikace pro správu skladu, prodeje a pokladny postavená na WinUI 3";

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

        [ObservableProperty]
        private string vatStatusMessage;

        [ObservableProperty]
        private bool isVatStatusError;

        [ObservableProperty]
        private string companyStatusMessage;

        [ObservableProperty]
        private bool isCompanyStatusError;

        // Export to PDF properties
        [ObservableProperty]
        private DateTimeOffset? exportStartDate = DateTime.Now.AddMonths(-1);

        [ObservableProperty]
        private DateTimeOffset? exportEndDate = DateTime.Now;

        [ObservableProperty]
        private string exportStatusMessage;

        public ObservableCollection<VatConfig> VatConfigs { get; } = new();

        public NastaveniViewModel(ISettingsService settingsService, IPrintService printService, IDataService dataService, IMessenger messenger, IAuthService authService)
        {
            _settingsService = settingsService;
            _printService = printService;
            _dataService = dataService;
            _messenger = messenger;
            _authService = authService;
            Settings = _settingsService.CurrentSettings;

            LoadVatConfigsCommand.Execute(null);

            // Listen for category changes
            _messenger.Register<VatConfigsChangedMessage>(this, async (r, m) =>
            {
                await LoadVatConfigsAsync();
            });
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
        private async Task SaveVatConfigsAsync()
        {
            VatStatusMessage = string.Empty;
            try
            {
                await _dataService.SaveVatConfigsAsync(VatConfigs);
                _messenger.Send(new VatConfigsChangedMessage());
                ShowVatSuccess("Sazby DPH byly úspěšně uloženy.");
            }
            catch (Exception ex)
            {
                ShowVatError($"Chyba při ukládání sazeb DPH: {ex.Message}");
            }
        }

        private void ShowVatError(string message)
        {
            VatStatusMessage = message;
            IsVatStatusError = true;
        }

        private void ShowVatSuccess(string message)
        {
            VatStatusMessage = message;
            IsVatStatusError = false;
        }

        [RelayCommand]
        private void ClearVatStatus()
        {
            VatStatusMessage = string.Empty;
            IsVatStatusError = false;
        }

        [RelayCommand]
        private async Task SaveCompanyInfoAsync()
        {
            CompanyStatusMessage = string.Empty;
            try
            {
                await _settingsService.SaveSettingsAsync();
                _messenger.Send(new SettingsChangedMessage());
                ShowCompanySuccess("Firemní údaje byly úspěšně uloženy.");
            }
            catch (Exception ex)
            {
                ShowCompanyError($"Chyba při ukládání firemních údajů: {ex.Message}");
            }
        }

        private void ShowCompanyError(string message)
        {
            CompanyStatusMessage = message;
            IsCompanyStatusError = true;
        }

        private void ShowCompanySuccess(string message)
        {
            CompanyStatusMessage = message;
            IsCompanyStatusError = false;
        }

        [RelayCommand]
        private void ClearCompanyStatus()
        {
            CompanyStatusMessage = string.Empty;
            IsCompanyStatusError = false;
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
                // Note: PasswordBox will be cleared by the page code-behind
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

        [RelayCommand]
        private async Task ExportReceiptsToHtmlAsync()
        {
            ExportStatusMessage = string.Empty;

            if (!ExportStartDate.HasValue || !ExportEndDate.HasValue)
            {
                ExportStatusMessage = "Musíte vybrat rozsah dat pro export.";
                return;
            }

            try
            {
                var startDate = ExportStartDate.Value.DateTime;
                var endDate = ExportEndDate.Value.DateTime.AddDays(1).AddSeconds(-1); // Include the whole end day

                var receipts = await _dataService.GetReceiptsAsync(startDate, endDate);

                if (receipts == null || receipts.Count == 0)
                {
                    ExportStatusMessage = "Nenalezeny žádné účtenky v zadaném období.";
                    return;
                }

                // Generate HTML
                var html = GenerateReceiptsHtml(receipts, startDate, endDate);

                // Save to Documents folder
                var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                var exportFolderPath = Path.Combine(documentsPath, "Sklad_2_Exports");
                Directory.CreateDirectory(exportFolderPath);

                var fileName = $"Uctenky_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.html";
                var filePath = Path.Combine(exportFolderPath, fileName);

                File.WriteAllText(filePath, html);

                // Open the file in default browser
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = filePath,
                    UseShellExecute = true
                });

                ExportStatusMessage = $"Export úspěšný!\n\nSoubor: {filePath}\n\nSoubor byl otevřen v prohlížeči. Můžete jej vytisknout nebo uložit jako PDF (Ctrl+P → Uložit jako PDF).";
            }
            catch (Exception ex)
            {
                ExportStatusMessage = $"Chyba při exportu: {ex.Message}";
            }
        }

        private string GenerateReceiptsHtml(List<Receipt> receipts, DateTime startDate, DateTime endDate)
        {
            var settings = _settingsService.CurrentSettings;
            var sb = new System.Text.StringBuilder();

            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang='cs'>");
            sb.AppendLine("<head>");
            sb.AppendLine("<meta charset='UTF-8'>");
            sb.AppendLine("<title>Přehled účtenek</title>");
            sb.AppendLine("<style>");
            sb.AppendLine("body { font-family: Arial, sans-serif; margin: 20px; }");
            sb.AppendLine("h1 { color: #333; }");
            sb.AppendLine("table { width: 100%; border-collapse: collapse; margin-top: 20px; }");
            sb.AppendLine("th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }");
            sb.AppendLine("th { background-color: #f2f2f2; font-weight: bold; }");
            sb.AppendLine("tr:nth-child(even) { background-color: #f9f9f9; }");
            sb.AppendLine(".storno { color: red; font-weight: bold; }");
            sb.AppendLine(".summary { margin-top: 30px; padding: 15px; background-color: #f0f0f0; border-radius: 5px; }");
            sb.AppendLine(".company-info { margin-bottom: 20px; padding: 10px; background-color: #e8f4f8; border-left: 4px solid #0078d4; }");
            sb.AppendLine("@media print { .no-print { display: none; } }");
            sb.AppendLine("</style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");

            // Company info
            sb.AppendLine("<div class='company-info'>");
            sb.AppendLine($"<h2>{settings.ShopName}</h2>");
            sb.AppendLine($"<p>{settings.ShopAddress}</p>");
            sb.AppendLine($"<p>IČ: {settings.CompanyId} | DIČ: {settings.VatId}</p>");
            sb.AppendLine($"<p>Plátce DPH: {(settings.IsVatPayer ? "Ano" : "Ne")}</p>");
            sb.AppendLine("</div>");

            sb.AppendLine($"<h1>Přehled účtenek - Export pro Finanční úřad</h1>");
            sb.AppendLine($"<p><strong>Období:</strong> {startDate:dd.MM.yyyy} - {endDate:dd.MM.yyyy}</p>");
            sb.AppendLine($"<p><strong>Datum exportu:</strong> {DateTime.Now:dd.MM.yyyy HH:mm:ss}</p>");

            // Receipts table
            sb.AppendLine("<table>");
            sb.AppendLine("<thead>");
            sb.AppendLine("<tr>");
            sb.AppendLine("<th>Číslo účtenky</th>");
            sb.AppendLine("<th>Datum a čas</th>");
            sb.AppendLine("<th>Prodavač</th>");
            sb.AppendLine("<th>Způsob platby</th>");
            sb.AppendLine("<th>Celkem (Kč)</th>");

            // Only show VAT columns if company is VAT payer
            if (settings.IsVatPayer)
            {
                sb.AppendLine("<th>Základ (Kč)</th>");
                sb.AppendLine("<th>DPH (Kč)</th>");
            }

            sb.AppendLine("</tr>");
            sb.AppendLine("</thead>");
            sb.AppendLine("<tbody>");

            decimal totalAmount = 0;
            decimal totalWithoutVat = 0;
            decimal totalVat = 0;

            foreach (var receipt in receipts.OrderBy(r => r.SaleDate))
            {
                var rowClass = receipt.IsStorno ? " class='storno'" : "";
                var stornoIndicator = receipt.IsStorno ? "❌ STORNO " : "";

                sb.AppendLine($"<tr{rowClass}>");
                sb.AppendLine($"<td>{stornoIndicator}{receipt.FormattedReceiptNumber}</td>");
                sb.AppendLine($"<td>{receipt.SaleDate:dd.MM.yyyy HH:mm:ss}</td>");
                sb.AppendLine($"<td>{receipt.SellerName}</td>");
                sb.AppendLine($"<td>{receipt.PaymentMethod}</td>");
                sb.AppendLine($"<td style='text-align: right;'>{receipt.TotalAmount:N2}</td>");

                // Only show VAT cells if company is VAT payer
                if (settings.IsVatPayer)
                {
                    sb.AppendLine($"<td style='text-align: right;'>{receipt.TotalAmountWithoutVat:N2}</td>");
                    sb.AppendLine($"<td style='text-align: right;'>{receipt.TotalVatAmount:N2}</td>");
                }

                sb.AppendLine("</tr>");

                totalAmount += receipt.TotalAmount;
                totalWithoutVat += receipt.TotalAmountWithoutVat;
                totalVat += receipt.TotalVatAmount;
            }

            sb.AppendLine("</tbody>");
            sb.AppendLine("</table>");

            // Summary
            sb.AppendLine("<div class='summary'>");
            sb.AppendLine("<h2>Souhrn za období</h2>");
            sb.AppendLine($"<p><strong>Celkový počet účtenek:</strong> {receipts.Count}</p>");
            sb.AppendLine($"<p><strong>Celková částka:</strong> {totalAmount:N2} Kč</p>");

            // Only show VAT summary if company is VAT payer
            if (settings.IsVatPayer)
            {
                sb.AppendLine($"<p><strong>Základ (bez DPH):</strong> {totalWithoutVat:N2} Kč</p>");
                sb.AppendLine($"<p><strong>Celkem DPH:</strong> {totalVat:N2} Kč</p>");
            }

            sb.AppendLine("</div>");

            sb.AppendLine("<p class='no-print' style='margin-top: 30px; color: #666;'>");
            sb.AppendLine("<em>Tento dokument byl vygenerován systémem Sklad 2. Pro vytisknutí nebo uložení jako PDF použijte funkci tisku prohlížeče (Ctrl+P).</em>");
            sb.AppendLine("</p>");

            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            return sb.ToString();
        }
    }
}

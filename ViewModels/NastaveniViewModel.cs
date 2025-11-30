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

        public bool IsVatPayer => _settingsService.CurrentSettings.IsVatPayer;

        [ObservableProperty]
        private AppSettings settings;

        [ObservableProperty]
        private string appVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.5";

        [ObservableProperty]
        private string appName = "Sklad 2 - Skladový a prodejní systém";

        [ObservableProperty]
        private string author = "Jiří Hejda - Aplikárna®";

        [ObservableProperty]
        private string contactEmail = "info@aplikarna.cz";

        [ObservableProperty]
        private string website = "https://www.aplikarna.cz";

        [ObservableProperty]
        private string copyright = "Copyright © 2025 Jiří Hejda";

        [ObservableProperty]
        private string description = "Moderní POS systém pro Windows s kompletní správou skladu, prodeje, DPH a pokladny. Podpora dárkových poukazů, vratek, automatických záloh a multi-user přístupu.";

        [ObservableProperty]
        private string backupStatusMessage;

        [ObservableProperty]
        private string backupStatusColor = "#000000";

        [ObservableProperty]
        private string activeBackupPath;

        public bool IsBackupPathConfigured => _settingsService.IsBackupPathConfigured();

        [ObservableProperty]
        private string testPrintStatusMessage;
        
        [ObservableProperty]
        private string saveStatusMessage;

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

        [ObservableProperty]
        private string manualDiscountsStatusMessage;

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
            UpdateActiveBackupPath();

            // Listen for category changes
            _messenger.Register<VatConfigsChangedMessage>(this, async (r, m) =>
            {
                await LoadVatConfigsAsync();
            });

            // Listen for settings changes to update IsVatPayer property
            _messenger.Register<SettingsChangedMessage>(this, (r, m) =>
            {
                OnPropertyChanged(nameof(IsVatPayer));
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

                // Small delay for Win10 file system flush (100ms)
                await Task.Delay(100);

                // Send message to refresh StatusBar and other components
                _messenger.Send(new SettingsChangedMessage());

                // Additional delay for UI to update (200ms)
                await Task.Delay(200);

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
        private async Task SaveBackupPathAsync()
        {
            try
            {
                await _settingsService.SaveSettingsAsync();
                UpdateActiveBackupPath();
                
                // Notify status bar changes
                _messenger.Send(new SettingsChangedMessage());
                
                BackupStatusMessage = "Cesta pro zálohy byla úspěšně uložena.";
                
                // Small delay to allow UI to update, then refresh animation state
                await Task.Delay(100);
                OnPropertyChanged(nameof(IsBackupPathConfigured));
                
                await Task.Delay(3000);
                BackupStatusMessage = string.Empty;
            }
            catch (Exception ex)
            {
                BackupStatusMessage = $"Chyba při ukládání cesty: {ex.Message}";
            }
        }

        private void UpdateActiveBackupPath()
        {
            if (_settingsService.IsBackupPathConfigured())
            {
                var path = _settingsService.GetBackupFolderPath();
                ActiveBackupPath = $"Aktivní cesta: {path}";
            }
            else
            {
                ActiveBackupPath = "Aktivní cesta: NENÍ NASTAVENA - nastavte cestu pro zálohy";
            }
            OnPropertyChanged(nameof(IsBackupPathConfigured));
        }

        [RelayCommand]
        private async Task SaveManualDiscountsAsync()
        {
            ManualDiscountsStatusMessage = string.Empty;
            try
            {
                await _settingsService.SaveSettingsAsync();
                _messenger.Send(new SettingsChangedMessage());
                ManualDiscountsStatusMessage = "Nastavení ručních slev bylo úspěšně uloženo.";
                await Task.Delay(3000);
                ManualDiscountsStatusMessage = string.Empty;
            }
            catch (Exception ex)
            {
                ManualDiscountsStatusMessage = $"Chyba při ukládání nastavení: {ex.Message}";
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

                // Check if backup path is configured
                if (!_settingsService.IsBackupPathConfigured())
                {
                    BackupStatusMessage = "Chyba: Cesta pro zálohy není nastavena. Přejděte do Nastavení → Systém a nastavte cestu.";
                    BackupStatusColor = "#FF3B30";
                    return;
                }

                string backupFolderPath = _settingsService.GetBackupFolderPath();

                Directory.CreateDirectory(backupFolderPath);
                var backupFilePath = Path.Combine(backupFolderPath, "sklad.db");

                if (File.Exists(sourceDbPath))
                {
                    // Copy database file
                    File.Copy(sourceDbPath, backupFilePath, true);

                    // Also copy settings if they exist
                    var sourceSettingsPath = Path.Combine(sourceFolderPath, "AppSettings.json");
                    var backupSettingsPath = Path.Combine(backupFolderPath, "AppSettings.json");
                    if (File.Exists(sourceSettingsPath))
                    {
                        File.Copy(sourceSettingsPath, backupSettingsPath, true);
                    }

                    // Copy ProductImages folder
                    var sourceImagesPath = Path.Combine(sourceFolderPath, "ProductImages");
                    var backupImagesPath = Path.Combine(backupFolderPath, "ProductImages");
                    if (Directory.Exists(sourceImagesPath))
                    {
                        Directory.CreateDirectory(backupImagesPath);
                        foreach (var file in Directory.GetFiles(sourceImagesPath))
                        {
                            var fileName = Path.GetFileName(file);
                            File.Copy(file, Path.Combine(backupImagesPath, fileName), true);
                        }
                    }

                    BackupStatusMessage = $"Databáze úspěšně synchronizována do: {backupFilePath}";
                    BackupStatusColor = "#34C759"; // Green for success
                }
                else
                {
                    BackupStatusMessage = "Chyba: Soubor databáze nebyl nalezen.";
                    BackupStatusColor = "#FF3B30"; // Red for error
                }
            }
            catch (Exception ex)
            {
                BackupStatusMessage = $"Chyba při synchronizaci databáze: {ex.Message}";
                BackupStatusColor = "#FF3B30"; // Red for error
            }
            await Task.CompletedTask;
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
        private async Task SavePrinterSettingsAsync()
        {
            try
            {
                await _settingsService.SaveSettingsAsync();
                _messenger.Send(new SettingsChangedMessage());
                TestPrintStatusMessage = "Nastavení tiskárny uloženo.";
            }
            catch (Exception ex)
            {
                TestPrintStatusMessage = $"Chyba při ukládání: {ex.Message}";
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
                var returns = await _dataService.GetReturnsAsync(startDate, endDate);

                if ((receipts == null || receipts.Count == 0) && (returns == null || returns.Count == 0))
                {
                    ExportStatusMessage = "Nenalezeny žádné účtenky ani vratky v zadaném období.";
                    return;
                }

                // Generate HTML
                var html = GenerateReceiptsHtml(receipts ?? new List<Receipt>(), returns ?? new List<Return>(), startDate, endDate);

                // Check if backup path is configured
                if (!_settingsService.IsBackupPathConfigured())
                {
                    ExportStatusMessage = "Chyba: Cesta pro zálohy není nastavena. Export není možný.";
                    return;
                }

                // Save to backup folder (same as database backups)
                var exportFolderPath = _settingsService.GetBackupFolderPath();
                Directory.CreateDirectory(exportFolderPath);

                var fileName = $"Uctenky_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.html";
                var filePath = Path.Combine(exportFolderPath, fileName);

                // Use UTF-8 with BOM for proper encoding of Czech characters in HTML
                File.WriteAllText(filePath, html, new System.Text.UTF8Encoding(true));

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

        private string GenerateReceiptsHtml(List<Receipt> receipts, List<Return> returns, DateTime startDate, DateTime endDate)
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

            // Detailed receipt items (for tax office purposes)
            sb.AppendLine("<h2 style='margin-top: 40px;'>Detailní položky (pro FÚ)</h2>");
            sb.AppendLine("<table>");
            sb.AppendLine("<thead>");
            sb.AppendLine("<tr>");
            sb.AppendLine("<th>Účtenka</th>");
            sb.AppendLine("<th>Datum</th>");
            sb.AppendLine("<th>Produkt (EAN)</th>");
            sb.AppendLine("<th>Název</th>");
            sb.AppendLine("<th>Množství</th>");
            sb.AppendLine("<th>Jednotková cena</th>");
            sb.AppendLine("<th>Celkem za položku</th>");

            // Show discount columns if any receipt has discounted items
            bool hasAnyDiscount = receipts.Any(r => r.Items.Any(i => i.HasDiscount));
            if (hasAnyDiscount)
            {
                sb.AppendLine("<th>Původní cena</th>");
                sb.AppendLine("<th>Sleva %</th>");
                sb.AppendLine("<th>Důvod slevy</th>");
            }

            // Only show VAT columns if company is VAT payer
            if (settings.IsVatPayer)
            {
                sb.AppendLine("<th>DPH %</th>");
                sb.AppendLine("<th>Základ</th>");
                sb.AppendLine("<th>DPH</th>");
            }

            sb.AppendLine("</tr>");
            sb.AppendLine("</thead>");
            sb.AppendLine("<tbody>");

            foreach (var receipt in receipts.OrderBy(r => r.SaleDate))
            {
                foreach (var item in receipt.Items)
                {
                    var rowClass = receipt.IsStorno ? " class='storno'" : "";
                    
                    sb.AppendLine($"<tr{rowClass}>");
                    sb.AppendLine($"<td>{receipt.FormattedReceiptNumber}</td>");
                    sb.AppendLine($"<td>{receipt.SaleDate:dd.MM.yyyy}</td>");
                    sb.AppendLine($"<td>{item.ProductEan}</td>");
                    sb.AppendLine($"<td>{item.ProductName}</td>");
                    sb.AppendLine($"<td style='text-align: center;'>{item.Quantity}</td>");
                    sb.AppendLine($"<td style='text-align: right;'>{item.UnitPrice:N2} Kč</td>");
                    sb.AppendLine($"<td style='text-align: right;'>{item.TotalPrice:N2} Kč</td>");

                    // Show discount columns if any receipt has discounted items
                    if (hasAnyDiscount)
                    {
                        if (item.HasDiscount)
                        {
                            sb.AppendLine($"<td style='text-align: right;'>{item.OriginalUnitPrice:N2} Kč</td>");
                            sb.AppendLine($"<td style='text-align: center;'>{item.DiscountPercent:F0}%</td>");
                            sb.AppendLine($"<td>{item.DiscountReason ?? ""}</td>");
                        }
                        else
                        {
                            sb.AppendLine("<td style='text-align: center;'>-</td>");
                            sb.AppendLine("<td style='text-align: center;'>-</td>");
                            sb.AppendLine("<td style='text-align: center;'>-</td>");
                        }
                    }

                    // Only show VAT columns if company is VAT payer
                    if (settings.IsVatPayer)
                    {
                        sb.AppendLine($"<td style='text-align: center;'>{item.VatRate:F0}%</td>");
                        sb.AppendLine($"<td style='text-align: right;'>{item.PriceWithoutVat:N2} Kč</td>");
                        sb.AppendLine($"<td style='text-align: right;'>{item.VatAmount:N2} Kč</td>");
                    }

                    sb.AppendLine("</tr>");
                }
            }

            sb.AppendLine("</tbody>");
            sb.AppendLine("</table>");

            // Returns section
            decimal totalReturnsAmount = 0;
            decimal totalReturnsWithoutVat = 0;
            decimal totalReturnsVat = 0;

            if (returns.Count > 0)
            {
                sb.AppendLine("<h2 style='margin-top: 40px; color: #c00;'>Vratky (dobropisy)</h2>");
                sb.AppendLine("<table>");
                sb.AppendLine("<thead>");
                sb.AppendLine("<tr>");
                sb.AppendLine("<th>Číslo vratky</th>");
                sb.AppendLine("<th>Datum a čas</th>");
                sb.AppendLine("<th>Původní účtenka</th>");
                sb.AppendLine("<th>Částka k vrácení (Kč)</th>");

                if (settings.IsVatPayer)
                {
                    sb.AppendLine("<th>Základ (Kč)</th>");
                    sb.AppendLine("<th>DPH (Kč)</th>");
                }

                sb.AppendLine("</tr>");
                sb.AppendLine("</thead>");
                sb.AppendLine("<tbody>");

                foreach (var ret in returns.OrderBy(r => r.ReturnDate))
                {
                    sb.AppendLine("<tr style='background-color: #ffe6e6;'>");
                    sb.AppendLine($"<td>{ret.FormattedReturnNumber}</td>");
                    sb.AppendLine($"<td>{ret.ReturnDate:dd.MM.yyyy HH:mm:ss}</td>");
                    sb.AppendLine($"<td>Účtenka #{ret.OriginalReceiptId}</td>");
                    sb.AppendLine($"<td style='text-align: right; color: #c00;'>-{ret.AmountToRefund:N2}</td>");

                    if (settings.IsVatPayer)
                    {
                        sb.AppendLine($"<td style='text-align: right;'>-{ret.TotalRefundAmountWithoutVat:N2}</td>");
                        sb.AppendLine($"<td style='text-align: right;'>-{ret.TotalRefundVatAmount:N2}</td>");
                    }

                    sb.AppendLine("</tr>");

                    totalReturnsAmount += ret.AmountToRefund;
                    totalReturnsWithoutVat += ret.TotalRefundAmountWithoutVat;
                    totalReturnsVat += ret.TotalRefundVatAmount;
                }

                sb.AppendLine("</tbody>");
                sb.AppendLine("</table>");

                // Returns detail items
                sb.AppendLine("<h3 style='margin-top: 20px;'>Detailní položky vratek</h3>");
                sb.AppendLine("<table>");
                sb.AppendLine("<thead>");
                sb.AppendLine("<tr>");
                sb.AppendLine("<th>Vratka</th>");
                sb.AppendLine("<th>Datum</th>");
                sb.AppendLine("<th>Produkt (EAN)</th>");
                sb.AppendLine("<th>Název</th>");
                sb.AppendLine("<th>Množství</th>");
                sb.AppendLine("<th>Jednotková cena</th>");
                sb.AppendLine("<th>Celkem za položku</th>");

                if (settings.IsVatPayer)
                {
                    sb.AppendLine("<th>DPH %</th>");
                    sb.AppendLine("<th>Základ</th>");
                    sb.AppendLine("<th>DPH</th>");
                }

                sb.AppendLine("</tr>");
                sb.AppendLine("</thead>");
                sb.AppendLine("<tbody>");

                foreach (var ret in returns.OrderBy(r => r.ReturnDate))
                {
                    if (ret.Items != null)
                    {
                        foreach (var item in ret.Items)
                        {
                            sb.AppendLine("<tr style='background-color: #ffe6e6;'>");
                            sb.AppendLine($"<td>{ret.FormattedReturnNumber}</td>");
                            sb.AppendLine($"<td>{ret.ReturnDate:dd.MM.yyyy}</td>");
                            sb.AppendLine($"<td>{item.ProductEan}</td>");
                            sb.AppendLine($"<td>{item.ProductName}</td>");
                            sb.AppendLine($"<td style='text-align: center;'>{item.ReturnedQuantity}</td>");
                            sb.AppendLine($"<td style='text-align: right;'>{item.UnitPrice:N2} Kč</td>");
                            sb.AppendLine($"<td style='text-align: right; color: #c00;'>-{item.TotalRefund:N2} Kč</td>");

                            if (settings.IsVatPayer)
                            {
                                sb.AppendLine($"<td style='text-align: center;'>{item.VatRate:F0}%</td>");
                                sb.AppendLine($"<td style='text-align: right;'>-{item.PriceWithoutVat:N2} Kč</td>");
                                sb.AppendLine($"<td style='text-align: right;'>-{item.VatAmount:N2} Kč</td>");
                            }

                            sb.AppendLine("</tr>");
                        }
                    }
                }

                sb.AppendLine("</tbody>");
                sb.AppendLine("</table>");
            }

            // Summary
            sb.AppendLine("<div class='summary'>");
            sb.AppendLine("<h2>Souhrn za období</h2>");
            sb.AppendLine($"<p><strong>Celkový počet účtenek:</strong> {receipts.Count}</p>");
            sb.AppendLine($"<p><strong>Celková částka:</strong> {totalAmount:N2} Kč</p>");

            // Discount summary if there are any discounts
            var discountedItems = receipts.SelectMany(r => r.Items).Where(i => i.HasDiscount);
            if (discountedItems.Any())
            {
                var totalDiscountAmount = discountedItems.Sum(i => i.TotalDiscountAmount);
                var totalOriginalAmount = discountedItems.Sum(i => i.OriginalUnitPrice * i.Quantity);
                var avgDiscountPercent = discountedItems.Average(i => i.DiscountPercent ?? 0);

                sb.AppendLine($"<p><strong>Položky se slevou:</strong> {discountedItems.Count()}</p>");
                sb.AppendLine($"<p><strong>Celková výše slev:</strong> {totalDiscountAmount:N2} Kč</p>");
                sb.AppendLine($"<p><strong>Původní hodnota zlevněných položek:</strong> {totalOriginalAmount:N2} Kč</p>");
                sb.AppendLine($"<p><strong>Průměrná sleva:</strong> {avgDiscountPercent:F1}%</p>");
            }

            // Only show VAT summary if company is VAT payer
            if (settings.IsVatPayer)
            {
                sb.AppendLine($"<p><strong>Základ (bez DPH):</strong> {totalWithoutVat:N2} Kč</p>");
                sb.AppendLine($"<p><strong>Celkem DPH:</strong> {totalVat:N2} Kč</p>");
            }

            // Returns summary
            if (returns.Count > 0)
            {
                sb.AppendLine($"<hr style='margin: 15px 0;' />");
                sb.AppendLine($"<p style='color: #c00;'><strong>Celkový počet vratek:</strong> {returns.Count}</p>");
                sb.AppendLine($"<p style='color: #c00;'><strong>Celková částka vratek:</strong> -{totalReturnsAmount:N2} Kč</p>");

                if (settings.IsVatPayer)
                {
                    sb.AppendLine($"<p style='color: #c00;'><strong>Základ vratek (bez DPH):</strong> -{totalReturnsWithoutVat:N2} Kč</p>");
                    sb.AppendLine($"<p style='color: #c00;'><strong>DPH vratek:</strong> -{totalReturnsVat:N2} Kč</p>");
                }

                // Net totals
                sb.AppendLine($"<hr style='margin: 15px 0;' />");
                sb.AppendLine($"<p><strong>ČISTÝ OBRAT (tržby - vratky):</strong> {(totalAmount - totalReturnsAmount):N2} Kč</p>");

                if (settings.IsVatPayer)
                {
                    sb.AppendLine($"<p><strong>Čistý základ (bez DPH):</strong> {(totalWithoutVat - totalReturnsWithoutVat):N2} Kč</p>");
                    sb.AppendLine($"<p><strong>Čisté DPH:</strong> {(totalVat - totalReturnsVat):N2} Kč</p>");
                }
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

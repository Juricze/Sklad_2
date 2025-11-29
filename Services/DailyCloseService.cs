using Microsoft.EntityFrameworkCore;
using Sklad_2.Data;
using Sklad_2.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sklad_2.Services
{
    public class DailyCloseService : IDailyCloseService
    {
        private readonly IDbContextFactory<DatabaseContext> _contextFactory;
        private readonly ISettingsService _settingsService;

        public DailyCloseService(IDbContextFactory<DatabaseContext> contextFactory, ISettingsService settingsService)
        {
            _contextFactory = contextFactory;
            _settingsService = settingsService;
        }

        public async Task<(decimal CashSales, decimal CardSales, decimal TotalSales, int ReceiptCount)> GetTodaySalesAsync()
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                // KRITICKÉ: Použít datum poslední session (LastSaleLoginDate) místo DateTime.Today
                // Pro správné zachycení tržeb ze dne kdy byl den zahájen
                var sessionDate = _settingsService.CurrentSettings.LastSaleLoginDate?.Date ?? DateTime.Today;

                // Načíst VŠECHNY účtenky ze session dne (normální i stornované)
                var allReceipts = await context.Receipts
                    .AsNoTracking()
                    .Where(r => r.SaleDate.Date == sessionDate)
                    .ToListAsync();

                // Normální účtenky - přičíst
                var normalReceipts = allReceipts.Where(r => !r.IsStorno).ToList();
                var cashSales = normalReceipts.Sum(r => r.CashAmount);
                var cardSales = normalReceipts.Sum(r => r.CardAmount);

                // Stornované účtenky - přičíst (mají záporné hodnoty, takže přičtení = odečtení)
                // Storno účtenka má CashAmount = -100, takže cashSales + (-100) = cashSales - 100
                var stornoReceipts = allReceipts.Where(r => r.IsStorno).ToList();
                cashSales += stornoReceipts.Sum(r => r.CashAmount); // Přičíst zápornou hodnotu
                cardSales += stornoReceipts.Sum(r => r.CardAmount); // Přičíst zápornou hodnotu

                // Načíst vratky ze session dne - odečíst od hotovostní tržby (vracíme vždy v hotovosti)
                // DRY: Use AmountToRefund (after loyalty discount) - actual amount returned to customer
                var todayReturns = await context.Returns
                    .AsNoTracking()
                    .Where(r => r.ReturnDate.Date == sessionDate)
                    .ToListAsync();

                var returnAmount = todayReturns.Sum(r => r.AmountToRefund);
                cashSales -= returnAmount; // Vratky odečítáme od hotovostní tržby

                var totalSales = cashSales + cardSales;
                var receiptCount = allReceipts.Count; // Počet VŠECH účtenek (včetně storno)

                Debug.WriteLine($"DailyCloseService: Session ({sessionDate:yyyy-MM-dd}) sales - Normal: {normalReceipts.Count}, Storno: {stornoReceipts.Count}, Returns: {todayReturns.Count}, Total receipts: {receiptCount}, Cash: {cashSales:N2}, Card: {cardSales:N2}, Total: {totalSales:N2}");

                return (cashSales, cardSales, totalSales, receiptCount);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DailyCloseService: Error getting today sales: {ex.Message}");
                return (0, 0, 0, 0);
            }
        }

        public async Task<bool> IsDayClosedAsync(DateTime date)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();
                var dateOnly = date.Date;

                var exists = await context.DailyCloses
                    .AsNoTracking()
                    .AnyAsync(dc => dc.Date.Date == dateOnly);

                Debug.WriteLine($"DailyCloseService: IsDayClosed({dateOnly:yyyy-MM-dd}) = {exists}");
                return exists;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DailyCloseService: Error checking if day closed: {ex.Message}");
                return false;
            }
        }

        public async Task<(bool Success, string ErrorMessage, DailyClose DailyClose)> CloseDayAsync(string sellerName)
        {
            try
            {
                // KRITICKÉ: Použít datum poslední session (LastSaleLoginDate) místo DateTime.Today
                // Pro správné uzavření dne kdy byl den zahájen (ne aktuální kalendářní den!)
                var sessionDate = _settingsService.CurrentSettings.LastSaleLoginDate?.Date ?? DateTime.Today;

                // Kontrola zda už není uzavřeno
                if (await IsDayClosedAsync(sessionDate))
                {
                    return (false, $"Den {sessionDate:dd.MM.yyyy} již byl uzavřen.", null);
                }

                using var context = await _contextFactory.CreateDbContextAsync();

                // Načíst VŠECHNY účtenky ze session dne (normální i stornované)
                var allReceipts = await context.Receipts
                    .AsNoTracking()
                    .Where(r => r.SaleDate.Date == sessionDate)
                    .OrderBy(r => r.ReceiptSequence)
                    .ToListAsync();

                // Oddělit normální a stornované účtenky
                var normalReceipts = allReceipts.Where(r => !r.IsStorno).ToList();
                var stornoReceipts = allReceipts.Where(r => r.IsStorno).ToList();

                if (normalReceipts.Count == 0)
                {
                    return (false, $"Nelze uzavřít den {sessionDate:dd.MM.yyyy} bez účtenek.", null);
                }

                // Vypočítat tržby - normální účtenky přičíst, stornované přičíst (záporné hodnoty)
                var cashSales = normalReceipts.Sum(r => r.CashAmount);
                var cardSales = normalReceipts.Sum(r => r.CardAmount);

                // Stornované účtenky přičíst (mají záporné hodnoty, takže přičtení = odečtení)
                cashSales += stornoReceipts.Sum(r => r.CashAmount);
                cardSales += stornoReceipts.Sum(r => r.CardAmount);

                // Načíst vratky ze session dne - odečíst od hotovostní tržby
                var todayReturns = await context.Returns
                    .AsNoTracking()
                    .Where(r => r.ReturnDate.Date == sessionDate)
                    .ToListAsync();

                // DRY: Use AmountToRefund (after loyalty discount) - actual amount returned to customer
                var returnAmount = todayReturns.Sum(r => r.AmountToRefund);
                cashSales -= returnAmount; // Vratky odečítáme od hotovostní tržby

                var totalSales = cashSales + cardSales;

                // Načíst nastavení DPH
                var settings = _settingsService.CurrentSettings;
                decimal? vatAmount = null;

                if (settings.IsVatPayer)
                {
                    // Součet DPH z normálních účtenek, plus DPH ze stornovaných (záporné hodnoty)
                    vatAmount = normalReceipts.Sum(r => r.TotalVatAmount) + stornoReceipts.Sum(r => r.TotalVatAmount);
                }

                // Rozmezí účtenek - první a poslední VŠECH účtenek (včetně storno)
                // allReceipts jsou už seřazené podle ReceiptSequence
                var firstReceipt = allReceipts.First();
                var lastReceipt = allReceipts.Last();

                // Vytvořit uzavírku s datem SESSION (ne Today!)
                var dailyClose = new DailyClose
                {
                    Date = sessionDate, // KRITICKÉ: Datum dne kdy byl den zahájen
                    CashSales = cashSales,
                    CardSales = cardSales,
                    TotalSales = totalSales,
                    VatAmount = vatAmount,
                    SellerName = sellerName,
                    ReceiptNumberFrom = firstReceipt.FormattedReceiptNumber,
                    ReceiptNumberTo = lastReceipt.FormattedReceiptNumber,
                    ClosedAt = DateTime.Now
                };

                context.DailyCloses.Add(dailyClose);
                await context.SaveChangesAsync();

                Debug.WriteLine($"DailyCloseService: Day closed - Session: {sessionDate:yyyy-MM-dd}, Closed at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}, Total: {totalSales:N2} Kč");

                return (true, string.Empty, dailyClose);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DailyCloseService: Error closing day: {ex.Message}");
                return (false, $"Chyba při uzavírání dne: {ex.Message}", null);
            }
        }

        public async Task<List<DailyClose>> GetDailyClosesAsync(DateTime? fromDate = null, DateTime? toDate = null)
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var query = context.DailyCloses.AsNoTracking();

                if (fromDate.HasValue)
                {
                    query = query.Where(dc => dc.Date.Date >= fromDate.Value.Date);
                }

                if (toDate.HasValue)
                {
                    query = query.Where(dc => dc.Date.Date <= toDate.Value.Date);
                }

                var closes = await query
                    .OrderByDescending(dc => dc.Date)
                    .ToListAsync();

                Debug.WriteLine($"DailyCloseService: Retrieved {closes.Count} daily closes");
                return closes;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DailyCloseService: Error getting daily closes: {ex.Message}");
                return new List<DailyClose>();
            }
        }

        public async Task<(bool Success, string FilePath, string ErrorMessage)> ExportDailyClosesAsync(string period, DateTime referenceDate)
        {
            try
            {
                // Vypočítat rozmezí dat podle období
                var (fromDate, toDate) = CalculatePeriodRange(period, referenceDate);

                // Načíst uzavírky
                var closes = await GetDailyClosesAsync(fromDate, toDate);

                if (closes.Count == 0)
                {
                    return (false, string.Empty, "Žádné uzavírky pro export.");
                }

                // Načíst nastavení pro export cestu
                var settings = _settingsService.CurrentSettings;
                string exportPath;

                if (!string.IsNullOrWhiteSpace(settings.BackupPath) && Directory.Exists(settings.BackupPath))
                {
                    exportPath = settings.BackupPath;
                }
                else
                {
                    var oneDrivePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    oneDrivePath = Path.Combine(oneDrivePath, "OneDrive", "Sklad_2_Exports");

                    if (!Directory.Exists(oneDrivePath))
                    {
                        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                        exportPath = Path.Combine(documentsPath, "Sklad_2_Exports");
                    }
                    else
                    {
                        exportPath = oneDrivePath;
                    }
                }

                Directory.CreateDirectory(exportPath);

                // Vytvořit název souboru
                var periodName = GetPeriodName(period);
                var fileName = $"Uzavirky_{periodName}_{fromDate:yyyyMMdd}-{toDate:yyyyMMdd}.html";
                var filePath = Path.Combine(exportPath, fileName);

                // Sestavit HTML obsah exportu
                var html = await GenerateClosesHtmlAsync(closes, fromDate, toDate, periodName, settings.ShopName, settings.ShopAddress, settings.CompanyId, settings.VatId, settings.IsVatPayer);

                // Zapsat do souboru s UTF-8 BOM
                await File.WriteAllTextAsync(filePath, html, new UTF8Encoding(true));

                Debug.WriteLine($"DailyCloseService: Exported {closes.Count} closes to {filePath}");

                // Otevřít v prohlížeči
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = filePath,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"DailyCloseService: Failed to open HTML in browser: {ex.Message}");
                }

                return (true, filePath, string.Empty);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DailyCloseService: Error exporting daily closes: {ex.Message}");
                return (false, string.Empty, $"Chyba při exportu: {ex.Message}");
            }
        }

        public async Task<DateTime?> GetLastCloseDateAsync()
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                var lastClose = await context.DailyCloses
                    .AsNoTracking()
                    .OrderByDescending(dc => dc.Date)
                    .FirstOrDefaultAsync();

                return lastClose?.Date;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DailyCloseService: Error getting last close date: {ex.Message}");
                return null;
            }
        }

        public async Task<List<DailySalesSummary>> GetCurrentMonthDailySalesAsync()
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                // Aktuální kalendářní měsíc
                var today = DateTime.Today;
                var firstDayOfMonth = new DateTime(today.Year, today.Month, 1);
                var lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1);

                // Načíst pouze UZAVŘENÉ dny z DailyCloses tabulky
                var dailyCloses = await context.DailyCloses
                    .AsNoTracking()
                    .Where(dc => dc.Date.Date >= firstDayOfMonth && dc.Date.Date <= lastDayOfMonth)
                    .OrderByDescending(dc => dc.Date)
                    .ToListAsync();

                var summaries = dailyCloses.Select(dc => new DailySalesSummary
                {
                    Date = dc.Date,
                    ReceiptRangeFrom = dc.ReceiptNumberFrom ?? "",
                    ReceiptRangeTo = dc.ReceiptNumberTo ?? "",
                    CashSales = dc.CashSales,
                    CardSales = dc.CardSales,
                    TotalSales = dc.TotalSales,
                    ReceiptCount = 0 // DailyClose nemá počet účtenek, můžeme přidat později
                }).ToList();

                Debug.WriteLine($"DailyCloseService: Retrieved {summaries.Count} closed days for {today:MMMM yyyy}");
                return summaries;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DailyCloseService: Error getting current month daily sales: {ex.Message}");
                return new List<DailySalesSummary>();
            }
        }

        // Helper methods

        private (DateTime FromDate, DateTime ToDate) CalculatePeriodRange(string period, DateTime referenceDate)
        {
            var refDate = referenceDate.Date;

            return period.ToLower() switch
            {
                "month" => (new DateTime(refDate.Year, refDate.Month, 1),
                            new DateTime(refDate.Year, refDate.Month, DateTime.DaysInMonth(refDate.Year, refDate.Month))),

                "quarter" => CalculateQuarterRange(refDate),

                "halfyear" => refDate.Month <= 6
                    ? (new DateTime(refDate.Year, 1, 1), new DateTime(refDate.Year, 6, 30))
                    : (new DateTime(refDate.Year, 7, 1), new DateTime(refDate.Year, 12, 31)),

                "year" => (new DateTime(refDate.Year, 1, 1), new DateTime(refDate.Year, 12, 31)),

                _ => (refDate, refDate)
            };
        }

        private (DateTime FromDate, DateTime ToDate) CalculateQuarterRange(DateTime date)
        {
            int quarter = (date.Month - 1) / 3 + 1;
            int startMonth = (quarter - 1) * 3 + 1;
            int endMonth = startMonth + 2;

            return (new DateTime(date.Year, startMonth, 1),
                    new DateTime(date.Year, endMonth, DateTime.DaysInMonth(date.Year, endMonth)));
        }

        private string GetPeriodName(string period)
        {
            return period.ToLower() switch
            {
                "month" => "Mesicni",
                "quarter" => "Ctvrtletni",
                "halfyear" => "Pulrocni",
                "year" => "Rocni",
                _ => "Vlastni"
            };
        }

        private async Task<string> GenerateClosesHtmlAsync(List<DailyClose> closes, DateTime fromDate, DateTime toDate, string periodName, string shopName, string shopAddress, string companyId, string vatId, bool isVatPayer)
        {
            var sb = new StringBuilder();

            // Načíst všechny účtenky a vratky za období
            using var context = await _contextFactory.CreateDbContextAsync();

            var receipts = await context.Receipts
                .AsNoTracking()
                .Where(r => r.SaleDate.Date >= fromDate.Date && r.SaleDate.Date <= toDate.Date)
                .OrderBy(r => r.ReceiptYear).ThenBy(r => r.ReceiptSequence)
                .ToListAsync();

            var returns = await context.Returns
                .AsNoTracking()
                .Where(r => r.ReturnDate.Date >= fromDate.Date && r.ReturnDate.Date <= toDate.Date)
                .OrderBy(r => r.ReturnYear).ThenBy(r => r.ReturnSequence)
                .ToListAsync();

            // Rozmezí účtenek a vratek
            string receiptRange = receipts.Count > 0
                ? $"{receipts.First().FormattedReceiptNumber} - {receipts.Last().FormattedReceiptNumber}"
                : "Žádné účtenky";

            string returnRange = returns.Count > 0
                ? $"{returns.First().FormattedReturnNumber} - {returns.Last().FormattedReturnNumber}"
                : "Žádné vratky";

            // Celkové tržby
            var totalCash = closes.Sum(c => c.CashSales);
            var totalCard = closes.Sum(c => c.CardSales);
            var totalSales = closes.Sum(c => c.TotalSales);
            var totalVat = closes.Sum(c => c.VatAmount ?? 0);

            // HTML generování
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang='cs'>");
            sb.AppendLine("<head>");
            sb.AppendLine("<meta charset='UTF-8'>");
            sb.AppendLine($"<title>Uzavírka - {periodName}</title>");
            sb.AppendLine("<style>");
            sb.AppendLine("body { font-family: Arial, sans-serif; margin: 40px; background-color: #f5f5f5; }");
            sb.AppendLine(".container { max-width: 800px; margin: 0 auto; background-color: white; padding: 30px; box-shadow: 0 0 10px rgba(0,0,0,0.1); }");
            sb.AppendLine(".company-info { margin-bottom: 30px; padding: 20px; background-color: #e8f4f8; border-left: 4px solid #0078d4; }");
            sb.AppendLine("h1 { color: #333; margin-bottom: 10px; }");
            sb.AppendLine("h2 { color: #0078d4; margin-top: 30px; margin-bottom: 15px; }");
            sb.AppendLine(".info-row { display: flex; justify-content: space-between; margin: 10px 0; padding: 10px; background-color: #f9f9f9; }");
            sb.AppendLine(".info-label { font-weight: bold; color: #555; }");
            sb.AppendLine(".info-value { color: #333; }");
            sb.AppendLine(".summary { margin-top: 30px; padding: 20px; background-color: #f0f7ff; border: 2px solid #0078d4; border-radius: 5px; }");
            sb.AppendLine(".summary-row { display: flex; justify-content: space-between; margin: 15px 0; font-size: 18px; }");
            sb.AppendLine(".summary-row.total { font-size: 24px; font-weight: bold; color: #0078d4; border-top: 2px solid #0078d4; padding-top: 15px; margin-top: 20px; }");
            sb.AppendLine("@media print { body { margin: 0; background-color: white; } .container { box-shadow: none; } }");
            sb.AppendLine("</style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            sb.AppendLine("<div class='container'>");

            // Informace o firmě
            sb.AppendLine("<div class='company-info'>");
            sb.AppendLine($"<h2>{shopName}</h2>");
            sb.AppendLine($"<p>{shopAddress}</p>");
            sb.AppendLine($"<p>IČ: {companyId}");
            if (isVatPayer)
            {
                sb.AppendLine($" | DIČ: {vatId}");
            }
            sb.AppendLine("</p>");
            sb.AppendLine($"<p>Plátce DPH: {(isVatPayer ? "Ano" : "Ne")}</p>");
            sb.AppendLine("</div>");

            // Hlavička exportu
            sb.AppendLine($"<h1>Export uzavírek - {periodName}</h1>");
            sb.AppendLine($"<p><strong>Období:</strong> {fromDate:dd.MM.yyyy} - {toDate:dd.MM.yyyy}</p>");
            sb.AppendLine($"<p><strong>Datum exportu:</strong> {DateTime.Now:dd.MM.yyyy HH:mm:ss}</p>");
            sb.AppendLine($"<p><strong>Počet uzavírek:</strong> {closes.Count}</p>");

            // Rozmezí dokladů
            sb.AppendLine("<h2>Rozmezí dokladů</h2>");
            sb.AppendLine("<div class='info-row'>");
            sb.AppendLine("<span class='info-label'>Rozmezí účtenek:</span>");
            sb.AppendLine($"<span class='info-value'>{receiptRange}</span>");
            sb.AppendLine("</div>");
            sb.AppendLine("<div class='info-row'>");
            sb.AppendLine("<span class='info-label'>Rozmezí vratek:</span>");
            sb.AppendLine($"<span class='info-value'>{returnRange}</span>");
            sb.AppendLine("</div>");
            sb.AppendLine("<div class='info-row'>");
            sb.AppendLine("<span class='info-label'>Počet účtenek:</span>");
            sb.AppendLine($"<span class='info-value'>{receipts.Count}</span>");
            sb.AppendLine("</div>");
            sb.AppendLine("<div class='info-row'>");
            sb.AppendLine("<span class='info-label'>Počet vratek:</span>");
            sb.AppendLine($"<span class='info-value'>{returns.Count}</span>");
            sb.AppendLine("</div>");

            // Souhrn tržeb
            sb.AppendLine("<div class='summary'>");
            sb.AppendLine("<h2 style='margin-top: 0;'>Souhrn tržeb</h2>");
            sb.AppendLine("<div class='summary-row'>");
            sb.AppendLine("<span>Tržba hotovost:</span>");
            sb.AppendLine($"<span>{totalCash:N2} Kč</span>");
            sb.AppendLine("</div>");
            sb.AppendLine("<div class='summary-row'>");
            sb.AppendLine("<span>Tržba karta:</span>");
            sb.AppendLine($"<span>{totalCard:N2} Kč</span>");
            sb.AppendLine("</div>");

            if (isVatPayer)
            {
                sb.AppendLine("<div class='summary-row'>");
                sb.AppendLine("<span>DPH celkem:</span>");
                sb.AppendLine($"<span>{totalVat:N2} Kč</span>");
                sb.AppendLine("</div>");
            }

            sb.AppendLine("<div class='summary-row total'>");
            sb.AppendLine("<span>Celková tržba:</span>");
            sb.AppendLine($"<span>{totalSales:N2} Kč</span>");
            sb.AppendLine("</div>");
            sb.AppendLine("</div>");

            sb.AppendLine("</div>"); // container
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            return sb.ToString();
        }
    }
}

using Sklad_2.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Sklad_2.Services
{
    public interface IDailyCloseService
    {
        /// <summary>
        /// Vrátí aktuální tržby dnešního dne (hotovost, karta, celkem)
        /// </summary>
        Task<(decimal CashSales, decimal CardSales, decimal TotalSales, int ReceiptCount)> GetTodaySalesAsync();

        /// <summary>
        /// Kontrola, zda je daný den již uzavřen
        /// </summary>
        Task<bool> IsDayClosedAsync(DateTime date);

        /// <summary>
        /// Uzavře aktuální den a vytvoří záznam uzavírky
        /// </summary>
        /// <returns>True pokud se uzavírka podařila, false pokud už je den uzavřený</returns>
        Task<(bool Success, string ErrorMessage, DailyClose DailyClose)> CloseDayAsync(string sellerName);

        /// <summary>
        /// Vrátí seznam uzavírek s možností filtrování
        /// </summary>
        Task<List<DailyClose>> GetDailyClosesAsync(DateTime? fromDate = null, DateTime? toDate = null);

        /// <summary>
        /// Exportuje uzavírky za dané období do textového souboru (formát jako účtenky)
        /// </summary>
        /// <param name="period">"month", "quarter", "halfyear", "year"</param>
        /// <param name="referenceDate">Referenční datum (pro výpočet období)</param>
        Task<(bool Success, string FilePath, string ErrorMessage)> ExportDailyClosesAsync(string period, DateTime referenceDate);

        /// <summary>
        /// Vrátí datum poslední uzavírky (nebo null pokud ještě nebyla žádná)
        /// </summary>
        Task<DateTime?> GetLastCloseDateAsync();

        /// <summary>
        /// Vrátí přehled denních tržeb pro aktuální kalendářní měsíc
        /// </summary>
        Task<List<DailySalesSummary>> GetCurrentMonthDailySalesAsync();
    }
}

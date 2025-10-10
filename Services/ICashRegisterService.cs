using Sklad_2.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Sklad_2.Services
{
    public interface ICashRegisterService
    {
        Task<decimal> GetCurrentCashInTillAsync();
        Task RecordEntryAsync(EntryType type, decimal amount, string description);
        Task SetDayStartCashAsync(decimal initialAmount);
        Task MakeDepositAsync(decimal amount);
        Task<List<CashRegisterEntry>> GetCashRegisterHistoryAsync();
        Task PerformDailyReconciliationAsync(decimal actualAmount);
        Task<(bool Success, string ErrorMessage)> PerformDayCloseAsync(decimal actualAmount);
    }
}
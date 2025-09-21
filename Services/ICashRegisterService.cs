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
        Task InitializeTillAsync(decimal initialAmount);
        Task<List<CashRegisterEntry>> GetCashRegisterHistoryAsync();
        Task PerformDailyReconciliationAsync(decimal actualAmount);
    }
}
using Sklad_2.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Sklad_2.Services
{
    public class DummyCashRegisterService : ICashRegisterService
    {
        public Task<decimal> GetCurrentCashInTillAsync()
        {
            return Task.FromResult(123.45m);
        }

        public Task RecordEntryAsync(EntryType type, decimal amount, string description)
        {
            return Task.CompletedTask;
        }

        public Task SetDayStartCashAsync(decimal initialAmount)
        {
            return Task.CompletedTask;
        }

        public Task MakeDepositAsync(decimal amount)
        {
            return Task.CompletedTask;
        }

        public Task<List<CashRegisterEntry>> GetCashRegisterHistoryAsync()
        {
            var history = new List<CashRegisterEntry>
            {
                new CashRegisterEntry { Timestamp = DateTime.Now.AddHours(-1), Type = EntryType.Sale, Amount = 50m, Description = "Test Sale", CurrentCashInTill = 100m },
                new CashRegisterEntry { Timestamp = DateTime.Now.AddHours(-2), Type = EntryType.DayStart, Amount = 100m, Description = "Day Start", CurrentCashInTill = 100m }
            };
            return Task.FromResult(history);
        }

        public Task PerformDailyReconciliationAsync(decimal actualAmount)
        {
            return Task.CompletedTask;
        }

        public Task<(bool Success, string ErrorMessage)> PerformDayCloseAsync(decimal actualAmount)
        {
            // Dummy implementation - always succeeds
            return Task.FromResult((true, string.Empty));
        }
    }
}
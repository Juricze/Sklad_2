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

        public Task InitializeTillAsync(decimal initialAmount)
        {
            return Task.CompletedTask;
        }

        public Task<List<CashRegisterEntry>> GetCashRegisterHistoryAsync()
        {
            var history = new List<CashRegisterEntry>
            {
                new CashRegisterEntry { Timestamp = DateTime.Now.AddHours(-1), Type = EntryType.Sale, Amount = 50m, Description = "Test Sale", CurrentCashInTill = 100m },
                new CashRegisterEntry { Timestamp = DateTime.Now.AddHours(-2), Type = EntryType.InitialDeposit, Amount = 100m, Description = "Initial Till", CurrentCashInTill = 50m }
            };
            return Task.FromResult(history);
        }

        public Task PerformDailyReconciliationAsync(decimal actualAmount)
        {
            return Task.CompletedTask;
        }
    }
}
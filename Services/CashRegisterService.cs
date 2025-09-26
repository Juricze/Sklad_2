using Microsoft.EntityFrameworkCore;
using Sklad_2.Data;
using Sklad_2.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Sklad_2.Services
{
    public class CashRegisterService : ICashRegisterService
    {
        private readonly IDbContextFactory<DatabaseContext> _contextFactory;

        public CashRegisterService(IDbContextFactory<DatabaseContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<decimal> GetCurrentCashInTillAsync()
        {
            using var context = _contextFactory.CreateDbContext();
            return await context.CashRegisterEntries
                                 .OrderByDescending(e => e.Timestamp)
                                 .Select(e => e.CurrentCashInTill)
                                 .FirstOrDefaultAsync();
        }

        public async Task RecordEntryAsync(EntryType type, decimal amount, string description)
        {
            using var context = _contextFactory.CreateDbContext();
            var currentCash = await context.CashRegisterEntries
                                           .OrderByDescending(e => e.Timestamp)
                                           .Select(e => e.CurrentCashInTill)
                                           .FirstOrDefaultAsync();

            decimal newCashInTill = currentCash;

            switch (type)
            {
                case EntryType.InitialDeposit:
                case EntryType.Deposit:
                case EntryType.Sale:
                    newCashInTill += amount;
                    break;
                case EntryType.Withdrawal:
                case EntryType.DailyReconciliation:
                    newCashInTill -= amount;
                    break;
            }

            var entry = new CashRegisterEntry
            {
                Timestamp = DateTime.Now,
                Type = type,
                Amount = amount,
                Description = description,
                CurrentCashInTill = newCashInTill
            };

            context.CashRegisterEntries.Add(entry);
            await context.SaveChangesAsync();
        }

        public async Task InitializeTillAsync(decimal initialAmount)
        {
            using var context = _contextFactory.CreateDbContext();
            var hasEntries = await context.CashRegisterEntries.AnyAsync();
            if (!hasEntries)
            {
                await RecordEntryAsync(EntryType.InitialDeposit, initialAmount, "Počáteční vklad do pokladny");
            }
            else
            {
                await RecordEntryAsync(EntryType.Deposit, initialAmount, "Vklad do pokladny");
            }
        }

        public async Task<List<CashRegisterEntry>> GetCashRegisterHistoryAsync()
        {
            using var context = _contextFactory.CreateDbContext();
            return await context.CashRegisterEntries.OrderByDescending(e => e.Timestamp).ToListAsync();
        }

        public async Task<List<CashRegisterEntry>> GetCashRegisterHistoryAsync(DateTime startDate, DateTime endDate)
        {
            using var context = _contextFactory.CreateDbContext();
            return await context.CashRegisterEntries
                                .Where(e => e.Timestamp >= startDate && e.Timestamp <= endDate)
                                .OrderByDescending(e => e.Timestamp)
                                .ToListAsync();
        }

        public async Task PerformDailyReconciliationAsync(decimal actualAmount)
        {
            var currentCash = await GetCurrentCashInTillAsync();
            var difference = currentCash - actualAmount;

            if (difference != 0)
            {
                await RecordEntryAsync(EntryType.DailyReconciliation, difference, $"Denní kontrola pokladny. Rozdíl: {difference:C}");
            }
        }
    }
}
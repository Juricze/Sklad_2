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
        private readonly DatabaseContext _context;

        public CashRegisterService(DatabaseContext context)
        {
            _context = context;
        }

        public async Task<decimal> GetCurrentCashInTillAsync()
        {
            return await _context.CashRegisterEntries
                                 .OrderByDescending(e => e.Timestamp)
                                 .Select(e => e.CurrentCashInTill)
                                 .FirstOrDefaultAsync();
        }

        public async Task RecordEntryAsync(EntryType type, decimal amount, string description)
        {
            var currentCash = await GetCurrentCashInTillAsync();
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

            _context.CashRegisterEntries.Add(entry);
            await _context.SaveChangesAsync();
        }

        public async Task InitializeTillAsync(decimal initialAmount)
        {
            var hasEntries = await _context.CashRegisterEntries.AnyAsync();
            if (!hasEntries)
            {
                await RecordEntryAsync(EntryType.InitialDeposit, initialAmount, "Počáteční vklad do pokladny");
            }
            else
            {
                // Můžeme přidat logiku pro resetování pokladny, pokud je to potřeba
                // Prozatím jen zaznamenáme jako běžný vklad, pokud už existují záznamy
                await RecordEntryAsync(EntryType.Deposit, initialAmount, "Vklad do pokladny");
            }
        }

        public async Task<List<CashRegisterEntry>> GetCashRegisterHistoryAsync()
        {
            return await _context.CashRegisterEntries.OrderByDescending(e => e.Timestamp).ToListAsync();
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
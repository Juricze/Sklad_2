using Microsoft.EntityFrameworkCore;
using Sklad_2.Data;
using Sklad_2.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;

namespace Sklad_2.Services
{
    public class CashRegisterService : ICashRegisterService
    {
        private readonly IDbContextFactory<DatabaseContext> _dbContextFactory;
        private readonly IMessenger _messenger;
        private readonly ISettingsService _settingsService;

        public CashRegisterService(IDbContextFactory<DatabaseContext> dbContextFactory, IMessenger messenger, ISettingsService settingsService)
        {
            _dbContextFactory = dbContextFactory;
            _messenger = messenger;
            _settingsService = settingsService;
        }

        public async Task<decimal> GetCurrentCashInTillAsync()
        {
            using var context = _dbContextFactory.CreateDbContext();
            return await context.CashRegisterEntries
                                 .OrderByDescending(e => e.Timestamp)
                                 .Select(e => e.CurrentCashInTill)
                                 .FirstOrDefaultAsync();
        }

        public async Task RecordEntryAsync(EntryType type, decimal amount, string description)
        {
            using var context = _dbContextFactory.CreateDbContext();
            var currentCash = await context.CashRegisterEntries
                                           .OrderByDescending(e => e.Timestamp)
                                           .Select(e => e.CurrentCashInTill)
                                           .FirstOrDefaultAsync();

            decimal newCashInTill = currentCash;

            switch (type)
            {
                case EntryType.Deposit:
                case EntryType.Sale:
                    newCashInTill += amount;
                    break;
                case EntryType.Withdrawal:
                case EntryType.DailyReconciliation:
                    newCashInTill -= amount;
                    break;
                case EntryType.Return:
                    newCashInTill -= amount;
                    break;
                case EntryType.DayClose:
                case EntryType.DayStart:
                    // Day close/start sets the till to the specified amount
                    newCashInTill = amount;
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

        public async Task SetDayStartCashAsync(decimal initialAmount)
        {
            await RecordEntryAsync(EntryType.DayStart, initialAmount, "Zahájení nového dne - počáteční stav pokladny");
        }

        public async Task MakeDepositAsync(decimal amount)
        {
            await RecordEntryAsync(EntryType.Deposit, amount, "Vklad do pokladny");
        }

        public async Task<List<CashRegisterEntry>> GetCashRegisterHistoryAsync()
        {
            using var context = _dbContextFactory.CreateDbContext();
            return await context.CashRegisterEntries.OrderByDescending(e => e.Timestamp).ToListAsync();
        }

        public async Task<List<CashRegisterEntry>> GetCashRegisterHistoryAsync(DateTime startDate, DateTime endDate)
        {
            using var context = _dbContextFactory.CreateDbContext();
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

        public async Task<(bool Success, string ErrorMessage)> PerformDayCloseAsync(decimal actualAmount)
        {
            try
            {
                var today = DateTime.Today;
                var lastCloseDate = _settingsService.CurrentSettings.LastDayCloseDate?.Date;

                // Check if already closed today
                if (lastCloseDate.HasValue && lastCloseDate.Value == today)
                {
                    return (false, $"Denní uzavírka již byla provedena dne {lastCloseDate.Value:dd.MM.yyyy}. Uzavírku lze provést pouze jednou denně.");
                }

                // Validate actual amount
                if (actualAmount < 0)
                {
                    return (false, "Částka nesmí být záporná.");
                }

                if (actualAmount > 10000000)
                {
                    return (false, "Částka je příliš vysoká (maximum 10 000 000 Kč).");
                }

                // Get current cash from system
                var currentCash = await GetCurrentCashInTillAsync();
                var difference = currentCash - actualAmount;

                // Record the day close entry
                var description = difference == 0
                    ? "Denní uzavírka - bez rozdílu"
                    : $"Denní uzavírka - rozdíl: {difference:C} ({(difference > 0 ? "přebytek" : "manko")})";

                await RecordEntryAsync(EntryType.DayClose, actualAmount, description);

                // Update last close date
                _settingsService.CurrentSettings.LastDayCloseDate = today;
                await _settingsService.SaveSettingsAsync();

                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                return (false, $"Chyba při uzavírání dne: {ex.Message}");
            }
        }
    }
}
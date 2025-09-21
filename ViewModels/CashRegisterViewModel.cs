using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sklad_2.Models;
using Sklad_2.Services;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using Sklad_2.Messages;
using Sklad_2.Extensions;

namespace Sklad_2.ViewModels
{
    public partial class CashRegisterViewModel : ObservableObject, IRecipient<CashRegisterUpdatedMessage>
    {
        private readonly ICashRegisterService _cashRegisterService;

        [ObservableProperty]
        private decimal currentCashInTill;

        [ObservableProperty]
        private decimal initialAmount;

        [ObservableProperty]
        private decimal actualAmountForReconciliation;

        [ObservableProperty]
        private ObservableCollection<CashRegisterEntry> cashRegisterHistory;

        public CashRegisterViewModel(ICashRegisterService cashRegisterService)
        {
            _cashRegisterService = cashRegisterService;
            CashRegisterHistory = new ObservableCollection<CashRegisterEntry>();
            LoadCashRegisterDataAsync();

            // Register for messages
            WeakReferenceMessenger.Default.Register<CashRegisterUpdatedMessage, string>(this, "CashRegisterUpdateToken");
        }

        [RelayCommand]
        private async Task LoadCashRegisterDataAsync()
        {
            CurrentCashInTill = await _cashRegisterService.GetCurrentCashInTillAsync();
            var history = await _cashRegisterService.GetCashRegisterHistoryAsync();
            CashRegisterHistory.Clear();
            foreach (var entry in history)
            {
                CashRegisterHistory.Add(entry);
            }
        }

        [RelayCommand]
        private async Task SetInitialAmountAsync()
        {
            await _cashRegisterService.InitializeTillAsync(InitialAmount);
            await LoadCashRegisterDataAsync();
        }

        [RelayCommand]
        private async Task PerformDailyReconciliationAsync()
        {
            await _cashRegisterService.PerformDailyReconciliationAsync(ActualAmountForReconciliation);
            await LoadCashRegisterDataAsync();
        }

        // Implement IRecipient interface
        public void Receive(CashRegisterUpdatedMessage message)
        {
            // When a CashRegisterUpdatedMessage is received, reload the data
            LoadCashRegisterDataAsync().FireAndForgetSafeAsync();
        }
    }
}
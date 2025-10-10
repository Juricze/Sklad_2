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
using System.Diagnostics;

namespace Sklad_2.ViewModels
{
    public partial class CashRegisterViewModel : ObservableObject, IRecipient<RoleChangedMessage>
    {
        private readonly ICashRegisterService _cashRegisterService;
        private readonly IAuthService _authService;
        private readonly IMessenger _messenger;

        [ObservableProperty]
        private bool isSalesRole;

        [ObservableProperty]
        private decimal currentCashInTill;

        [ObservableProperty]
        private decimal depositAmount;

        [ObservableProperty]
        private decimal actualAmountForReconciliation;

        [ObservableProperty]
        private ObservableCollection<CashRegisterEntry> cashRegisterHistory;

        public CashRegisterViewModel(ICashRegisterService cashRegisterService, IAuthService authService, IMessenger messenger)
        {
            _cashRegisterService = cashRegisterService;
            _authService = authService;
            _messenger = messenger;
            CashRegisterHistory = new ObservableCollection<CashRegisterEntry>();
            
            // Initial check
            IsSalesRole = _authService.CurrentRole == "Prodej";
            Debug.WriteLine($"CashRegisterViewModel: Initial IsSalesRole = {IsSalesRole} (CurrentRole: {_authService.CurrentRole})");

            // Register for messages
            _messenger.Register<RoleChangedMessage>(this);
        }

        public void Receive(RoleChangedMessage message)
        {
            IsSalesRole = message.Value == "Prodej";
            Debug.WriteLine($"CashRegisterViewModel: Received RoleChangedMessage. New IsSalesRole = {IsSalesRole} (Role: {message.Value})");
        }

        [RelayCommand]
        private async Task LoadCashRegisterDataAsync()
        {
            CurrentCashInTill = await _cashRegisterService.GetCurrentCashInTillAsync();
            // var history = await _cashRegisterService.GetCashRegisterHistoryAsync();
            // CashRegisterHistory.Clear();
            // foreach (var entry in history)
            // {
            //     CashRegisterHistory.Add(entry);
            // }
        }

        [RelayCommand]
        private void MakeDeposit()
        {
            if (DepositAmount <= 0)
            {
                // Could trigger an error message here
                return;
            }

            if (DepositAmount > 10000000) // 10 million limit
            {
                // Could trigger an error message here
                return;
            }

            _messenger.Send(new ShowDepositConfirmationMessage(DepositAmount));
        }

        public async Task ExecuteDepositAsync()
        {
            if (DepositAmount > 0 && DepositAmount <= 10000000)
            {
                await _cashRegisterService.MakeDepositAsync(DepositAmount);
                DepositAmount = 0;
                await LoadCashRegisterDataAsync();
            }
        }

        [RelayCommand]
        private async Task PerformDailyReconciliationAsync()
        {
            // Validate that the amount is not negative and not unreasonably large
            if (ActualAmountForReconciliation < 0)
            {
                // Could trigger an error message
                return;
            }

            if (ActualAmountForReconciliation > 10000000) // 10 million limit
            {
                // Could trigger an error message
                return;
            }

            await _cashRegisterService.PerformDailyReconciliationAsync(ActualAmountForReconciliation);
            await LoadCashRegisterDataAsync();
        }

        // Implement IRecipient interface
        // public void Receive(CashRegisterUpdatedMessage message)
        // {
        //     // When a CashRegisterUpdatedMessage is received, reload the data
        //     LoadCashRegisterDataAsync().FireAndForgetSafeAsync();
        // }
    }
}
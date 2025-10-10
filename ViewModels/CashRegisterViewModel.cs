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

        public event EventHandler<string> DayCloseSucceeded;

        [ObservableProperty]
        private bool isSalesRole;

        [ObservableProperty]
        private decimal currentCashInTill;

        [ObservableProperty]
        private decimal depositAmount;

        [ObservableProperty]
        private decimal actualAmountForReconciliation;

        [ObservableProperty]
        private decimal dayCloseAmount;

        [ObservableProperty]
        private string dayCloseStatusMessage;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsDayCloseError))]
        private bool isDayCloseErrorVisible;

        public bool IsDayCloseError => IsDayCloseErrorVisible;

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

            // Register for messages
            _messenger.Register<RoleChangedMessage>(this);
            _messenger.Register<CashRegisterUpdatedMessage, string>(this, "CashRegisterUpdateToken", async (r, m) =>
            {
                // When a CashRegisterUpdatedMessage is received, reload the data
                await LoadCashRegisterDataAsync();
            });
        }

        public void Receive(RoleChangedMessage message)
        {
            IsSalesRole = message.Value == "Prodej";
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

        [RelayCommand]
        private async Task PerformDayCloseAsync()
        {
            IsDayCloseErrorVisible = false;
            DayCloseStatusMessage = string.Empty;

            var (success, errorMessage) = await _cashRegisterService.PerformDayCloseAsync(DayCloseAmount);

            if (success)
            {
                // Success - trigger event for UI to show dialog
                DayCloseSucceeded?.Invoke(this, $"Den byl úspěšně uzavřen. Stav pokladny: {DayCloseAmount:C}");

                // Clear the input
                DayCloseAmount = 0;
                await LoadCashRegisterDataAsync();
            }
            else
            {
                // Error - show error message inline
                DayCloseStatusMessage = errorMessage;
                IsDayCloseErrorVisible = true;
            }
        }
    }
}
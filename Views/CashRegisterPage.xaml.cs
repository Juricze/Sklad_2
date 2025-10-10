using System;
using Microsoft.UI.Xaml.Controls;
using Sklad_2.ViewModels;
using CommunityToolkit.Mvvm.Messaging;
using Sklad_2.Messages;

namespace Sklad_2.Views
{
    public sealed partial class CashRegisterPage : Page
    {
        public CashRegisterViewModel ViewModel { get; }
        private readonly IMessenger _messenger;

        public CashRegisterPage()
        {
            ViewModel = (CashRegisterViewModel)(App.Current as App).Services.GetService(typeof(CashRegisterViewModel));
            _messenger = (IMessenger)(App.Current as App).Services.GetService(typeof(IMessenger));
            this.InitializeComponent();
            ViewModel.LoadCashRegisterDataCommand.Execute(null);

            _messenger.Register<ShowDepositConfirmationMessage>(this, (r, m) =>
            {
                ShowConfirmationDialog(m.Value);
            });
        }

        private async void ShowConfirmationDialog(decimal amount)
        {
            var dialog = new ContentDialog
            {
                Title = "Potvrzení vkladu",
                Content = $"Opravdu si přejete vložit částku {amount:C} do pokladny?",
                PrimaryButtonText = "Ano, provést vklad",
                CloseButtonText = "Zrušit",
                XamlRoot = this.XamlRoot,
                DefaultButton = ContentDialogButton.Primary
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                await ViewModel.ExecuteDepositAsync();
            }
        }
    }
}
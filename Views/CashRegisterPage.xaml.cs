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

            _messenger.Register<ShowDepositConfirmationMessage>(this, (r, m) =>
            {
                ShowConfirmationDialog(m.Value);
            });

            // Subscribe to day close events
            ViewModel.DayCloseSucceeded += HandleDayCloseSucceeded;

            // Load data when page is loaded (not just in constructor)
            this.Loaded += (s, e) =>
            {
                ViewModel.LoadCashRegisterDataCommand.Execute(null);
            };
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

        private void HandleDayCloseSucceeded(object sender, string message)
        {
            // Use DispatcherQueue to ensure we're on UI thread and delay to avoid dialog conflict
            this.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, async () =>
            {
                // Wait for any existing dialogs to close (WinUI limitation)
                // Longer delay for slower machines
                await System.Threading.Tasks.Task.Delay(800);

                var dialog = new ContentDialog
                {
                    Title = "Uzavírka dne provedena",
                    Content = message,
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };

                try
                {
                    await dialog.ShowAsync();
                }
                catch (System.Runtime.InteropServices.COMException)
                {
                    // Dialog was already open, ignore and try again after delay
                    await System.Threading.Tasks.Task.Delay(300);
                    try
                    {
                        await dialog.ShowAsync();
                    }
                    catch
                    {
                        // Still failed, give up silently
                    }
                }
            });
        }
    }
}
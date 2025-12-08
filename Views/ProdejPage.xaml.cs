using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Sklad_2.ViewModels;
using Sklad_2.Views.Dialogs;
using Sklad_2.Models;
using Sklad_2.Services;
using System;

namespace Sklad_2.Views
{
    public sealed partial class ProdejPage : Page
    {
        public ProdejViewModel ViewModel { get; }

        public ProdejPage()
        {
            // IMPORTANT: ViewModel must be set BEFORE InitializeComponent() for x:Bind to work properly
            ViewModel = (Application.Current as App).Services.GetRequiredService<ProdejViewModel>();

            this.InitializeComponent();

            this.Loaded += (s, e) =>
            {
                if (ViewModel != null)
                {
                    ViewModel.ProductOutOfStock += ViewModel_ProductOutOfStock;
                    ViewModel.CheckoutFailed += ViewModel_CheckoutFailed;
                    ViewModel.ReceiptCancelled += ViewModel_ReceiptCancelled;
                    ViewModel.GiftCardValidationFailed += ViewModel_GiftCardValidationFailed;
                    ViewModel.LoyaltyValidationFailed += ViewModel_LoyaltyValidationFailed;
                }
            };
            this.Unloaded += (s, e) =>
            {
                if (ViewModel != null)
                {
                    ViewModel.ProductOutOfStock -= ViewModel_ProductOutOfStock;
                    ViewModel.CheckoutFailed -= ViewModel_CheckoutFailed;
                    ViewModel.ReceiptCancelled -= ViewModel_ReceiptCancelled;
                    ViewModel.GiftCardValidationFailed -= ViewModel_GiftCardValidationFailed;
                    ViewModel.LoyaltyValidationFailed -= ViewModel_LoyaltyValidationFailed;
                }
            };
        }

        private async void ViewModel_CheckoutFailed(object sender, string errorMessage)
        {
            ContentDialog errorDialog = new ContentDialog
            {
                Title = "Chyba při placení",
                Content = errorMessage,
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await errorDialog.ShowAsync();
        }

        private async void ViewModel_ProductOutOfStock(object sender, Models.Product product)
        {
            var dialog = new OutOfStockDialog(product)
            {
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }

        private async void ViewModel_ReceiptCancelled(object sender, string message)
        {
            ContentDialog successDialog = new ContentDialog
            {
                Title = "Prodej zrušen",
                Content = message,
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await successDialog.ShowAsync();
        }

        private async void ViewModel_GiftCardValidationFailed(object sender, string errorMessage)
        {
            ContentDialog errorDialog = new ContentDialog
            {
                Title = "Chyba při načítání poukazu",
                Content = errorMessage,
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await errorDialog.ShowAsync();
        }

        private async void ViewModel_LoyaltyValidationFailed(object sender, string errorMessage)
        {
            ContentDialog errorDialog = new ContentDialog
            {
                Title = "Věrnostní program",
                Content = errorMessage,
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await errorDialog.ShowAsync();
        }

        private async void CancelLastReceiptButton_Click(object sender, RoutedEventArgs e)
        {
            // Show confirmation dialog
            ContentDialog confirmDialog = new ContentDialog
            {
                Title = "Potvrzení zrušení prodeje",
                Content = $"Opravdu chcete zrušit poslední prodej?\n\n" +
                         $"Účtenka č. {ViewModel.LastCreatedReceipt?.ReceiptId}\n" +
                         $"Částka: {ViewModel.LastCreatedReceipt?.TotalAmountFormatted}\n\n" +
                         $"Produkty budou vráceny do skladu a částka odečtena z pokladny.\n" +
                         $"Tato akce je nevratná.",
                PrimaryButtonText = "Ano, zrušit prodej",
                CloseButtonText = "Ne, ponechat",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            var result = await confirmDialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                await ViewModel.CancelLastReceiptCommand.ExecuteAsync(null);
            }
        }

        private async void EanTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                var textBox = (TextBox)sender;
                var eanCode = textBox.Text;
                textBox.Text = string.Empty;
                e.Handled = true;

                // Disable TextBox during processing to prevent duplicate scans from barcode reader
                textBox.IsEnabled = false;
                try
                {
                    await ViewModel.FindProductCommand.ExecuteAsync(eanCode);
                }
                finally
                {
                    textBox.IsEnabled = true;
                    textBox.Focus(FocusState.Programmatic);
                }
            }
        }

        private async void GiftCardEanTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                e.Handled = true;
                await ViewModel.LoadGiftCardForRedemptionCommand.ExecuteAsync(ViewModel.GiftCardEanInput);
            }
        }

        private async void LoadGiftCardButton_Click(object sender, RoutedEventArgs e)
        {
            await ViewModel.LoadGiftCardForRedemptionCommand.ExecuteAsync(ViewModel.GiftCardEanInput);
        }

        private void RemoveGiftCard_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string ean)
            {
                ViewModel.RemoveGiftCardCommand.Execute(ean);
            }
        }

        // Věrnostní program - event handlery
        private async void LoyaltySearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                await ViewModel.SearchLoyaltyCustomersCommand.ExecuteAsync(sender.Text);
            }
        }

        private void LoyaltySearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            if (args.SelectedItem is Sklad_2.Models.LoyaltyCustomer customer)
            {
                ViewModel.SelectLoyaltyCustomer(customer);
                sender.Text = string.Empty;
            }
        }

        private async void LoyaltySearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            // Pokud je to EAN kartičky, zkusit načíst přímo
            if (!string.IsNullOrWhiteSpace(args.QueryText) && args.ChosenSuggestion == null)
            {
                await ViewModel.LoadLoyaltyCustomerByEanCommand.ExecuteAsync(args.QueryText);
            }
        }

        private async void ClearReceiptButton_Click(object sender, RoutedEventArgs e)
        {
            ContentDialog clearReceiptDialog = new ContentDialog
            {
                Title = "Potvrzení smazání účtenky",
                Content = "Opravdu si přejete smazat celou účtenku? Tato akce je nevratná.",
                CloseButtonText = "Zrušit",
                PrimaryButtonText = "Smazat",
                XamlRoot = this.XamlRoot
            };

            ContentDialogResult result = await clearReceiptDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                ViewModel.ClearReceiptCommand.Execute(null);
            }
        }

        private void IncrementButton_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is Sklad_2.Services.CartItem item)
            {
                ViewModel.IncrementQuantityCommand.Execute(item);
            }
        }

        private void DecrementButton_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is Sklad_2.Services.CartItem item)
            {
                ViewModel.DecrementQuantityCommand.Execute(item);
            }
        }

        private async void CheckoutButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.Receipt.Items.Count == 0)
            {
                ContentDialog emptyReceiptDialog = new ContentDialog
                {
                    Title = "Prázdná účtenka",
                    Content = "Nelze dokončit prodej s prázdnou účtenkou.",
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await emptyReceiptDialog.ShowAsync();
                return;
            }

            // Payment dialog - select payment method
            var paymentSelectionDialog = new PaymentSelectionDialog()
            {
                XamlRoot = this.XamlRoot,
            };
            await paymentSelectionDialog.ShowAsync();

            var parameters = new Dictionary<string, object>();

            switch (paymentSelectionDialog.SelectedPaymentMethod)
            {
                case PaymentMethod.Cash:
                    // If gift cards are loaded and cover full amount, skip cash dialog
                    if (ViewModel.IsAnyGiftCardReady && ViewModel.AmountToPay == 0)
                    {
                        // Show warning about forfeiture if applicable
                        if (ViewModel.WillHavePartialUsage)
                        {
                            ContentDialog forfeitureWarningDialog = new ContentDialog
                            {
                                Title = "Upozornění: Částečné využití",
                                Content = $"Hodnota poukazů ({ViewModel.TotalGiftCardValueFormatted}) je vyšší než celková částka ({ViewModel.Receipt.GrandTotal:C}).\n\n" +
                                         $"Zbývající částka {ViewModel.ForfeitedAmountFormatted} propadne a nelze ji použít v budoucnu.\n\n" +
                                         $"Chcete pokračovat?",
                                PrimaryButtonText = "Ano, pokračovat",
                                CloseButtonText = "Ne, zrušit",
                                DefaultButton = ContentDialogButton.Close,
                                XamlRoot = this.XamlRoot
                            };
                            var forfeitureResult = await forfeitureWarningDialog.ShowAsync();
                            if (forfeitureResult != ContentDialogResult.Primary)
                            {
                                return;
                            }
                        }

                        parameters.Add("paymentMethod", PaymentMethod.Cash);
                        parameters.Add("receivedAmount", 0m);
                        parameters.Add("changeAmount", 0m);
                        await ViewModel.CheckoutCommand.ExecuteAsync(parameters);
                        break;
                    }

                    // Použít zaokrouhlenou částku (celé koruny)
                    decimal amountToPayRounded = ViewModel.AmountToPayRounded;
                    ContentDialogResult cashPaymentResult;
                    decimal receivedAmount = 0m;
                    decimal changeAmount = 0m;

                    var cashPaymentDialog = new CashPaymentDialog(amountToPayRounded) { XamlRoot = this.XamlRoot };
                    cashPaymentResult = await cashPaymentDialog.ShowAsync();

                    if (cashPaymentResult != ContentDialogResult.Primary) break;

                    receivedAmount = cashPaymentDialog.ReceivedAmount;
                    changeAmount = cashPaymentDialog.ChangeAmount;

                    var cashConfirmationDialog = new CashConfirmationDialog(amountToPayRounded, receivedAmount, changeAmount) { XamlRoot = this.XamlRoot };
                    ContentDialogResult confirmationResult = await cashConfirmationDialog.ShowAsync();

                    if (confirmationResult != ContentDialogResult.Primary) break;

                    parameters.Add("paymentMethod", PaymentMethod.Cash);
                    parameters.Add("receivedAmount", receivedAmount);
                    parameters.Add("changeAmount", changeAmount);

                    await ViewModel.CheckoutCommand.ExecuteAsync(parameters);
                    break;

                case PaymentMethod.Card:
                    // Show warning about forfeiture if applicable (gift cards loaded and cover more than total)
                    if (ViewModel.IsAnyGiftCardReady && ViewModel.WillHavePartialUsage)
                    {
                        ContentDialog forfeitureWarningDialog = new ContentDialog
                        {
                            Title = "Upozornění: Částečné využití",
                            Content = $"Hodnota poukazů ({ViewModel.TotalGiftCardValueFormatted}) je vyšší než celková částka ({ViewModel.Receipt.GrandTotal:C}).\n\n" +
                                     $"Zbývající částka {ViewModel.ForfeitedAmountFormatted} propadne a nelze ji použít v budoucnu.\n\n" +
                                     $"Chcete pokračovat?",
                            PrimaryButtonText = "Ano, pokračovat",
                            CloseButtonText = "Ne, zrušit",
                            DefaultButton = ContentDialogButton.Close,
                            XamlRoot = this.XamlRoot
                        };
                        var forfeitureResult = await forfeitureWarningDialog.ShowAsync();
                        if (forfeitureResult != ContentDialogResult.Primary)
                        {
                            return;
                        }
                    }

                    var cardConfirmationDialog = new CardPaymentConfirmationDialog() { XamlRoot = this.XamlRoot };
                    var cardConfirmationResult = await cardConfirmationDialog.ShowAsync();

                    if (cardConfirmationResult != ContentDialogResult.Primary) break;

                    parameters.Add("paymentMethod", PaymentMethod.Card);
                    await ViewModel.CheckoutCommand.ExecuteAsync(parameters);
                    break;

                case PaymentMethod.None:
                    return;
            }

            if (ViewModel.IsCheckoutSuccessful)
            {
                var createdReceipt = ViewModel.LastCreatedReceipt;
                var printService = (Application.Current as App).Services.GetRequiredService<IPrintService>();
                var finalReceiptPreviewDialog = new ReceiptPreviewDialog(createdReceipt, printService)
                {
                    XamlRoot = this.XamlRoot,
                };
                await finalReceiptPreviewDialog.ShowAsync();
                ViewModel.ClearReceiptCommand.Execute(null);
            }
        }
    }
}
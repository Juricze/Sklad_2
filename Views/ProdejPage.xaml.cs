using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Sklad_2.ViewModels;
using Sklad_2.Views.Dialogs;
using Sklad_2.Models;
using System;

namespace Sklad_2.Views
{
    public sealed partial class ProdejPage : Page
    {
        public ProdejViewModel ViewModel { get; set; }

        public ProdejPage()
        {
            this.InitializeComponent();
            this.Loaded += (s, e) =>
            {
                if (ViewModel != null)
                {
                    ViewModel.ProductOutOfStock += ViewModel_ProductOutOfStock;
                    ViewModel.CheckoutFailed += ViewModel_CheckoutFailed;
                }
            };
            this.Unloaded += (s, e) =>
            {
                if (ViewModel != null)
                {
                    ViewModel.ProductOutOfStock -= ViewModel_ProductOutOfStock;
                    ViewModel.CheckoutFailed -= ViewModel_CheckoutFailed;
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

        private void EanTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                var textBox = (TextBox)sender;
                ViewModel.FindProductCommand.Execute(textBox.Text);
                textBox.Text = string.Empty;
                e.Handled = true;
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
            if ((sender as FrameworkElement)?.DataContext is Sklad_2.Services.ReceiptItem item)
            {
                ViewModel.IncrementQuantityCommand.Execute(item);
            }
        }

        private void DecrementButton_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is Sklad_2.Services.ReceiptItem item)
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

            var paymentSelectionDialog = new PaymentSelectionDialog(ViewModel.Receipt.GrandTotal)
            {
                XamlRoot = this.XamlRoot,
            };
            await paymentSelectionDialog.ShowAsync();

            // Zpracování zvolené platební metody
            switch (paymentSelectionDialog.SelectedPaymentMethod)
            {
                case PaymentMethod.Cash:
                    decimal grandTotal = ViewModel.Receipt.GrandTotal;
                    ContentDialogResult cashPaymentResult;
                    decimal receivedAmount = 0m;
                    decimal changeAmount = 0m;

                    do
                    {
                        var cashPaymentDialog = new CashPaymentDialog(grandTotal)
                        {
                            XamlRoot = this.XamlRoot
                        };
                        cashPaymentResult = await cashPaymentDialog.ShowAsync();

                        if (cashPaymentResult == ContentDialogResult.Primary)
                        {
                            receivedAmount = cashPaymentDialog.ReceivedAmount;
                            changeAmount = cashPaymentDialog.ChangeAmount;

                            var cashConfirmationDialog = new CashConfirmationDialog(grandTotal, receivedAmount, changeAmount)
                            {
                                XamlRoot = this.XamlRoot
                            };
                            ContentDialogResult confirmationResult = await cashConfirmationDialog.ShowAsync();

                            if (confirmationResult == ContentDialogResult.Primary) // User confirmed sale
                            {
                                // Proceed with checkout
                                await ViewModel.CheckoutCommand.ExecuteAsync(null);

                                if (ViewModel.IsCheckoutSuccessful)
                                {
                                    var createdReceipt = ViewModel.LastCreatedReceipt;
                                    var finalReceiptPreviewDialog = new ReceiptPreviewDialog(createdReceipt, receivedAmount, changeAmount)
                                    {
                                        XamlRoot = this.XamlRoot,
                                    };
                                    await finalReceiptPreviewDialog.ShowAsync();
                                    ViewModel.ClearReceiptCommand.Execute(null);
                                }
                                cashPaymentResult = ContentDialogResult.Primary; // Exit loop
                            }
                            else // User clicked "Zpět" (CloseButton) in confirmation dialog
                            {
                                cashPaymentResult = ContentDialogResult.None; // Re-show cash payment dialog
                            }
                        }
                        else // User cancelled cash payment dialog
                        {
                            // If the cash payment dialog was cancelled, we want to go back to the payment selection dialog.
                            // We set cashPaymentResult to Primary to exit the do-while loop, but the outer switch will then handle it as a cancellation.
                            cashPaymentResult = ContentDialogResult.Primary; 
                        }
                    } while (cashPaymentResult == ContentDialogResult.None); // Loop if user went "Zpět" from confirmation

                    // If cashPaymentResult is not Primary (meaning it was cancelled), we break out of the switch to re-show PaymentSelectionDialog.
                    if (cashPaymentResult != ContentDialogResult.Primary)
                    {
                        break; // Exit the switch to re-show PaymentSelectionDialog
                    }

                    break;
                case PaymentMethod.Card:
                    // Provedení CheckoutCommand pro platbu kartou
                    await ViewModel.CheckoutCommand.ExecuteAsync(null);

                    if (ViewModel.IsCheckoutSuccessful)
                    {
                        var createdReceipt = ViewModel.LastCreatedReceipt;
                        var finalReceiptPreviewDialog = new ReceiptPreviewDialog(createdReceipt)
                        {
                            XamlRoot = this.XamlRoot,
                        };
                        await finalReceiptPreviewDialog.ShowAsync();
                        ViewModel.ClearReceiptCommand.Execute(null);
                    }
                    break;
                case PaymentMethod.Print:
                    // Zde by se v budoucnu mohla přidat logika pro tisk
                    // Prozatím jen vyčistíme účtenku
                    ViewModel.ClearReceiptCommand.Execute(null);
                    break;
                case PaymentMethod.None:
                    // Uživatel zavřel dialog bez výběru platby
                    break;
            }
        }
    }
}
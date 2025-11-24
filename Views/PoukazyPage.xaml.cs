using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Sklad_2.Models;
using Sklad_2.ViewModels;
using System;

namespace Sklad_2.Views
{
    public sealed partial class PoukazyPage : Page
    {
        public PoukazyViewModel ViewModel { get; }

        public PoukazyPage()
        {
            ViewModel = (Application.Current as App).Services.GetRequiredService<PoukazyViewModel>();
            this.InitializeComponent();
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            // Load data - filters are already bound to checkboxes
            await ViewModel.LoadGiftCardsCommand.ExecuteAsync(null);
        }


        private async void NewGiftCardEan_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                e.Handled = true;

                // Validate that we have a value set
                if (string.IsNullOrWhiteSpace(ViewModel.NewGiftCardValue))
                {
                    ContentDialog errorDialog = new ContentDialog
                    {
                        Title = "Chyba",
                        Content = "Nejprve nastavte hodnotu poukazu (krok 1).",
                        CloseButtonText = "OK",
                        XamlRoot = this.XamlRoot
                    };
                    await errorDialog.ShowAsync();
                    ValueTextBox.Focus(FocusState.Programmatic);
                    return;
                }

                // Validate EAN is not empty
                if (string.IsNullOrWhiteSpace(ViewModel.NewGiftCardEan))
                {
                    return; // Silent - just wait for next scan
                }

                await ViewModel.AddGiftCardCommand.ExecuteAsync(null);

                // Check if there was an error
                if (!string.IsNullOrWhiteSpace(ViewModel.LastErrorMessage))
                {
                    ContentDialog errorDialog = new ContentDialog
                    {
                        Title = "Chyba",
                        Content = ViewModel.LastErrorMessage,
                        CloseButtonText = "OK",
                        XamlRoot = this.XamlRoot
                    };
                    await errorDialog.ShowAsync();
                }

                // Always return focus to EAN field for next scan
                EanTextBox.Focus(FocusState.Programmatic);
            }
        }

        private async void CancelGiftCardButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is GiftCard giftCard)
            {
                ContentDialog confirmDialog = new ContentDialog
                {
                    Title = "Potvrzení zrušení",
                    Content = $"Opravdu chcete zrušit dárkový poukaz?\n\n" +
                             $"EAN: {giftCard.Ean}\n" +
                             $"Hodnota: {giftCard.ValueFormatted}\n\n" +
                             $"Tato akce je nevratná.",
                    PrimaryButtonText = "Ano, zrušit",
                    CloseButtonText = "Ne",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = this.XamlRoot
                };

                var result = await confirmDialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    await ViewModel.MarkAsCancelledCommand.ExecuteAsync(giftCard);

                    ContentDialog successDialog = new ContentDialog
                    {
                        Title = "Poukaz zrušen",
                        Content = "Dárkový poukaz byl označen jako zrušený.",
                        CloseButtonText = "OK",
                        XamlRoot = this.XamlRoot
                    };
                    await successDialog.ShowAsync();
                }
            }
        }

        public DateTimeOffset GetTomorrow()
        {
            return DateTimeOffset.Now.AddDays(1);
        }

        private void SortComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Apply sorting based on selected index
            if (ViewModel != null && sender is ComboBox comboBox)
            {
                ViewModel.ApplySorting(comboBox.SelectedIndex);
            }
        }
    }
}

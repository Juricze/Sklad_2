using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Sklad_2.Models;
using Sklad_2.ViewModels;
using System;

namespace Sklad_2.Views
{
    public sealed partial class LoyaltyPage : Page
    {
        public LoyaltyViewModel ViewModel { get; }

        public LoyaltyPage()
        {
            ViewModel = (Application.Current as App).Services.GetRequiredService<LoyaltyViewModel>();
            this.InitializeComponent();

            this.Loaded += async (s, e) =>
            {
                await ViewModel.LoadCustomersAsync();
            };
        }

        private async void EditButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is LoyaltyCustomer customer)
            {
                // Dialog pro editaci
                var firstNameBox = new TextBox { Text = customer.FirstName, PlaceholderText = "Jméno", Margin = new Thickness(0, 0, 0, 10) };
                var lastNameBox = new TextBox { Text = customer.LastName, PlaceholderText = "Příjmení", Margin = new Thickness(0, 0, 0, 10) };
                var emailBox = new TextBox { Text = customer.Email, PlaceholderText = "Email", Margin = new Thickness(0, 0, 0, 10) };
                var cardEanBox = new TextBox { Text = customer.CardEan ?? "", PlaceholderText = "EAN kartičky", Margin = new Thickness(0, 0, 0, 10) };
                var discountBox = new TextBox { Text = customer.DiscountPercent.ToString("0"), PlaceholderText = "Sleva 0-30%", Margin = new Thickness(0, 0, 0, 10) };

                var panel = new StackPanel();
                panel.Children.Add(new TextBlock { Text = "Jméno:", Margin = new Thickness(0, 0, 0, 5) });
                panel.Children.Add(firstNameBox);
                panel.Children.Add(new TextBlock { Text = "Příjmení:", Margin = new Thickness(0, 0, 0, 5) });
                panel.Children.Add(lastNameBox);
                panel.Children.Add(new TextBlock { Text = "Email:", Margin = new Thickness(0, 0, 0, 5) });
                panel.Children.Add(emailBox);
                panel.Children.Add(new TextBlock { Text = "EAN kartičky:", Margin = new Thickness(0, 0, 0, 5) });
                panel.Children.Add(cardEanBox);

                // Sleva - pouze pro admina
                if (ViewModel.IsAdmin)
                {
                    panel.Children.Add(new TextBlock { Text = "Sleva (%):", Margin = new Thickness(0, 0, 0, 5) });
                    panel.Children.Add(discountBox);
                }

                var dialog = new ContentDialog
                {
                    Title = $"Upravit člena: {customer.FullName}",
                    Content = panel,
                    PrimaryButtonText = "Uložit",
                    CloseButtonText = "Zrušit",
                    XamlRoot = this.XamlRoot
                };

                var result = await dialog.ShowAsync();

                if (result == ContentDialogResult.Primary)
                {
                    // Validace
                    if (string.IsNullOrWhiteSpace(firstNameBox.Text) ||
                        string.IsNullOrWhiteSpace(lastNameBox.Text) ||
                        string.IsNullOrWhiteSpace(emailBox.Text))
                    {
                        var errorDialog = new ContentDialog
                        {
                            Title = "Chyba",
                            Content = "Jméno, příjmení a email jsou povinné.",
                            CloseButtonText = "OK",
                            XamlRoot = this.XamlRoot
                        };
                        await errorDialog.ShowAsync();
                        return;
                    }

                    // Validace slevy (pouze admin)
                    decimal discountPercent = customer.DiscountPercent; // Zachovat původní pokud není admin
                    if (ViewModel.IsAdmin)
                    {
                        if (!decimal.TryParse(discountBox.Text, out discountPercent) || discountPercent < 0 || discountPercent > 30)
                        {
                            var errorDialog = new ContentDialog
                            {
                                Title = "Chyba",
                                Content = "Sleva musí být číslo mezi 0 a 30.",
                                CloseButtonText = "OK",
                                XamlRoot = this.XamlRoot
                            };
                            await errorDialog.ShowAsync();
                            return;
                        }
                    }

                    // Aktualizovat
                    var updatedCustomer = new LoyaltyCustomer
                    {
                        Id = customer.Id,
                        FirstName = firstNameBox.Text.Trim(),
                        LastName = lastNameBox.Text.Trim(),
                        Email = emailBox.Text.Trim(),
                        CardEan = string.IsNullOrWhiteSpace(cardEanBox.Text) ? string.Empty : cardEanBox.Text.Trim(),
                        DiscountPercent = discountPercent
                    };

                    await ViewModel.UpdateCustomerCommand.ExecuteAsync(updatedCustomer);
                }
            }
        }

        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ViewModel.IsAdmin)
            {
                var errorDialog = new ContentDialog
                {
                    Title = "Přístup odepřen",
                    Content = "Pouze admin může mazat členy věrnostního programu.",
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await errorDialog.ShowAsync();
                return;
            }

            if (sender is Button button && button.DataContext is LoyaltyCustomer customer)
            {
                var confirmDialog = new ContentDialog
                {
                    Title = "Smazat člena",
                    Content = $"Opravdu chcete smazat člena {customer.FullName}?\n\nTato akce je nevratná.",
                    PrimaryButtonText = "Smazat",
                    CloseButtonText = "Zrušit",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = this.XamlRoot
                };

                var result = await confirmDialog.ShowAsync();

                if (result == ContentDialogResult.Primary)
                {
                    ViewModel.SelectedCustomer = customer;
                    await ViewModel.DeleteCustomerCommand.ExecuteAsync(null);
                }
            }
        }
    }
}

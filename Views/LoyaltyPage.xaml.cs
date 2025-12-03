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

                // Telefon - odstranit +420 prefix pro zobrazení
                var phoneDisplay = customer.PhoneNumber ?? "";
                if (phoneDisplay.StartsWith("+420"))
                {
                    phoneDisplay = phoneDisplay.Substring(4); // Odstranit "+420"
                }
                var phoneNumberBox = new TextBox { Text = phoneDisplay, PlaceholderText = "Tel. číslo", Margin = new Thickness(0, 0, 0, 10) };
                var cardEanBox = new TextBox { Text = customer.CardEan ?? "", PlaceholderText = "EAN kartičky", Margin = new Thickness(0, 0, 0, 10) };
                var discountBox = new TextBox { Text = customer.DiscountPercent.ToString("0"), PlaceholderText = "Sleva 0-30%", Margin = new Thickness(0, 0, 0, 10) };

                var panel = new StackPanel();
                panel.Children.Add(new TextBlock { Text = "Jméno:", Margin = new Thickness(0, 0, 0, 5) });
                panel.Children.Add(firstNameBox);
                panel.Children.Add(new TextBlock { Text = "Příjmení:", Margin = new Thickness(0, 0, 0, 5) });
                panel.Children.Add(lastNameBox);
                panel.Children.Add(new TextBlock { Text = "Email:", Margin = new Thickness(0, 0, 0, 5) });
                panel.Children.Add(emailBox);
                panel.Children.Add(new TextBlock { Text = "Tel. číslo:", Margin = new Thickness(0, 0, 0, 5) });

                // Telefon s viditelným +420 prefixem
                var phonePanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 3,
                    Margin = new Thickness(0, 0, 0, 10)
                };
                var phonePrefix = new TextBlock
                {
                    Text = "+420",
                    VerticalAlignment = VerticalAlignment.Center,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 102, 102, 102)) // #666
                };
                phonePanel.Children.Add(phonePrefix);
                phonePanel.Children.Add(phoneNumberBox);
                panel.Children.Add(phonePanel);
                panel.Children.Add(new TextBlock { Text = "EAN kartičky:", Margin = new Thickness(0, 0, 0, 5) });
                panel.Children.Add(cardEanBox);

                // Hint pro prodavačku
                var hintPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 5,
                    Margin = new Thickness(0, 10, 0, 10)
                };
                var icon = new FontIcon
                {
                    Glyph = "\uE946", // Info icon
                    FontSize = 12,
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Orange)
                };
                var hintText = new TextBlock
                {
                    Text = "Vyplňte alespoň email nebo telefon",
                    FontSize = 12,
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Orange),
                    FontStyle = Windows.UI.Text.FontStyle.Italic
                };
                hintPanel.Children.Add(icon);
                hintPanel.Children.Add(hintText);
                panel.Children.Add(hintPanel);

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
                    // Validace jména a příjmení
                    if (string.IsNullOrWhiteSpace(firstNameBox.Text) ||
                        string.IsNullOrWhiteSpace(lastNameBox.Text))
                    {
                        var errorDialog = new ContentDialog
                        {
                            Title = "Chyba",
                            Content = "Jméno a příjmení jsou povinné.",
                            CloseButtonText = "OK",
                            XamlRoot = this.XamlRoot
                        };
                        await errorDialog.ShowAsync();
                        return;
                    }

                    // Validace: alespoň email NEBO telefon
                    if (string.IsNullOrWhiteSpace(emailBox.Text) &&
                        string.IsNullOrWhiteSpace(phoneNumberBox.Text))
                    {
                        var errorDialog = new ContentDialog
                        {
                            Title = "Chyba",
                            Content = "Musí být vyplněn alespoň email nebo telefon.",
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

                    // Přidat +420 prefix k telefonu pokud není prázdný
                    var phoneNumber = string.Empty;
                    if (!string.IsNullOrWhiteSpace(phoneNumberBox.Text))
                    {
                        var phone = phoneNumberBox.Text.Trim();
                        // Přidat +420 pokud tam ještě není
                        phoneNumber = phone.StartsWith("+420") ? phone : $"+420{phone}";
                    }

                    // Aktualizovat
                    var updatedCustomer = new LoyaltyCustomer
                    {
                        Id = customer.Id,
                        FirstName = firstNameBox.Text.Trim(),
                        LastName = lastNameBox.Text.Trim(),
                        Email = string.IsNullOrWhiteSpace(emailBox.Text) ? string.Empty : emailBox.Text.Trim(),
                        PhoneNumber = phoneNumber,
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

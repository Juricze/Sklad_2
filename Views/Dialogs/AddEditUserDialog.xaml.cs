using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Sklad_2.Models;

namespace Sklad_2.Views.Dialogs
{
    public sealed partial class AddEditUserDialog : ContentDialog
    {
        public string Username => UsernameTextBox.Text;
        public string DisplayName => DisplayNameTextBox.Text;
        public string Password { get; private set; }
        public string Role => (RoleComboBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "Cashier";

        private bool _isEditMode;

        public AddEditUserDialog()
        {
            this.InitializeComponent();
            _isEditMode = false;
            Title = "Přidat uživatele";
        }

        public void SetEditMode(User user)
        {
            _isEditMode = true;
            Title = "Upravit uživatele";

            UsernameTextBox.Text = user.Username;
            DisplayNameTextBox.Text = user.DisplayName;

            // Select role
            foreach (ComboBoxItem item in RoleComboBox.Items)
            {
                if (item.Tag as string == user.Role)
                {
                    RoleComboBox.SelectedItem = item;
                    break;
                }
            }

            // Show hint for password
            PasswordHintTextBlock.Visibility = Visibility.Visible;
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ErrorTextBlock.Visibility = Visibility.Collapsed;
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            Password = PasswordBox.Password;
            ErrorTextBlock.Visibility = Visibility.Collapsed;
        }

        private void PrimaryButton_Click(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(UsernameTextBox.Text))
            {
                ErrorTextBlock.Text = "Uživatelské jméno nesmí být prázdné.";
                ErrorTextBlock.Visibility = Visibility.Visible;
                args.Cancel = true;
                return;
            }

            if (string.IsNullOrWhiteSpace(DisplayNameTextBox.Text))
            {
                ErrorTextBlock.Text = "Zobrazované jméno nesmí být prázdné.";
                ErrorTextBlock.Visibility = Visibility.Visible;
                args.Cancel = true;
                return;
            }

            // Password is required for new users, optional for edit
            if (!_isEditMode && string.IsNullOrWhiteSpace(Password))
            {
                ErrorTextBlock.Text = "Heslo nesmí být prázdné.";
                ErrorTextBlock.Visibility = Visibility.Visible;
                args.Cancel = true;
                return;
            }

            if (RoleComboBox.SelectedItem == null)
            {
                ErrorTextBlock.Text = "Vyberte roli uživatele.";
                ErrorTextBlock.Visibility = Visibility.Visible;
                args.Cancel = true;
                return;
            }
        }
    }
}

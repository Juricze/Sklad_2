using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Sklad_2.Views.Dialogs
{
    public sealed partial class CreateFirstAdminDialog : ContentDialog
    {
        public string Username => UsernameTextBox.Text;
        public string DisplayName => DisplayNameTextBox.Text;
        public string Password { get; private set; }

        public CreateFirstAdminDialog()
        {
            this.InitializeComponent();
        }

        public void SetDefaults(string defaultUsername, string defaultDisplayName)
        {
            UsernameTextBox.Text = defaultUsername;
            DisplayNameTextBox.Text = defaultDisplayName;
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

            if (string.IsNullOrWhiteSpace(Password))
            {
                ErrorTextBlock.Text = "Heslo nesmí být prázdné.";
                ErrorTextBlock.Visibility = Visibility.Visible;
                args.Cancel = true;
                return;
            }
        }
    }
}

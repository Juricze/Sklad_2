using Microsoft.UI.Xaml.Controls;

namespace Sklad_2.Views.Dialogs
{
    public sealed partial class PasswordPromptDialog : ContentDialog
    {
        public string Password => PasswordInputBox.Password;

        public PasswordPromptDialog()
        {
            this.InitializeComponent();
        }

        public void SetPromptText(string text)
        {
            PromptText.Text = text;
        }

        public void SetErrorText(string text)
        {
            ErrorText.Text = text;
        }
    }
}

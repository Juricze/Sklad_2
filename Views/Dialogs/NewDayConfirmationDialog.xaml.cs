using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Sklad_2.Views.Dialogs
{
    public sealed partial class NewDayConfirmationDialog : ContentDialog
    {
        public decimal InitialAmount { get; private set; }

        public NewDayConfirmationDialog()
        {
            this.InitializeComponent();
        }

        public void SetPromptText(string text)
        {
            PromptTextBlock.Text = text;
        }

        private void InitialAmountTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (decimal.TryParse(InitialAmountTextBox.Text, out decimal amount))
            {
                InitialAmount = amount;
                IsPrimaryButtonEnabled = true;
                ErrorTextBlock.Visibility = Visibility.Collapsed;
            }
            else
            {
                IsPrimaryButtonEnabled = false;
                ErrorTextBlock.Text = "Zadejte platnou částku.";
                ErrorTextBlock.Visibility = Visibility.Visible;
            }
        }
    }
}
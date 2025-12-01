using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Sklad_2.Models;

namespace Sklad_2.Views.Dialogs
{
    public sealed partial class WriteOffDialog : ContentDialog
    {
        public StockMovementType SelectedWriteOffType
        {
            get
            {
                var tag = (WriteOffTypeComboBox.SelectedItem as ComboBoxItem)?.Tag as string;
                return tag == "WriteOffTester" ? StockMovementType.WriteOffTester : StockMovementType.WriteOffDamaged;
            }
        }

        public int Quantity => (int)QuantityNumberBox.Value;
        public string Notes => NotesTextBox.Text;

        private int _availableStock;

        public WriteOffDialog()
        {
            this.InitializeComponent();
        }

        public void SetProduct(string productName, string productEan, int stockQuantity)
        {
            ProductNameRun.Text = productName;
            ProductEanRun.Text = productEan;
            StockQuantityRun.Text = stockQuantity.ToString();
            _availableStock = stockQuantity;

            // Set NumberBox maximum to available stock
            QuantityNumberBox.Maximum = stockQuantity;
        }

        private void QuantityNumberBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            // Null check - event může být volán před InitializeComponent (WinUI bug)
            if (ErrorTextBlock != null)
            {
                ErrorTextBlock.Visibility = Visibility.Collapsed;
            }
        }

        private void PrimaryButton_Click(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // Validate quantity
            if (QuantityNumberBox.Value < 1)
            {
                ErrorTextBlock.Text = "Počet kusů musí být alespoň 1.";
                ErrorTextBlock.Visibility = Visibility.Visible;
                args.Cancel = true;
                return;
            }

            if (QuantityNumberBox.Value > _availableStock)
            {
                ErrorTextBlock.Text = $"Nelze odepsat více než {_availableStock} ks (dostupné množství na skladě).";
                ErrorTextBlock.Visibility = Visibility.Visible;
                args.Cancel = true;
                return;
            }

            if (WriteOffTypeComboBox.SelectedItem == null)
            {
                ErrorTextBlock.Text = "Vyberte typ odpisu.";
                ErrorTextBlock.Visibility = Visibility.Visible;
                args.Cancel = true;
                return;
            }
        }
    }
}

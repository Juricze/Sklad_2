using Microsoft.UI.Xaml.Controls;

namespace Sklad_2.Views.Dialogs
{
    public sealed partial class ManualDiscountDialog : ContentDialog
    {
        public decimal DiscountPercent => (decimal)(DiscountPercentBox?.Value ?? 0);
        public string DiscountReason => (DiscountReasonCombo?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Manuální sleva";

        public ManualDiscountDialog()
        {
            this.InitializeComponent();
        }
    }
}
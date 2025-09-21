using Microsoft.UI.Xaml.Controls;
using Sklad_2.Models;

namespace Sklad_2.Views.Dialogs
{
    public sealed partial class OutOfStockDialog : ContentDialog
    {
        public OutOfStockDialog(Product product)
        {
            this.InitializeComponent();
            MessageTextBlock.Text = $"Název: {product.Name}\nEAN: {product.Ean}\nSkladem: {product.StockQuantity} ks";
        }
    }
}

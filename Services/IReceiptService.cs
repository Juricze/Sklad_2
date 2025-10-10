using Sklad_2.Models;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Sklad_2.Services
{
    public interface IReceiptService
    {
        ObservableCollection<CartItem> Items { get; }
        void AddProduct(Models.Product product);
        void RemoveItem(CartItem item);
        void Clear();
        decimal GrandTotal { get; }
        decimal GrandTotalWithoutVat { get; }
        decimal GrandTotalVatAmount { get; }

        // New properties for last receipt
        List<CartItem> LastReceiptItems { get; }
        decimal LastReceiptGrandTotal { get; }

        void FinalizeCurrentReceipt(); // New method to copy current to last
    }
}
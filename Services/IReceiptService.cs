using Sklad_2.Models;
using System.Collections.Generic; 
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace Sklad_2.Services
{
    public interface IReceiptService
    {
        ObservableCollection<ReceiptItem> Items { get; }
        void AddProduct(Models.Product product);
        void RemoveItem(ReceiptItem item);
        void Clear();
        decimal GrandTotal { get; }

        // New properties for last receipt
        List<ReceiptItem> LastReceiptItems { get; }
        decimal LastReceiptGrandTotal { get; }

        void FinalizeCurrentReceipt(); // New method to copy current to last
    }
}
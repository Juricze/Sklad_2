using Sklad_2.Services;
using System.Collections.ObjectModel;

namespace Sklad_2.Services
{
    public interface IReceiptService
    {
        ObservableCollection<ReceiptItem> Items { get; }
        void AddProduct(Models.Product product);
        void RemoveItem(ReceiptItem item);
        void Clear();
        decimal GrandTotal { get; }
    }
}
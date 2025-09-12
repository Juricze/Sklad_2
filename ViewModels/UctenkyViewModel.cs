using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sklad_2.Models;
using Sklad_2.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Sklad_2.ViewModels
{
    public partial class UctenkyViewModel : ObservableObject
    {
        private readonly IDataService _dataService;

        public ObservableCollection<Receipt> Receipts { get; } = new ObservableCollection<Receipt>();

        [ObservableProperty]
        private Receipt selectedReceipt;

        public UctenkyViewModel(IDataService dataService)
        {
            _dataService = dataService;
        }

        [RelayCommand]
        private async Task LoadReceiptsAsync()
        {
            Receipts.Clear();
            var allReceipts = await _dataService.GetReceiptsAsync(); 
            foreach (var receipt in allReceipts.OrderByDescending(r => r.SaleDate))
            {
                Receipts.Add(receipt);
            }
        }
    }
}
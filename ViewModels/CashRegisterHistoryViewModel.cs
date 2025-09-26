using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sklad_2.Models;
using Sklad_2.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Sklad_2.ViewModels
{
    public partial class CashRegisterHistoryViewModel : ObservableObject
    {
        private readonly IDataService _dataService;

        public ObservableCollection<CashRegisterEntryViewModel> Entries { get; } = new ObservableCollection<CashRegisterEntryViewModel>();

        public CashRegisterHistoryViewModel(IDataService dataService)
        {
            _dataService = dataService;
        }

        [RelayCommand]
        private async Task LoadHistoryAsync()
        {
            Entries.Clear();
            var allEntries = await _dataService.GetCashRegisterEntriesAsync(); 
            foreach (var entry in allEntries.OrderByDescending(e => e.Timestamp))
            {
                Entries.Add(new CashRegisterEntryViewModel(entry));
            }
        }
    }
}
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sklad_2.Models;
using Sklad_2.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Sklad_2.ViewModels
{
    public partial class VratkyPrehledViewModel : ObservableObject
    {
        private readonly IDataService _dataService;

        public ObservableCollection<Return> Returns { get; } = new ObservableCollection<Return>();

        [ObservableProperty]
        private Return selectedReturn;

        public VratkyPrehledViewModel(IDataService dataService)
        {
            _dataService = dataService;
        }

        [RelayCommand]
        private async Task LoadReturnsAsync()
        {
            Returns.Clear();
            var allReturns = await _dataService.GetReturnsAsync(); 
            foreach (var returnDoc in allReturns.OrderByDescending(r => r.ReturnDate))
            {
                Returns.Add(returnDoc);
            }
        }
    }
}

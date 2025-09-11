using CommunityToolkit.Mvvm.ComponentModel;
using Sklad_2.Services;

namespace Sklad_2.ViewModels
{
    public partial class DatabazeViewModel : ObservableObject
    {
        private readonly IDataService _dataService;
        public DatabazeViewModel(IDataService dataService)
        {
            _dataService = dataService;
        }
    }
}

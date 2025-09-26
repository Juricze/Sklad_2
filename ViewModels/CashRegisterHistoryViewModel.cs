using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sklad_2.Models;
using Sklad_2.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Sklad_2.ViewModels
{
    public partial class CashRegisterHistoryViewModel : ObservableObject
    {
        private readonly IDataService _dataService;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(LoadHistoryCommand))]
        private bool isLoading;

        public ObservableCollection<CashRegisterEntryViewModel> Entries { get; } = new ObservableCollection<CashRegisterEntryViewModel>();
        public List<DateFilterType> FilterOptions { get; } = Enum.GetValues(typeof(DateFilterType)).Cast<DateFilterType>().ToList();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsCustomFilterVisible))]
        private DateFilterType selectedFilterType = DateFilterType.Daily;

        [ObservableProperty]
        private DateTimeOffset filterStartDate = DateTimeOffset.Now;

        [ObservableProperty]
        private DateTimeOffset filterEndDate = DateTimeOffset.Now;

        public bool IsCustomFilterVisible => SelectedFilterType == DateFilterType.Custom;

        public CashRegisterHistoryViewModel(IDataService dataService)
        {
            _dataService = dataService;
        }

        partial void OnSelectedFilterTypeChanged(DateFilterType value)
        {
            if (IsLoading) return;
            LoadHistoryCommand.Execute(null);
        }

        partial void OnFilterStartDateChanged(DateTimeOffset value)
        {
            if (IsLoading) return;
            if (SelectedFilterType == DateFilterType.Custom)
            {
                LoadHistoryCommand.Execute(null);
            }
        }

        partial void OnFilterEndDateChanged(DateTimeOffset value)
        {
            if (IsLoading) return;
            if (SelectedFilterType == DateFilterType.Custom)
            {
                LoadHistoryCommand.Execute(null);
            }
        }

        private bool CanLoad() => !IsLoading;

        [RelayCommand(CanExecute = nameof(CanLoad))]
        private async Task LoadHistoryAsync()
        {
            IsLoading = true;
            try
            {
                DateTime startDate;
                DateTime endDate;

                switch (SelectedFilterType)
                {
                    case DateFilterType.Daily:
                        startDate = DateTime.Today;
                        endDate = DateTime.Today.AddDays(1).AddTicks(-1);
                        break;
                    case DateFilterType.Weekly:
                        startDate = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);
                        endDate = startDate.AddDays(7).AddTicks(-1);
                        break;
                    case DateFilterType.Monthly:
                        startDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                        endDate = startDate.AddMonths(1).AddTicks(-1);
                        break;
                    case DateFilterType.Custom:
                        startDate = FilterStartDate.Date;
                        endDate = FilterEndDate.Date.AddDays(1).AddTicks(-1);
                        break;
                    default:
                        return;
                }

                Entries.Clear();
                var filteredEntries = await _dataService.GetCashRegisterEntriesAsync(startDate, endDate);
                foreach (var entry in filteredEntries.OrderByDescending(e => e.Timestamp))
                {
                    Entries.Add(new CashRegisterEntryViewModel(entry));
                }
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}
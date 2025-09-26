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
    public enum DateFilterType
    {
        Daily,
        Weekly,
        Monthly,
        Custom
    }

    public partial class UctenkyViewModel : ObservableObject
    {
        private readonly IDataService _dataService;

        public ObservableCollection<Receipt> Receipts { get; } = new ObservableCollection<Receipt>();
        public List<DateFilterType> FilterOptions { get; } = Enum.GetValues(typeof(DateFilterType)).Cast<DateFilterType>().ToList();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsReceiptSelected))]
        private Receipt selectedReceipt;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsCustomFilterVisible))]
        [NotifyCanExecuteChangedFor(nameof(LoadReceiptsCommand))]
        private DateFilterType selectedFilterType = DateFilterType.Daily;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(LoadReceiptsCommand))]
        private DateTimeOffset filterStartDate = DateTimeOffset.Now;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(LoadReceiptsCommand))]
        private DateTimeOffset filterEndDate = DateTimeOffset.Now;

        public bool IsReceiptSelected => SelectedReceipt != null;
        public bool IsCustomFilterVisible => SelectedFilterType == DateFilterType.Custom;

        public UctenkyViewModel(IDataService dataService)
        {
            _dataService = dataService;
        }

        partial void OnSelectedFilterTypeChanged(DateFilterType value)
        {
            LoadReceiptsCommand.Execute(null);
        }

        partial void OnFilterStartDateChanged(DateTimeOffset value)
        {
            if (SelectedFilterType == DateFilterType.Custom)
            {
                LoadReceiptsCommand.Execute(null);
            }
        }

        partial void OnFilterEndDateChanged(DateTimeOffset value)
        {
            if (SelectedFilterType == DateFilterType.Custom)
            {
                LoadReceiptsCommand.Execute(null);
            }
        }

        [RelayCommand]
        private async Task LoadReceiptsAsync()
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

            Receipts.Clear();
            var filteredReceipts = await _dataService.GetReceiptsAsync(startDate, endDate);
            foreach (var receipt in filteredReceipts.OrderByDescending(r => r.SaleDate))
            {
                Receipts.Add(receipt);
            }
        }
    }
}
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
    public partial class UctenkyViewModel : ObservableObject
    {
        private readonly IDataService _dataService;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(LoadReceiptsCommand))]
        private bool isLoading;

        public ObservableCollection<Receipt> Receipts { get; } = new ObservableCollection<Receipt>();
        public List<DateFilterType> FilterOptions { get; } = Enum.GetValues(typeof(DateFilterType)).Cast<DateFilterType>().ToList();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsReceiptSelected))]
        [NotifyPropertyChangedFor(nameof(AmountToPay))]
        [NotifyPropertyChangedFor(nameof(AmountToPayFormatted))]
        private Receipt selectedReceipt;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsCustomFilterVisible))]
        private DateFilterType selectedFilterType = DateFilterType.Daily;

        [ObservableProperty]
        private DateTimeOffset? filterStartDate = DateTimeOffset.Now;

        [ObservableProperty]
        private DateTimeOffset? filterEndDate = DateTimeOffset.Now;

        public bool IsReceiptSelected => SelectedReceipt != null;
        public bool IsCustomFilterVisible => SelectedFilterType == DateFilterType.Custom;

        /// <summary>
        /// Částka k úhradě po odečtení dárkového poukazu
        /// </summary>
        public decimal AmountToPay => SelectedReceipt != null
            ? SelectedReceipt.TotalAmount - (SelectedReceipt.ContainsGiftCardRedemption ? SelectedReceipt.GiftCardRedemptionAmount : 0)
            : 0;

        public string AmountToPayFormatted => $"{AmountToPay:C}";

        public UctenkyViewModel(IDataService dataService)
        {
            _dataService = dataService;
        }

        partial void OnSelectedFilterTypeChanged(DateFilterType value)
        {
            if (IsLoading) return;
            LoadReceiptsCommand.Execute(null);
        }

        partial void OnFilterStartDateChanged(DateTimeOffset? value)
        {
            if (IsLoading) return;
            if (SelectedFilterType == DateFilterType.Custom && value.HasValue)
            {
                LoadReceiptsCommand.Execute(null);
            }
        }

        partial void OnFilterEndDateChanged(DateTimeOffset? value)
        {
            if (IsLoading) return;
            if (SelectedFilterType == DateFilterType.Custom && value.HasValue)
            {
                LoadReceiptsCommand.Execute(null);
            }
        }

        private bool CanLoad() => !IsLoading;

        [RelayCommand(CanExecute = nameof(CanLoad))]
        private async Task LoadReceiptsAsync()
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
                        int currentDayOfWeek = (int)DateTime.Today.DayOfWeek;
                        int daysToSubtract = (currentDayOfWeek == 0) ? 6 : currentDayOfWeek - 1;
                        startDate = DateTime.Today.AddDays(-daysToSubtract);
                        endDate = startDate.AddDays(7).AddTicks(-1);
                        break;
                    case DateFilterType.Monthly:
                        startDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                        endDate = startDate.AddMonths(1).AddTicks(-1);
                        break;
                    case DateFilterType.Custom:
                        if (!FilterStartDate.HasValue || !FilterEndDate.HasValue)
                        {
                            // If dates are not set, use today as default
                            startDate = DateTime.Today;
                            endDate = DateTime.Today.AddDays(1).AddTicks(-1);
                        }
                        else
                        {
                            startDate = FilterStartDate.Value.Date;
                            endDate = FilterEndDate.Value.Date.AddDays(1).AddTicks(-1);
                        }
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
            finally
            {
                IsLoading = false;
            }
        }
    }
}
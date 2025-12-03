using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Sklad_2.Messages;
using Sklad_2.Models;
using Sklad_2.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Sklad_2.ViewModels
{
    public partial class PrehledProdejuViewModel : ObservableObject
    {
        private readonly IDataService _dataService;
        private readonly ISettingsService _settingsService;
        private readonly IMessenger _messenger;

        public bool IsVatPayer => _settingsService.CurrentSettings.IsVatPayer;

        [ObservableProperty]
        private ObservableCollection<Receipt> sales = new ObservableCollection<Receipt>();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TotalSalesAmountFormatted))]
        private decimal totalSalesAmount;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TotalSalesAmountWithoutVatFormatted))]
        private decimal totalSalesAmountWithoutVat;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TotalVatAmountFormatted))]
        private decimal totalVatAmount;

        public string TotalSalesAmountFormatted => $"{TotalSalesAmount:C}";
        public string TotalSalesAmountWithoutVatFormatted => $"{TotalSalesAmountWithoutVat:C}";
        public string TotalVatAmountFormatted => $"{TotalVatAmount:C}";

        [ObservableProperty]
        private int numberOfReceipts;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AverageSaleAmountFormatted))]
        private decimal averageSaleAmount;

        public string AverageSaleAmountFormatted => $"{AverageSaleAmount:C}";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DailyAverageAmountFormatted))]
        private decimal dailyAverageAmount;

        public string DailyAverageAmountFormatted => $"{DailyAverageAmount:C}";

        [ObservableProperty]
        private DateTimeOffset startDate;

        [ObservableProperty]
        private DateTimeOffset endDate;

        [ObservableProperty]
        private DateFilterType selectedFilter = DateFilterType.All;

        [RelayCommand]
        private void SetFilter(string filterType)
        {
            if (Enum.TryParse<DateFilterType>(filterType, out var filter))
            {
                // Always set the filter (even if it's the same value)
                // This ensures data reloads when clicking the same button
                if (SelectedFilter == filter)
                {
                    // Force reload by manually calling the methods
                    SetDateRangeForFilter(filter);
                    _ = LoadSalesDataAsync();
                }
                else
                {
                    SelectedFilter = filter; // This triggers OnSelectedFilterChanged
                }
            }
        }

        // Top products
        [ObservableProperty]
        private ObservableCollection<TopProduct> topProducts = new ObservableCollection<TopProduct>();

        // Worst products
        [ObservableProperty]
        private ObservableCollection<TopProduct> worstProducts = new ObservableCollection<TopProduct>();

        // Payment methods
        [ObservableProperty]
        private ObservableCollection<PaymentMethodStats> paymentMethodStats = new ObservableCollection<PaymentMethodStats>();

        public PrehledProdejuViewModel(IDataService dataService, ISettingsService settingsService, IMessenger messenger)
        {
            _dataService = dataService;
            _settingsService = settingsService;
            _messenger = messenger;

            // Set initial date range for "All" filter
            SetDateRangeForFilter(DateFilterType.All);

            // Listen for settings changes to update IsVatPayer property
            _messenger.Register<SettingsChangedMessage>(this, (r, m) =>
            {
                OnPropertyChanged(nameof(IsVatPayer));
            });
        }

        partial void OnSelectedFilterChanged(DateFilterType value)
        {
            SetDateRangeForFilter(value);
            _ = LoadSalesDataAsync();
        }

        private void SetDateRangeForFilter(DateFilterType filter)
        {
            var now = DateTime.Now;
            switch (filter)
            {
                case DateFilterType.All:
                    StartDate = new DateTimeOffset(new DateTime(2000, 1, 1));
                    EndDate = new DateTimeOffset(new DateTime(2099, 12, 31, 23, 59, 59));
                    break;
                case DateFilterType.Daily:
                    StartDate = new DateTimeOffset(now.Date);
                    EndDate = new DateTimeOffset(now.Date.AddDays(1).AddSeconds(-1));
                    break;
                case DateFilterType.Weekly:
                    // Get current day of week (0=Sunday, 1=Monday, ..., 6=Saturday)
                    int currentDayOfWeek = (int)now.DayOfWeek;
                    // Calculate days to subtract to get to Monday
                    // If Sunday (0), go back 6 days; if Monday (1), go back 0 days, etc.
                    int daysToSubtract = (currentDayOfWeek == 0) ? 6 : currentDayOfWeek - 1;
                    var startOfWeek = now.Date.AddDays(-daysToSubtract);
                    StartDate = new DateTimeOffset(startOfWeek);
                    EndDate = new DateTimeOffset(startOfWeek.AddDays(7).AddSeconds(-1));
                    break;
                case DateFilterType.Monthly:
                    StartDate = new DateTimeOffset(new DateTime(now.Year, now.Month, 1));
                    EndDate = new DateTimeOffset(new DateTime(now.Year, now.Month, DateTime.DaysInMonth(now.Year, now.Month), 23, 59, 59));
                    break;
                case DateFilterType.Custom:
                    // Keep current dates
                    break;
            }
        }

        [RelayCommand]
        private async Task LoadSalesDataAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"LoadSalesDataAsync: StartDate={StartDate:dd.MM.yyyy}, EndDate={EndDate:dd.MM.yyyy}");

                Sales.Clear();
                var allReceipts = await _dataService.GetReceiptsAsync(StartDate.DateTime, EndDate.DateTime);

                System.Diagnostics.Debug.WriteLine($"LoadSalesDataAsync: Loaded {allReceipts.Count()} receipts");

                foreach (var receipt in allReceipts.OrderByDescending(r => r.SaleDate))
                {
                    Sales.Add(receipt);
                }

                // KRITICKÉ: Načíst vratky pro stejný časový rozsah
                var allReturns = await _dataService.GetReturnsAsync(StartDate.DateTime, EndDate.DateTime);
                System.Diagnostics.Debug.WriteLine($"LoadSalesDataAsync: Loaded {allReturns.Count()} returns");

                CalculateTotals(allReturns);

                System.Diagnostics.Debug.WriteLine($"LoadSalesDataAsync: Completed. Total={TotalSalesAmount:C}, Count={NumberOfReceipts}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadSalesDataAsync ERROR: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void CalculateTotals(IEnumerable<Return> returns)
        {
            // KRITICKÉ: Use FinalAmountRounded (zaokrouhlená částka) místo AmountToPay (přesná)
            // Důvod: Denní uzávěrka počítá se zaokrouhlenými částkami
            // DRY: Delegace na Receipt.FinalAmountRounded computed property

            // 1. Sečíst všechny účtenky (storno účtenky mají záporný FinalAmountRounded)
            var receiptTotal = Sales.Sum(r => r.FinalAmountRounded);

            // 2. KRITICKÉ: Odečíst vratky (stejně jako DailyCloseService!)
            // DRY: Delegace na Return.FinalRefundRounded computed property
            var returnTotal = returns.Sum(r => r.FinalRefundRounded);

            // 3. Celková tržba = účtenky - vratky
            TotalSalesAmount = receiptTotal - returnTotal;

            TotalSalesAmountWithoutVat = Sales.Sum(r => r.TotalAmountWithoutVat);
            TotalVatAmount = Sales.Sum(r => r.TotalVatAmount);
            NumberOfReceipts = Sales.Count;
            AverageSaleAmount = NumberOfReceipts > 0 ? TotalSalesAmount / NumberOfReceipts : 0;

            // Calculate daily average based on selected time period
            int numberOfDays = CalculateNumberOfDays();
            DailyAverageAmount = numberOfDays > 0 ? TotalSalesAmount / numberOfDays : 0;

            CalculateTopProducts();
            CalculateWorstProducts();
            CalculatePaymentMethodStats();

            System.Diagnostics.Debug.WriteLine($"CalculateTotals: Receipts={receiptTotal:C}, Returns={returnTotal:C}, Final={TotalSalesAmount:C}");
        }

        private int CalculateNumberOfDays()
        {
            // Calculate the number of days in the current date range
            var days = (EndDate.Date - StartDate.Date).Days + 1; // +1 to include both start and end day

            // For "All" filter, calculate based on actual sales data if available
            if (SelectedFilter == DateFilterType.All && Sales.Any())
            {
                var firstSaleDate = Sales.Min(r => r.SaleDate).Date;
                var lastSaleDate = Sales.Max(r => r.SaleDate).Date;
                days = (lastSaleDate - firstSaleDate).Days + 1;
            }

            return days > 0 ? days : 1; // Minimum 1 day to avoid division by zero
        }

        private void CalculateTopProducts()
        {
            TopProducts.Clear();

            var productStats = Sales
                .SelectMany(r => r.Items ?? new ObservableCollection<ReceiptItem>())
                .GroupBy(item => item.ProductName)
                .Select(g => new TopProduct
                {
                    ProductName = g.Key,
                    QuantitySold = g.Sum(item => item.Quantity),
                    TotalRevenue = g.Sum(item => item.TotalPrice)
                })
                .OrderByDescending(p => p.TotalRevenue)
                .Take(5)
                .ToList();

            var maxRevenue = productStats.FirstOrDefault()?.TotalRevenue ?? 1;
            foreach (var product in productStats)
            {
                product.PercentageOfTotal = maxRevenue > 0 ? (double)(product.TotalRevenue / maxRevenue) * 100 : 0;
                TopProducts.Add(product);
            }
        }

        private void CalculateWorstProducts()
        {
            WorstProducts.Clear();

            var productStats = Sales
                .SelectMany(r => r.Items ?? new ObservableCollection<ReceiptItem>())
                .GroupBy(item => item.ProductName)
                .Select(g => new TopProduct
                {
                    ProductName = g.Key,
                    QuantitySold = g.Sum(item => item.Quantity),
                    TotalRevenue = g.Sum(item => item.TotalPrice)
                })
                .OrderBy(p => p.QuantitySold)
                .Take(5)
                .ToList();

            var maxQuantity = productStats.LastOrDefault()?.QuantitySold ?? 1;
            foreach (var product in productStats)
            {
                product.PercentageOfTotal = maxQuantity > 0 ? (double)product.QuantitySold / maxQuantity * 100 : 0;
                WorstProducts.Add(product);
            }
        }

        private void CalculatePaymentMethodStats()
        {
            PaymentMethodStats.Clear();

            // KRITICKÉ: Use FinalAmountRounded (zaokrouhlená částka) místo AmountToPay (přesná)
            // Důvod: Skutečná částka v pokladně je zaokrouhlená (CashAmount/CardAmount)
            // DRY: Delegace na Receipt.FinalAmountRounded computed property
            var paymentStats = Sales
                .GroupBy(r => r.PaymentMethod)
                .Select(g => new PaymentMethodStats
                {
                    PaymentMethod = g.Key,
                    Count = g.Count(),
                    TotalAmount = g.Sum(r => r.FinalAmountRounded)
                })
                .ToList();

            var totalAmount = paymentStats.Sum(p => p.TotalAmount);
            foreach (var stat in paymentStats)
            {
                stat.Percentage = totalAmount > 0 ? (double)(stat.TotalAmount / totalAmount) * 100 : 0;
                PaymentMethodStats.Add(stat);
            }
        }
    }
}

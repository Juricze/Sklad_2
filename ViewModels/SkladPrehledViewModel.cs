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
    public partial class SkladPrehledViewModel : ObservableObject
    {
        private readonly IDataService _dataService;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(LoadMovementsCommand))]
        private bool isLoading;

        public ObservableCollection<StockMovement> Movements { get; } = new ObservableCollection<StockMovement>();
        public List<DateFilterType> DateFilterOptions { get; } = Enum.GetValues(typeof(DateFilterType)).Cast<DateFilterType>().ToList();
        public List<StockMovementTypeFilter> TypeFilterOptions { get; } = new List<StockMovementTypeFilter>
        {
            new StockMovementTypeFilter { Name = "Vše", Type = null },
            new StockMovementTypeFilter { Name = "Nový produkt", Type = StockMovementType.ProductCreated },
            new StockMovementTypeFilter { Name = "Naskladnění", Type = StockMovementType.StockIn },
            new StockMovementTypeFilter { Name = "Prodej", Type = StockMovementType.Sale },
            new StockMovementTypeFilter { Name = "Vratka", Type = StockMovementType.Return },
            new StockMovementTypeFilter { Name = "Úprava", Type = StockMovementType.Adjustment },
            new StockMovementTypeFilter { Name = "Odpis - Tester", Type = StockMovementType.WriteOffTester },
            new StockMovementTypeFilter { Name = "Odpis - Poškozené", Type = StockMovementType.WriteOffDamaged }
        };

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsCustomFilterVisible))]
        private DateFilterType selectedDateFilter = DateFilterType.Daily;

        [ObservableProperty]
        private StockMovementTypeFilter selectedTypeFilter;

        [ObservableProperty]
        private DateTimeOffset filterStartDate = DateTimeOffset.Now;

        [ObservableProperty]
        private DateTimeOffset filterEndDate = DateTimeOffset.Now;

        [ObservableProperty]
        private string searchProductText = string.Empty;

        public bool IsCustomFilterVisible => SelectedDateFilter == DateFilterType.Custom;

        // Statistics
        [ObservableProperty]
        private int totalMovements;

        [ObservableProperty]
        private int totalStockIn;

        [ObservableProperty]
        private int totalStockOut;

        [ObservableProperty]
        private int netStockChange;

        public SkladPrehledViewModel(IDataService dataService)
        {
            _dataService = dataService;
            SelectedTypeFilter = TypeFilterOptions[0]; // "Vše"
        }

        partial void OnSelectedDateFilterChanged(DateFilterType value)
        {
            if (IsLoading) return;
            LoadMovementsCommand.Execute(null);
        }

        partial void OnSelectedTypeFilterChanged(StockMovementTypeFilter value)
        {
            if (IsLoading) return;
            LoadMovementsCommand.Execute(null);
        }

        partial void OnFilterStartDateChanged(DateTimeOffset value)
        {
            if (IsLoading) return;
            if (SelectedDateFilter == DateFilterType.Custom)
            {
                LoadMovementsCommand.Execute(null);
            }
        }

        partial void OnFilterEndDateChanged(DateTimeOffset value)
        {
            if (IsLoading) return;
            if (SelectedDateFilter == DateFilterType.Custom)
            {
                LoadMovementsCommand.Execute(null);
            }
        }

        partial void OnSearchProductTextChanged(string value)
        {
            if (IsLoading) return;
            LoadMovementsCommand.Execute(null);
        }

        private bool CanLoad() => !IsLoading;

        [RelayCommand(CanExecute = nameof(CanLoad))]
        private async Task LoadMovementsAsync()
        {
            IsLoading = true;
            try
            {
                System.Diagnostics.Debug.WriteLine("=== LoadMovementsAsync START ===");
                DateTime startDate;
                DateTime endDate;

                switch (SelectedDateFilter)
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
                        startDate = FilterStartDate.Date;
                        endDate = FilterEndDate.Date.AddDays(1).AddTicks(-1);
                        break;
                    default:
                        return;
                }

                System.Diagnostics.Debug.WriteLine($"Date range: {startDate} to {endDate}");
                Movements.Clear();
                System.Diagnostics.Debug.WriteLine("About to call GetStockMovementsAsync...");
                var filteredMovements = await _dataService.GetStockMovementsAsync(startDate, endDate);
                System.Diagnostics.Debug.WriteLine($"Got {filteredMovements.Count} movements");

                // Filter by type
                if (SelectedTypeFilter?.Type != null)
                {
                    filteredMovements = filteredMovements.Where(m => m.MovementType == SelectedTypeFilter.Type.Value).ToList();
                }

                // Filter by product search
                if (!string.IsNullOrWhiteSpace(SearchProductText))
                {
                    var searchLower = SearchProductText.ToLower();
                    filteredMovements = filteredMovements.Where(m =>
                        m.ProductEan.ToLower().Contains(searchLower) ||
                        m.ProductName.ToLower().Contains(searchLower)
                    ).ToList();
                }

                foreach (var movement in filteredMovements.OrderByDescending(m => m.Timestamp))
                {
                    Movements.Add(movement);
                }

                // Calculate statistics
                TotalMovements = Movements.Count;
                TotalStockIn = Movements.Where(m => m.QuantityChange > 0).Sum(m => m.QuantityChange);
                TotalStockOut = Math.Abs(Movements.Where(m => m.QuantityChange < 0).Sum(m => m.QuantityChange));
                NetStockChange = TotalStockIn - TotalStockOut;
                System.Diagnostics.Debug.WriteLine("=== LoadMovementsAsync SUCCESS ===");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("=== LoadMovementsAsync ERROR ===");
                System.Diagnostics.Debug.WriteLine($"Exception: {ex.GetType().Name}");
                System.Diagnostics.Debug.WriteLine($"Message: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Inner Exception: {ex.InnerException.GetType().Name}");
                    System.Diagnostics.Debug.WriteLine($"Inner Message: {ex.InnerException.Message}");
                }
                throw; // Re-throw to see it in debugger
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    // Helper class for type filter
    public class StockMovementTypeFilter
    {
        public string Name { get; set; }
        public StockMovementType? Type { get; set; }
    }
}

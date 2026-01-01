using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Sklad_2.Messages;
using Sklad_2.Models;
using Sklad_2.Services;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Sklad_2.ViewModels
{
    public partial class TrzbyUzavirkViewModel : ObservableObject
    {
        private readonly IDailyCloseService _dailyCloseService;
        private readonly IAuthService _authService;
        private readonly ISettingsService _settingsService;
        private readonly IMessenger _messenger;

        public TrzbyUzavirkViewModel(IDailyCloseService dailyCloseService, IAuthService authService, ISettingsService settingsService, IMessenger messenger)
        {
            _dailyCloseService = dailyCloseService;
            _authService = authService;
            _settingsService = settingsService;
            _messenger = messenger;

            // Initialize export selectors
            InitializeExportSelectors();

            // Listen for settings changes to auto-refresh data
            _messenger.Register<SettingsChangedMessage>(this, async (r, m) =>
            {
                Debug.WriteLine("TrzbyUzavirkViewModel: SettingsChangedMessage received");
                await Task.Delay(300); // Win10 file flush + settings propagation
                await LoadTodaySalesAsync();
                await Task.Delay(100); // Win10 UI update

                // Second refresh for Win10 reliability
                await LoadTodaySalesAsync();
                Debug.WriteLine("TrzbyUzavirkViewModel: Auto-refresh completed (Win10 double-refresh)");
            });
        }

        private void InitializeExportSelectors()
        {
            // Populate years (from 2024 to current year)
            var currentYear = DateTime.Today.Year;
            for (int year = 2024; year <= currentYear; year++)
            {
                ExportAvailableYears.Add(year);
            }

            // Default values for monthly overview navigation
            SelectedYear = currentYear;
            SelectedMonth = DateTime.Today.Month;

            // Default values for export (Měsíční export aktuálního měsíce)
            SelectedExportType = ExportPeriodType.Monthly;
            ExportYear = currentYear;
            SelectedExportPeriodIndex = DateTime.Today.Month - 1; // 0-based index
        }

        // Datum session (den který se zobrazuje/uzavírá)
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SessionDateFormatted), nameof(DayStatusFormatted))]
        private DateTime sessionDate;

        public string SessionDateFormatted => $"Den: {SessionDate:dd.MM.yyyy}";

        // Aktuální denní tržba
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TodayCashSalesFormatted))]
        private decimal todayCashSales;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TodayCardSalesFormatted))]
        private decimal todayCardSales;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TodayTotalSalesFormatted))]
        private decimal todayTotalSales;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ReceiptCountFormatted))]
        private int todayReceiptCount;

        public string TodayCashSalesFormatted => $"{TodayCashSales:N2} Kč";
        public string TodayCardSalesFormatted => $"{TodayCardSales:N2} Kč";
        public string TodayTotalSalesFormatted => $"{TodayTotalSales:N2} Kč";
        public string ReceiptCountFormatted => $"Počet účtenek: {TodayReceiptCount}";

        // Den uzavřen?
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DayStatusFormatted), nameof(IsCloseDayButtonEnabled))]
        private bool isDayClosed;

        public string DayStatusFormatted => IsDayClosed
            ? $"🔒 Den uzavřen ({SessionDateFormatted})"
            : $"🔓 Den otevřen ({SessionDateFormatted})";

        public bool IsCloseDayButtonEnabled => !IsDayClosed;

        // Seznam uzavírek
        public ObservableCollection<DailyClose> DailyCloses { get; } = new();

        // Přehled denních tržeb za aktuální měsíc
        public ObservableCollection<DailySalesSummary> DailySalesSummaries { get; } = new();

        [ObservableProperty]
        private string currentMonthName;

        // Year/Month tracking pro Monthly Overview (internal - jen pro navigation šipkami)
        [ObservableProperty]
        private int selectedYear = DateTime.Today.Year;

        [ObservableProperty]
        private int selectedMonth = DateTime.Today.Month;

        // ===== EXPORT CONFIGURATION (NEW UX) =====

        /// <summary>
        /// Seznam názvů typů exportu (pro ComboBox)
        /// </summary>
        public ObservableCollection<string> ExportTypeNames { get; } = new ObservableCollection<string>
        {
            "Týdenní",
            "Měsíční",
            "Čtvrtletní",
            "Půlroční",
            "Roční"
        };

        /// <summary>
        /// Index vybraného typu exportu (0=Týdenní, 1=Měsíční, 2=Čtvrtletní, 3=Půlroční, 4=Roční)
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AvailableExportPeriods), nameof(ExportPeriodPreview), nameof(ShowPeriodSelector))]
        private int selectedExportTypeIndex = 1; // Default: Měsíční (index 1)

        /// <summary>
        /// Reset SelectedExportPeriodIndex při změně typu exportu (fix out-of-range bug)
        /// </summary>
        partial void OnSelectedExportTypeIndexChanged(int value)
        {
            // Reset period index na bezpečnou hodnotu (aktuální měsíc/týden/Q/H nebo 0)
            SelectedExportPeriodIndex = SelectedExportType switch
            {
                ExportPeriodType.Weekly => 0, // První týden roku
                ExportPeriodType.Monthly => Math.Min(DateTime.Today.Month - 1, 11), // Aktuální měsíc
                ExportPeriodType.Quarterly => Math.Min((DateTime.Today.Month - 1) / 3, 3), // Aktuální čtvrtletí
                ExportPeriodType.HalfYearly => Math.Min((DateTime.Today.Month - 1) / 6, 1), // Aktuální půlrok
                ExportPeriodType.Yearly => 0, // Není potřeba (žádný ComboBox)
                _ => 0
            };
        }

        /// <summary>
        /// Typ exportu (Týdenní/Měsíční/Čtvrtletní/Půlroční/Roční)
        /// </summary>
        public ExportPeriodType SelectedExportType
        {
            get => (ExportPeriodType)SelectedExportTypeIndex;
            set
            {
                SelectedExportTypeIndex = (int)value;
                OnPropertyChanged(nameof(SelectedExportType));
            }
        }

        /// <summary>
        /// Rok pro export
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AvailableExportPeriods), nameof(ExportPeriodPreview))]
        private int exportYear = DateTime.Today.Year;

        /// <summary>
        /// Index vybraného období (týden/měsíc/čtvrtletí/pololetí) - 0-based
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ExportPeriodPreview))]
        private int selectedExportPeriodIndex = DateTime.Today.Month - 1; // Defaultně aktuální měsíc

        /// <summary>
        /// Dostupné roky pro export
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<int> exportAvailableYears = new();

        /// <summary>
        /// Zobrazit ComboBox pro výběr období (skrýt pro Yearly export)
        /// </summary>
        public bool ShowPeriodSelector => SelectedExportType != ExportPeriodType.Yearly;

        /// <summary>
        /// Dynamický seznam období podle vybraného typu exportu
        /// </summary>
        public ObservableCollection<string> AvailableExportPeriods
        {
            get
            {
                var periods = new ObservableCollection<string>();

                switch (SelectedExportType)
                {
                    case ExportPeriodType.Weekly:
                        periods = GetAvailableWeeks(ExportYear);
                        break;

                    case ExportPeriodType.Monthly:
                        var czechMonths = new[] {
                            "Leden", "Únor", "Březen", "Duben", "Květen", "Červen",
                            "Červenec", "Srpen", "Září", "Říjen", "Listopad", "Prosinec"
                        };
                        foreach (var month in czechMonths)
                        {
                            periods.Add(month);
                        }
                        break;

                    case ExportPeriodType.Quarterly:
                        periods.Add("Q1 (leden-březen)");
                        periods.Add("Q2 (duben-červen)");
                        periods.Add("Q3 (červenec-září)");
                        periods.Add("Q4 (říjen-prosinec)");
                        break;

                    case ExportPeriodType.HalfYearly:
                        periods.Add("H1 (leden-červen)");
                        periods.Add("H2 (červenec-prosinec)");
                        break;

                    case ExportPeriodType.Yearly:
                        // Roční export nepotřebuje další selector
                        break;
                }

                return periods;
            }
        }

        /// <summary>
        /// Náhled vybraného období (např. "Období: 1.12.2025 - 31.12.2025")
        /// </summary>
        public string ExportPeriodPreview
        {
            get
            {
                try
                {
                    var (startDate, endDate) = GetExportDateRange();
                    return $"Období: {startDate:dd.MM.yyyy} - {endDate:dd.MM.yyyy}";
                }
                catch
                {
                    return "Období: -";
                }
            }
        }

        /// <summary>
        /// Získat rozsah dat pro aktuální výběr exportu
        /// </summary>
        private (DateTime StartDate, DateTime EndDate) GetExportDateRange()
        {
            DateTime startDate;
            DateTime endDate;

            try
            {
                switch (SelectedExportType)
                {
                    case ExportPeriodType.Weekly:
                        // Parse week index to get dates
                        var weekData = GetWeekDateRange(ExportYear, SelectedExportPeriodIndex);
                        startDate = weekData.StartDate;
                        endDate = weekData.EndDate;
                        break;

                    case ExportPeriodType.Monthly:
                        int month = Math.Clamp(SelectedExportPeriodIndex + 1, 1, 12); // Index 0-11 → Month 1-12 (clamped)
                        startDate = new DateTime(ExportYear, month, 1);
                        endDate = startDate.AddMonths(1).AddDays(-1);
                        break;

                    case ExportPeriodType.Quarterly:
                        int quarterIndex = Math.Clamp(SelectedExportPeriodIndex, 0, 3); // Q0-Q3
                        int quarterStartMonth = (quarterIndex * 3) + 1; // Q0→1, Q1→4, Q2→7, Q3→10
                        startDate = new DateTime(ExportYear, quarterStartMonth, 1);
                        endDate = startDate.AddMonths(3).AddDays(-1);
                        break;

                    case ExportPeriodType.HalfYearly:
                        int halfIndex = Math.Clamp(SelectedExportPeriodIndex, 0, 1); // H0-H1
                        int halfStartMonth = (halfIndex * 6) + 1; // H0→1, H1→7
                        startDate = new DateTime(ExportYear, halfStartMonth, 1);
                        endDate = startDate.AddMonths(6).AddDays(-1);
                        break;

                    case ExportPeriodType.Yearly:
                        startDate = new DateTime(ExportYear, 1, 1);
                        endDate = new DateTime(ExportYear, 12, 31);
                        break;

                    default:
                        startDate = DateTime.Today;
                        endDate = DateTime.Today;
                        break;
                }

                return (startDate, endDate);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TrzbyUzavirkViewModel: Error calculating export date range: {ex.Message}");
                // Fallback: aktuální měsíc
                var today = DateTime.Today;
                return (new DateTime(today.Year, today.Month, 1),
                        new DateTime(today.Year, today.Month, 1).AddMonths(1).AddDays(-1));
            }
        }

        /// <summary>
        /// Získat seznam týdnů pro daný rok (ISO 8601: pondělí-neděle)
        /// </summary>
        private ObservableCollection<string> GetAvailableWeeks(int year)
        {
            var weeks = new ObservableCollection<string>();
            var firstDayOfYear = new DateTime(year, 1, 1);
            var lastDayOfYear = new DateTime(year, 12, 31);

            // Najít první pondělí roku (nebo 1.1. pokud je to pondělí)
            var currentMonday = firstDayOfYear;
            while (currentMonday.DayOfWeek != DayOfWeek.Monday)
            {
                currentMonday = currentMonday.AddDays(1);
            }

            // Pokud první pondělí je až v příštím roce, začni od 1.1.
            if (currentMonday.Year > year)
            {
                currentMonday = firstDayOfYear;
            }

            int weekNumber = 1;

            while (currentMonday.Year == year && currentMonday <= lastDayOfYear)
            {
                var sunday = currentMonday.AddDays(6);

                // Pokud neděle je už v příštím roce, ukonči na poslední den tohoto roku
                if (sunday.Year > year)
                {
                    sunday = lastDayOfYear;
                }

                weeks.Add($"Týden {weekNumber} ({currentMonday:dd.MM} - {sunday:dd.MM})");

                currentMonday = currentMonday.AddDays(7);
                weekNumber++;
            }

            return weeks;
        }

        /// <summary>
        /// Získat rozsah dat pro vybraný týden
        /// </summary>
        private (DateTime StartDate, DateTime EndDate) GetWeekDateRange(int year, int weekIndex)
        {
            var firstDayOfYear = new DateTime(year, 1, 1);

            // Najít první pondělí roku
            var currentMonday = firstDayOfYear;
            while (currentMonday.DayOfWeek != DayOfWeek.Monday)
            {
                currentMonday = currentMonday.AddDays(1);
            }

            // Pokud první pondělí je až v příštím roce, začni od 1.1.
            if (currentMonday.Year > year)
            {
                currentMonday = firstDayOfYear;
            }

            // Přejdi na vybraný týden
            var startDate = currentMonday.AddDays(weekIndex * 7);
            var endDate = startDate.AddDays(6);

            // Limit do konce roku
            var lastDayOfYear = new DateTime(year, 12, 31);
            if (endDate > lastDayOfYear)
            {
                endDate = lastDayOfYear;
            }

            return (startDate, endDate);
        }

        // Filtry (legacy - pro budoucí Custom range funkci)
        [ObservableProperty]
        private DateTime? filterFromDate;

        [ObservableProperty]
        private DateTime? filterToDate;

        // Status message
        [ObservableProperty]
        private string statusMessage;

        /// <summary>
        /// Načíst aktuální tržby dne
        /// </summary>
        [RelayCommand]
        public async Task LoadTodaySalesAsync()
        {
            try
            {
                // Nastavit session datum (den který se zobrazuje/uzavírá)
                SessionDate = _settingsService.CurrentSettings.LastSaleLoginDate?.Date ?? DateTime.Today;

                var (cash, card, total, count) = await _dailyCloseService.GetTodaySalesAsync();
                TodayCashSales = cash;
                TodayCardSales = card;
                TodayTotalSales = total;
                TodayReceiptCount = count;

                // Kontrola, zda je už session den uzavřený
                IsDayClosed = await _dailyCloseService.IsDayClosedAsync(SessionDate);

                // Načíst přehled denních tržeb za měsíc
                await LoadDailySalesSummariesAsync();

                Debug.WriteLine($"TrzbyUzavirkViewModel: Loaded session ({SessionDate:yyyy-MM-dd}) sales - Total: {total:N2} Kč, Closed: {IsDayClosed}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TrzbyUzavirkViewModel: Error loading today sales: {ex.Message}");
                StatusMessage = $"Chyba při načítání tržeb: {ex.Message}";
            }
        }

        /// <summary>
        /// Notifikace o zahájení nového dne - aktualizuje UI a notifikuje ostatní ViewModely
        /// </summary>
        public async Task NotifyNewDayStartedAsync(DateTime? newSessionDate = null)
        {
            Debug.WriteLine($"TrzbyUzavirkViewModel: NotifyNewDayStartedAsync called with date: {newSessionDate:yyyy-MM-dd}");

            // Pokud bylo předáno nové datum, nastavit SessionDate přímo
            if (newSessionDate.HasValue)
            {
                SessionDate = newSessionDate.Value;
                Debug.WriteLine($"TrzbyUzavirkViewModel: SessionDate set to {SessionDate:yyyy-MM-dd}");
            }

            // Notifikovat ostatní ViewModely (Status Bar atd.)
            _messenger.Send(new SettingsChangedMessage());

            // Win10 compatibility delays
            await Task.Delay(200); // File flush

            // Aktualizovat vlastní data
            await LoadTodaySalesAsync();
            await Task.Delay(100); // Win10 UI update

            // Win10: Force UI refresh by manually triggering all property change notifications
            OnPropertyChanged(nameof(SessionDate));
            OnPropertyChanged(nameof(SessionDateFormatted));
            OnPropertyChanged(nameof(TodayCashSales));
            OnPropertyChanged(nameof(TodayCashSalesFormatted));
            OnPropertyChanged(nameof(TodayCardSales));
            OnPropertyChanged(nameof(TodayCardSalesFormatted));
            OnPropertyChanged(nameof(TodayTotalSales));
            OnPropertyChanged(nameof(TodayTotalSalesFormatted));
            OnPropertyChanged(nameof(TodayReceiptCount));
            OnPropertyChanged(nameof(ReceiptCountFormatted));
            OnPropertyChanged(nameof(IsDayClosed));
            OnPropertyChanged(nameof(DayStatusFormatted));
            OnPropertyChanged(nameof(IsCloseDayButtonEnabled));

            Debug.WriteLine("TrzbyUzavirkViewModel: Forced Win10 UI refresh completed");
        }

        /// <summary>
        /// Uzavřít dnešní den
        /// </summary>
        [RelayCommand]
        public async Task<(bool Success, string Message, DailyClose DailyClose)> CloseTodayAsync()
        {
            try
            {
                var sellerName = _authService.CurrentUser?.DisplayName ?? "Neznámý";
                var (success, errorMessage, dailyClose) = await _dailyCloseService.CloseDayAsync(sellerName);

                if (success)
                {
                    IsDayClosed = true;
                    StatusMessage = $"Den úspěšně uzavřen. Celková tržba: {dailyClose.TotalSalesFormatted}";

                    // Reload sales to refresh display
                    await LoadTodaySalesAsync();

                    // Reload closes list
                    await LoadDailyClosesAsync();

                    Debug.WriteLine($"TrzbyUzavirkViewModel: Day closed successfully");
                    return (true, StatusMessage, dailyClose);
                }
                else
                {
                    StatusMessage = $"Chyba: {errorMessage}";
                    Debug.WriteLine($"TrzbyUzavirkViewModel: Failed to close day: {errorMessage}");
                    return (false, StatusMessage, null);
                }
            }
            catch (Exception ex)
            {
                var errorMsg = $"Chyba při uzavírání dne: {ex.Message}";
                StatusMessage = errorMsg;
                Debug.WriteLine($"TrzbyUzavirkViewModel: Error closing day: {ex.Message}");
                return (false, errorMsg, null);
            }
        }

        /// <summary>
        /// Navigovat na předchozí měsíc (◀ šipka)
        /// </summary>
        [RelayCommand]
        private async Task GoToPreviousMonthAsync()
        {
            if (SelectedMonth == 1)
            {
                SelectedMonth = 12;
                SelectedYear--;
            }
            else
            {
                SelectedMonth--;
            }

            // Reload data
            await LoadDailySalesSummariesAsync(SelectedYear, SelectedMonth);
        }

        /// <summary>
        /// Navigovat na další měsíc (▶ šipka)
        /// </summary>
        [RelayCommand]
        private async Task GoToNextMonthAsync()
        {
            // Calculate next month
            int nextMonth = SelectedMonth;
            int nextYear = SelectedYear;

            if (nextMonth == 12)
            {
                nextMonth = 1;
                nextYear++;
            }
            else
            {
                nextMonth++;
            }

            // LIMIT: Nelze jít do budoucnosti
            var currentDate = DateTime.Today;
            if (nextYear > currentDate.Year ||
                (nextYear == currentDate.Year && nextMonth > currentDate.Month))
            {
                // Nelze jít dál - jsme už v aktuálním měsíci
                Debug.WriteLine("TrzbyUzavirkViewModel: Cannot go to future month");
                return;
            }

            // Update and reload
            SelectedYear = nextYear;
            SelectedMonth = nextMonth;
            await LoadDailySalesSummariesAsync(SelectedYear, SelectedMonth);
        }

        /// <summary>
        /// Načíst přehled denních tržeb za vybraný měsíc
        /// </summary>
        public async Task LoadDailySalesSummariesAsync(int? year = null, int? month = null)
        {
            try
            {
                // Use parameters or fall back to selected values
                int targetYear = year ?? SelectedYear;
                int targetMonth = month ?? SelectedMonth;

                var summaries = await _dailyCloseService.GetMonthDailySalesAsync(targetYear, targetMonth);

                DailySalesSummaries.Clear();
                foreach (var summary in summaries)
                {
                    DailySalesSummaries.Add(summary);
                }

                // Update CurrentMonthName pro header
                var czechMonths = new[] {
                    "Leden", "Únor", "Březen", "Duben", "Květen", "Červen",
                    "Červenec", "Srpen", "Září", "Říjen", "Listopad", "Prosinec"
                };
                CurrentMonthName = $"{czechMonths[targetMonth - 1]} {targetYear}";

                Debug.WriteLine($"TrzbyUzavirkViewModel: Loaded {summaries.Count} daily summaries for {targetMonth}/{targetYear}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TrzbyUzavirkViewModel: Error loading daily summaries: {ex.Message}");
                StatusMessage = $"Chyba při načítání přehledu: {ex.Message}";
            }
        }

        /// <summary>
        /// Načíst seznam uzavírek s filtry
        /// </summary>
        [RelayCommand]
        public async Task LoadDailyClosesAsync()
        {
            try
            {
                var closes = await _dailyCloseService.GetDailyClosesAsync(FilterFromDate, FilterToDate);

                DailyCloses.Clear();
                foreach (var close in closes)
                {
                    DailyCloses.Add(close);
                }

                Debug.WriteLine($"TrzbyUzavirkViewModel: Loaded {closes.Count} daily closes");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TrzbyUzavirkViewModel: Error loading daily closes: {ex.Message}");
                StatusMessage = $"Chyba při načítání uzavírek: {ex.Message}";
            }
        }

        /// <summary>
        /// Export uzavírek za vybrané období (NEW UX)
        /// </summary>
        public async Task<(bool Success, string FilePath, string ErrorMessage)> ExportClosesAsync()
        {
            try
            {
                // Získat rozsah dat podle aktuálního výběru
                var (startDate, endDate) = GetExportDateRange();

                // Určit název období pro filename
                string periodName = SelectedExportType switch
                {
                    ExportPeriodType.Weekly => "tydenni",
                    ExportPeriodType.Monthly => "mesicni",
                    ExportPeriodType.Quarterly => "ctvrtletni",
                    ExportPeriodType.HalfYearly => "pulrocni",
                    ExportPeriodType.Yearly => "rocni",
                    _ => "export"
                };

                // Volat service s konkrétním date range
                var (success, filePath, errorMessage) = await _dailyCloseService.ExportDailyClosesByDateRangeAsync(
                    startDate, endDate, periodName);

                if (success)
                {
                    StatusMessage = $"Export úspěšný: {filePath}";
                    Debug.WriteLine($"TrzbyUzavirkViewModel: Export successful: {filePath} ({startDate:dd.MM.yyyy} - {endDate:dd.MM.yyyy})");
                }
                else
                {
                    StatusMessage = $"Chyba při exportu: {errorMessage}";
                    Debug.WriteLine($"TrzbyUzavirkViewModel: Export failed: {errorMessage}");
                }

                return (success, filePath, errorMessage);
            }
            catch (Exception ex)
            {
                var errorMsg = $"Chyba při exportu: {ex.Message}";
                StatusMessage = errorMsg;
                Debug.WriteLine($"TrzbyUzavirkViewModel: Error exporting: {ex.Message}");
                return (false, string.Empty, errorMsg);
            }
        }
    }
}

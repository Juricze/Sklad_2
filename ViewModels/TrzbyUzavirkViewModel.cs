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

        // Filtry
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
        /// Načíst přehled denních tržeb za aktuální měsíc
        /// </summary>
        [RelayCommand]
        public async Task LoadDailySalesSummariesAsync()
        {
            try
            {
                var summaries = await _dailyCloseService.GetCurrentMonthDailySalesAsync();

                DailySalesSummaries.Clear();
                foreach (var summary in summaries)
                {
                    DailySalesSummaries.Add(summary);
                }

                // Nastavit název měsíce
                var today = DateTime.Today;
                CurrentMonthName = $"{today:MMMM yyyy}";

                Debug.WriteLine($"TrzbyUzavirkViewModel: Loaded {summaries.Count} daily summaries");
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
        /// Export uzavírek za období
        /// </summary>
        public async Task<(bool Success, string FilePath, string ErrorMessage)> ExportClosesAsync(string period)
        {
            try
            {
                var (success, filePath, errorMessage) = await _dailyCloseService.ExportDailyClosesAsync(period, DateTime.Today);

                if (success)
                {
                    StatusMessage = $"Export úspěšný: {filePath}";
                    Debug.WriteLine($"TrzbyUzavirkViewModel: Export successful: {filePath}");
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

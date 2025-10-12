using CommunityToolkit.Mvvm.ComponentModel;
using Sklad_2.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Sklad_2.ViewModels
{
    public partial class StatusBarViewModel : ObservableObject
    {
        private readonly IDataService _dataService;
        private readonly ISettingsService _settingsService;
        private readonly IPrintService _printService;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PrinterStatusText), nameof(PrinterStatusColor))]
        private bool isPrinterConnected;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ScannerStatusText), nameof(ScannerStatusColor))]
        private bool isScannerConnected;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DayCloseStatusText), nameof(DayCloseStatusColor))]
        private bool isDayClosedToday;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(VatPayerStatusText), nameof(VatPayerStatusColor))]
        private bool isVatPayer;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CompanyInfoStatusText), nameof(CompanyInfoStatusColor))]
        private bool isCompanyInfoComplete;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(VatConfigStatusText), nameof(VatConfigStatusColor))]
        private bool isVatConfigComplete;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DatabaseStatusText), nameof(DatabaseStatusColor))]
        private bool isDatabaseOk;

        // Status texts
        public string PrinterStatusText => IsPrinterConnected ? "Připojena" : "Odpojena";
        public string ScannerStatusText => IsScannerConnected ? "Připojen" : "Odpojen";
        public string DayCloseStatusText => IsDayClosedToday ? "Provedena" : "Neprovedena";
        public string VatPayerStatusText => IsVatPayer ? "Plátce" : "Neplátce";
        public string CompanyInfoStatusText => IsCompanyInfoComplete ? "Vyplněno" : "Nevyplněno";
        public string VatConfigStatusText => IsVatConfigComplete ? "Nastaveno" : "Nenastaveno";
        public string DatabaseStatusText => IsDatabaseOk ? "OK" : "Chyba";

        // Status colors
        public string PrinterStatusColor => IsPrinterConnected ? "#34C759" : "#FF3B30";
        public string ScannerStatusColor => IsScannerConnected ? "#34C759" : "#999999";
        public string DayCloseStatusColor => IsDayClosedToday ? "#34C759" : "#FF9500";
        public string VatPayerStatusColor => "#007AFF";
        public string CompanyInfoStatusColor => IsCompanyInfoComplete ? "#34C759" : "#FF9500";
        public string VatConfigStatusColor => IsVatConfigComplete ? "#34C759" : "#FF9500";
        public string DatabaseStatusColor => IsDatabaseOk ? "#34C759" : "#FF3B30";

        public StatusBarViewModel(IDataService dataService, ISettingsService settingsService, IPrintService printService)
        {
            _dataService = dataService;
            _settingsService = settingsService;
            _printService = printService;
        }

        public async Task RefreshStatusAsync()
        {
            // Check printer status
            var settings = _settingsService.CurrentSettings;
            IsPrinterConnected = !string.IsNullOrWhiteSpace(settings.PrinterPath);

            // Check scanner status (placeholder - add real implementation if you have scanner service)
            IsScannerConnected = false; // TODO: Implement scanner check

            // Check if day was closed today
            var lastDayCloseDate = settings.LastDayCloseDate;
            IsDayClosedToday = lastDayCloseDate?.Date == DateTime.Today;

            // Check VAT payer status
            IsVatPayer = settings.IsVatPayer;

            // Check company info
            IsCompanyInfoComplete = !string.IsNullOrWhiteSpace(settings.ShopName) &&
                                   !string.IsNullOrWhiteSpace(settings.ShopAddress);

            // Check VAT config
            var vatConfigs = await _dataService.GetVatConfigsAsync();
            IsVatConfigComplete = vatConfigs.Any();

            // Check database status
            try
            {
                // Simple check - try to get products
                var products = await _dataService.GetProductsAsync();
                IsDatabaseOk = true;
            }
            catch
            {
                IsDatabaseOk = false;
            }
        }
    }
}

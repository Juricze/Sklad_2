using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Sklad_2.Messages;
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
        private readonly IMessenger _messenger;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PrinterStatusText), nameof(PrinterStatusColor))]
        private bool isPrinterConnected;

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

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(BackupPathStatusText), nameof(BackupPathStatusColor))]
        private bool isBackupPathConfigured;

        // Status texts
        public string PrinterStatusText => IsPrinterConnected ? "Připojena" : "Odpojena";
        public string DayCloseStatusText => IsDayClosedToday ? "Provedena" : "Neprovedena";
        public string VatPayerStatusText => IsVatPayer ? "Plátce" : "Neplátce";
        public string CompanyInfoStatusText => IsCompanyInfoComplete ? "Vyplněno" : "Nevyplněno";
        public string VatConfigStatusText => IsVatConfigComplete ? "Nastaveno" : "Nenastaveno";
        public string DatabaseStatusText => IsDatabaseOk ? "OK" : "Chyba";
        public string BackupPathStatusText => IsBackupPathConfigured ? "Nastavena" : "CHYBA";

        // Status colors
        public string PrinterStatusColor => IsPrinterConnected ? "#34C759" : "#FF3B30";
        public string DayCloseStatusColor => IsDayClosedToday ? "#34C759" : "#FF9500";
        public string BackupPathStatusColor => IsBackupPathConfigured ? "#34C759" : "#FF3B30";
        public string VatPayerStatusColor => "#007AFF";
        public string CompanyInfoStatusColor => IsCompanyInfoComplete ? "#34C759" : "#FF9500";
        public string VatConfigStatusColor => IsVatConfigComplete ? "#34C759" : "#FF9500";
        public string DatabaseStatusColor => IsDatabaseOk ? "#34C759" : "#FF3B30";

        public StatusBarViewModel(IDataService dataService, ISettingsService settingsService, IPrintService printService, IMessenger messenger)
        {
            _dataService = dataService;
            _settingsService = settingsService;
            _printService = printService;
            _messenger = messenger;

            // Listen for settings changes
            _messenger.Register<SettingsChangedMessage>(this, async (r, m) =>
            {
                await RefreshStatusAsync();
            });

            // Listen for VAT config changes
            _messenger.Register<VatConfigsChangedMessage>(this, async (r, m) =>
            {
                await RefreshStatusAsync();
            });
        }

        public async Task RefreshStatusAsync()
        {
            // Check printer status (actual connection test)
            IsPrinterConnected = _printService.IsPrinterConnected();

            var settings = _settingsService.CurrentSettings;

            // Check if day was closed today
            var lastDayCloseDate = settings.LastDayCloseDate;
            IsDayClosedToday = lastDayCloseDate?.Date == DateTime.Today;

            // Check VAT payer status
            IsVatPayer = settings.IsVatPayer;

            // Check company info (ShopName, ShopAddress, CompanyId always required)
            IsCompanyInfoComplete = !string.IsNullOrWhiteSpace(settings.ShopName) &&
                                   !string.IsNullOrWhiteSpace(settings.ShopAddress) &&
                                   !string.IsNullOrWhiteSpace(settings.CompanyId);

            // Check VAT config
            var vatConfigs = await _dataService.GetVatConfigsAsync();
            IsVatConfigComplete = vatConfigs.Any();

            // Check backup path status
            IsBackupPathConfigured = _settingsService.IsBackupPathConfigured();

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

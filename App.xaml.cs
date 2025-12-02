using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Sklad_2.Data;
using Sklad_2.Services;
using Sklad_2.ViewModels;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;

namespace Sklad_2
{
    public partial class App : Application
    {
        // Win32 MessageBox for single-instance warning (before WinUI is initialized)
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

        private const uint MB_OK = 0x00000000;
        private const uint MB_ICONWARNING = 0x00000030;

        public IServiceProvider Services { get; }
        private Window m_window;
        private static Mutex _singleInstanceMutex;

        // Public accessor for current window (needed for dialogs/pickers in pages)
        public Window CurrentWindow
        {
            get => m_window;
            set => m_window = value;
        }

        public App()
        {
            this.InitializeComponent();
            this.UnhandledException += App_UnhandledException;
            RequestedTheme = ApplicationTheme.Light;
            Services = ConfigureServices();
        }

        private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"UNHANDLED EXCEPTION: {e.Message}");
            System.Diagnostics.Debug.WriteLine($"Exception: {e.Exception}");
            System.Diagnostics.Debug.WriteLine($"StackTrace: {e.Exception?.StackTrace}");
            e.Handled = false; // Let it crash to see the error
        }

        protected override async void OnLaunched(LaunchActivatedEventArgs args)
        {
            // Single instance protection - only one instance of the app can run at a time
            bool createdNew;
            _singleInstanceMutex = new Mutex(true, "Sklad_2_SingleInstance_Mutex", out createdNew);

            if (!createdNew)
            {
                // Another instance is already running
                System.Diagnostics.Debug.WriteLine("[App] Another instance is already running. Exiting.");

                // Show Win32 MessageBox (works before WinUI is fully initialized)
                MessageBox(
                    IntPtr.Zero,
                    "Sklad 2 je již spuštěn.\n\nMůže běžet pouze jedna instance aplikace.",
                    "Aplikace již běží",
                    MB_OK | MB_ICONWARNING
                );

                // Release mutex and exit
                _singleInstanceMutex?.Close();
                _singleInstanceMutex = null;
                Environment.Exit(0);
                return;
            }

            System.Diagnostics.Debug.WriteLine("[App] Single instance mutex acquired successfully.");

            var settingsService = Services.GetRequiredService<ISettingsService>();
            await settingsService.LoadSettingsAsync();

            // Run database migrations BEFORE anything else
            var migrationService = Services.GetRequiredService<IDatabaseMigrationService>();
            var migrationSuccess = await migrationService.MigrateToLatestAsync();

            if (!migrationSuccess)
            {
                // Show error dialog and exit
                var errorDialog = new Microsoft.UI.Xaml.Controls.ContentDialog()
                {
                    Title = "Chyba databáze",
                    Content = "Nepodařilo se aktualizovat databázi na nejnovější verzi. Aplikace se ukončí.",
                    CloseButtonText = "OK"
                };

                // Create a temporary window to show the dialog
                var tempWindow = new Microsoft.UI.Xaml.Window();
                tempWindow.Activate();
                errorDialog.XamlRoot = tempWindow.Content.XamlRoot;
                await errorDialog.ShowAsync();

                Environment.Exit(1);
                return;
            }

            // Show the LoginWindow first
            m_window = new LoginWindow();
            m_window.Activate();

            // m_window = new MainWindow();
            // m_window.Activate();
        }

        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            // Core
            services.AddDbContextFactory<DatabaseContext>();

            // Services
            services.AddSingleton<ISettingsService, SettingsService>();
            services.AddSingleton<IDatabaseMigrationService, DatabaseMigrationService>();
            services.AddSingleton<IDataService, SqliteDataService>();
            services.AddSingleton<IReceiptService, ReceiptService>();
            services.AddSingleton<IPrintService, EscPosPrintService>(); // Epson TM-T20III (ESC/POS)
            services.AddSingleton<IDailyCloseService, DailyCloseService>();
            services.AddSingleton<IGiftCardService, GiftCardService>();
            services.AddSingleton<IUpdateService, UpdateService>();
            services.AddSingleton<IProductImageService, ProductImageService>();
            services.AddSingleton<IAuthService>(sp => new AuthService(
                sp.GetRequiredService<IMessenger>(),
                sp.GetRequiredService<IDataService>()));

            // Messaging
            services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);

            // ViewModels
            services.AddSingleton<ProdejViewModel>(sp => new ProdejViewModel(
                sp.GetRequiredService<IDataService>(),
                sp.GetRequiredService<IReceiptService>(),
                sp.GetRequiredService<ISettingsService>(),
                sp.GetRequiredService<IAuthService>(),
                sp.GetRequiredService<IGiftCardService>(),
                sp.GetRequiredService<IPrintService>(),
                sp.GetRequiredService<IProductImageService>(),
                sp.GetRequiredService<IDbContextFactory<DatabaseContext>>()));
            services.AddSingleton<PrijemZboziViewModel>(sp => new PrijemZboziViewModel(
                sp.GetRequiredService<IDataService>(),
                sp.GetRequiredService<IAuthService>()));
            services.AddSingleton<DatabazeViewModel>(sp => new DatabazeViewModel(
                sp.GetRequiredService<IDataService>(),
                sp.GetRequiredService<IAuthService>(),
                sp.GetRequiredService<ISettingsService>(),
                sp.GetRequiredService<IMessenger>(),
                sp.GetRequiredService<IProductImageService>()));
            services.AddSingleton<NastaveniViewModel>(sp => new NastaveniViewModel(
                sp.GetRequiredService<ISettingsService>(),
                sp.GetRequiredService<IPrintService>(),
                sp.GetRequiredService<IDataService>(),
                sp.GetRequiredService<IMessenger>(),
                sp.GetRequiredService<IAuthService>()));
            services.AddSingleton<UctenkyViewModel>();
            services.AddSingleton<VratkyPrehledViewModel>();
            services.AddSingleton<NovyProduktViewModel>(sp => new NovyProduktViewModel(
                sp.GetRequiredService<IDataService>(),
                sp.GetRequiredService<IAuthService>(),
                sp.GetRequiredService<ISettingsService>(),
                sp.GetRequiredService<IProductImageService>(),
                sp.GetRequiredService<IMessenger>()));
            services.AddSingleton<PrehledProdejuViewModel>(sp => new PrehledProdejuViewModel(
                sp.GetRequiredService<IDataService>(),
                sp.GetRequiredService<ISettingsService>(),
                sp.GetRequiredService<IMessenger>()));
            services.AddSingleton<VratkyViewModel>(sp => new VratkyViewModel(
                sp.GetRequiredService<IDataService>(),
                sp.GetRequiredService<ISettingsService>(),
                sp.GetRequiredService<IMessenger>(),
                sp.GetRequiredService<IAuthService>(),
                sp.GetRequiredService<IDbContextFactory<DatabaseContext>>()));
            services.AddSingleton<TrzbyUzavirkViewModel>(sp => new TrzbyUzavirkViewModel(
                sp.GetRequiredService<IDailyCloseService>(),
                sp.GetRequiredService<IAuthService>(),
                sp.GetRequiredService<ISettingsService>(),
                sp.GetRequiredService<IMessenger>()));
            services.AddSingleton<StatusBarViewModel>(sp => new StatusBarViewModel(
                sp.GetRequiredService<IDataService>(),
                sp.GetRequiredService<ISettingsService>(),
                sp.GetRequiredService<IPrintService>(),
                sp.GetRequiredService<IDailyCloseService>(),
                sp.GetRequiredService<IMessenger>()));
            services.AddSingleton<BrandManagementViewModel>(sp => new BrandManagementViewModel(
                sp.GetRequiredService<IDataService>()));
            services.AddSingleton<CategoryManagementViewModel>();
            services.AddSingleton<UserManagementViewModel>(sp => new UserManagementViewModel(
                sp.GetRequiredService<IDataService>(),
                sp.GetRequiredService<IAuthService>()));
            services.AddSingleton<SkladPrehledViewModel>(sp => new SkladPrehledViewModel(
                sp.GetRequiredService<IDataService>()));
            services.AddSingleton<PoukazyViewModel>(sp => new PoukazyViewModel(
                sp.GetRequiredService<IGiftCardService>()));
            services.AddSingleton<LoyaltyViewModel>(sp => new LoyaltyViewModel(
                sp.GetRequiredService<IDbContextFactory<DatabaseContext>>(),
                sp.GetRequiredService<IAuthService>()));

            // Transient ViewModels (for dialogs, login, etc.)
            services.AddTransient<LoginViewModel>(sp => new LoginViewModel(
                sp.GetRequiredService<IDataService>(),
                sp.GetRequiredService<IAuthService>()));

            return services.BuildServiceProvider();
        }

        // Cleanup: Release mutex when app exits
        ~App()
        {
            _singleInstanceMutex?.ReleaseMutex();
            _singleInstanceMutex?.Dispose();
        }
    }
}

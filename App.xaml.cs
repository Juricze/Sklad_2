using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Sklad_2.Data;
using Sklad_2.Services;
using Sklad_2.ViewModels;
using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;

namespace Sklad_2
{
    public partial class App : Application
    {
        public IServiceProvider Services { get; }
        private Window m_window;

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

            // Restore from backup if newer version exists
            await RestoreFromBackupIfNewerAsync(settingsService);

            // Show the LoginWindow first
            m_window = new LoginWindow();
            m_window.Activate();

            // m_window = new MainWindow();
            // m_window.Activate();
        }

        private async Task RestoreFromBackupIfNewerAsync(ISettingsService settingsService)
        {
            try
            {
                var appDataPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData);
                var localFolderPath = System.IO.Path.Combine(appDataPath, "Sklad_2_Data");
                var localDbPath = System.IO.Path.Combine(localFolderPath, "sklad.db");

                // Only check backup if backup path is configured
                if (!settingsService.IsBackupPathConfigured())
                {
                    return; // No backup path configured
                }

                var backupFolderPath = settingsService.GetBackupFolderPath();
                var backupDbPath = System.IO.Path.Combine(backupFolderPath, "sklad.db");

                if (!System.IO.File.Exists(backupDbPath))
                {
                    return; // No backup exists yet
                }

                // Compare modification times
                var localLastWrite = System.IO.File.Exists(localDbPath)
                    ? System.IO.File.GetLastWriteTime(localDbPath)
                    : DateTime.MinValue;
                var backupLastWrite = System.IO.File.GetLastWriteTime(backupDbPath);

                // If backup version is newer, restore it
                if (backupLastWrite > localLastWrite)
                {
                    System.IO.Directory.CreateDirectory(localFolderPath);
                    System.IO.File.Copy(backupDbPath, localDbPath, true);

                    // Also restore settings if they exist
                    var backupSettingsPath = System.IO.Path.Combine(backupFolderPath, "AppSettings.json");
                    var localSettingsPath = System.IO.Path.Combine(localFolderPath, "AppSettings.json");
                    if (System.IO.File.Exists(backupSettingsPath))
                    {
                        System.IO.File.Copy(backupSettingsPath, localSettingsPath, true);
                    }
                }
            }
            catch
            {
                // Silent fail - don't block app startup
            }

            await Task.CompletedTask;
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
            services.AddSingleton<IPrintService, PrintService>();
            services.AddSingleton<ICashRegisterService, CashRegisterService>();
            services.AddSingleton<IGiftCardService, GiftCardService>();
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
                sp.GetRequiredService<ICashRegisterService>(),
                sp.GetRequiredService<IAuthService>(),
                sp.GetRequiredService<IGiftCardService>()));
            services.AddSingleton<PrijemZboziViewModel>(sp => new PrijemZboziViewModel(
                sp.GetRequiredService<IDataService>(),
                sp.GetRequiredService<IAuthService>()));
            services.AddSingleton<DatabazeViewModel>(sp => new DatabazeViewModel(
                sp.GetRequiredService<IDataService>(),
                sp.GetRequiredService<IAuthService>(),
                sp.GetRequiredService<ISettingsService>(),
                sp.GetRequiredService<IMessenger>()));
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
                sp.GetRequiredService<IMessenger>()));
            services.AddSingleton<PrehledProdejuViewModel>(sp => new PrehledProdejuViewModel(
                sp.GetRequiredService<IDataService>(),
                sp.GetRequiredService<ISettingsService>(),
                sp.GetRequiredService<IMessenger>()));
            services.AddSingleton<VratkyViewModel>(sp => new VratkyViewModel(
                sp.GetRequiredService<IDataService>(),
                sp.GetRequiredService<ISettingsService>(),
                sp.GetRequiredService<ICashRegisterService>(),
                sp.GetRequiredService<IMessenger>(),
                sp.GetRequiredService<IAuthService>()));
            services.AddSingleton<CashRegisterViewModel>(sp => new CashRegisterViewModel(
                sp.GetRequiredService<ICashRegisterService>(),
                sp.GetRequiredService<IAuthService>(),
                sp.GetRequiredService<IMessenger>()));
            services.AddSingleton<CashRegisterHistoryViewModel>();
            services.AddSingleton<StatusBarViewModel>(sp => new StatusBarViewModel(
                sp.GetRequiredService<IDataService>(),
                sp.GetRequiredService<ISettingsService>(),
                sp.GetRequiredService<IPrintService>(),
                sp.GetRequiredService<IMessenger>()));
            services.AddSingleton<CategoryManagementViewModel>();
            services.AddSingleton<UserManagementViewModel>(sp => new UserManagementViewModel(
                sp.GetRequiredService<IDataService>()));
            services.AddSingleton<SkladPrehledViewModel>(sp => new SkladPrehledViewModel(
                sp.GetRequiredService<IDataService>()));
            services.AddSingleton<PoukazyViewModel>(sp => new PoukazyViewModel(
                sp.GetRequiredService<IGiftCardService>()));

            // Transient ViewModels (for dialogs, login, etc.)
            services.AddTransient<LoginViewModel>(sp => new LoginViewModel(
                sp.GetRequiredService<IDataService>(),
                sp.GetRequiredService<IAuthService>()));

            return services.BuildServiceProvider();
        }
    }
}

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

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            var settingsService = Services.GetRequiredService<ISettingsService>();


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
            services.AddSingleton<IDataService, SqliteDataService>();
            services.AddSingleton<IReceiptService, ReceiptService>();
            services.AddSingleton<IPrintService, PrintService>();
            services.AddSingleton<ICashRegisterService, CashRegisterService>();
            services.AddSingleton<IAuthService, AuthService>();

            // Messaging
            services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);

            // ViewModels
            services.AddSingleton<ProdejViewModel>();
            services.AddSingleton<PrijemZboziViewModel>();
            services.AddSingleton<DatabazeViewModel>();
            services.AddSingleton<NastaveniViewModel>();
            services.AddSingleton<UctenkyViewModel>();
            services.AddSingleton<VratkyPrehledViewModel>();
            services.AddSingleton<NovyProduktViewModel>(sp => new NovyProduktViewModel(
                sp.GetRequiredService<IDataService>(),
                sp.GetRequiredService<IAuthService>(),
                sp.GetRequiredService<IMessenger>()));
            services.AddSingleton<PrehledProdejuViewModel>();
            services.AddSingleton<VratkyViewModel>();
            services.AddSingleton<CashRegisterViewModel>(sp => new CashRegisterViewModel(
                sp.GetRequiredService<ICashRegisterService>(),
                sp.GetRequiredService<IAuthService>(),
                sp.GetRequiredService<IMessenger>()));
            services.AddSingleton<CashRegisterHistoryViewModel>();
            services.AddSingleton<StatusBarViewModel>();

            // Transient ViewModels (for dialogs, login, etc.)
            services.AddTransient<LoginViewModel>();

            return services.BuildServiceProvider();
        }
    }
}

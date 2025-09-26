using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Sklad_2.Data;
using Sklad_2.Services;
using Sklad_2.ViewModels;
using System;
using System.Threading.Tasks;

namespace Sklad_2
{
    public partial class App : Application
    {
        public IServiceProvider Services { get; }
        private MainWindow m_window;

        public App()
        {
            this.InitializeComponent();
            RequestedTheme = ApplicationTheme.Light;
            Services = ConfigureServices();
        }

        protected override async void OnLaunched(LaunchActivatedEventArgs args)
        {
            var settingsService = Services.GetRequiredService<ISettingsService>();
            await settingsService.LoadSettingsAsync();

            m_window = new MainWindow();
            m_window.Activate();
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

            // ViewModels
            services.AddSingleton<ProdejViewModel>();
            services.AddSingleton<PrijemZboziViewModel>();
            services.AddSingleton<DatabazeViewModel>();
            services.AddSingleton<NastaveniViewModel>();
            services.AddSingleton<UctenkyViewModel>();
            services.AddSingleton<VratkyPrehledViewModel>();
            services.AddSingleton<NovyProduktViewModel>();
            services.AddSingleton<PrehledProdejuViewModel>();
            services.AddSingleton<VratkyViewModel>();
            services.AddSingleton<CashRegisterViewModel>();
            services.AddSingleton<CashRegisterHistoryViewModel>();

            return services.BuildServiceProvider();
        }
    }
}

using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Sklad_2.Data;
using Sklad_2.Services;
using Sklad_2.ViewModels;
using System;

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

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            m_window = new MainWindow();
            m_window.Activate();
        }

        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            // Core
            services.AddSingleton<DatabaseContext>();

            // Services
            services.AddSingleton<IDataService, SqliteDataService>();
            services.AddSingleton<IReceiptService, ReceiptService>();
            services.AddSingleton<IPrintService, PrintService>();

            // ViewModels
            services.AddSingleton<ProdejViewModel>();
            services.AddSingleton<NaskladneniViewModel>();
            services.AddSingleton<DatabazeViewModel>();
            services.AddSingleton<NastaveniViewModel>();

            return services.BuildServiceProvider();
        }
    }
}

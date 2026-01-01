using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Sklad_2.Services;
using Sklad_2.ViewModels;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Sklad_2.Views
{
    public sealed partial class TrzbyUzavirkPage : Page
    {
        public TrzbyUzavirkViewModel ViewModel { get; }

        public TrzbyUzavirkPage()
        {
            var app = Application.Current as App;
            ViewModel = app.Services.GetService(typeof(TrzbyUzavirkViewModel)) as TrzbyUzavirkViewModel;

            this.InitializeComponent();

            // Set DataContext for classic {Binding} to work
            this.DataContext = ViewModel;

            this.Loaded += TrzbyUzavirkPage_Loaded;
        }

        private async void TrzbyUzavirkPage_Loaded(object sender, RoutedEventArgs e)
        {
            // KRITICKÉ: Refresh data při každém zobrazení stránky
            // (když se uživatel vrátí ze stránky Prodej po stornu, data se aktualizují)
            await LoadDataAsync();
        }

        protected override async void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // Auto-refresh při každé navigaci na tuto stránku
            // Zajistí aktualizaci po stornu/vratce/prodeji
            Debug.WriteLine("TrzbyUzavirkPage: OnNavigatedTo - refreshing data");
            await LoadDataAsync();
        }

        private async System.Threading.Tasks.Task LoadDataAsync()
        {
            // Data binding automatically updates UI when ViewModel properties change
            await ViewModel.LoadTodaySalesAsync();

            // Načíst data pro aktuálně vybraný rok/měsíc
            // (při prvním spuštění to bude Today.Year a Today.Month díky defaultu ve ViewModel)
            await ViewModel.LoadDailySalesSummariesAsync(
                ViewModel.SelectedYear,
                ViewModel.SelectedMonth);
        }

        private async void CloseDayButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new ContentDialog
                {
                    Title = "Uzavřít den",
                    Content = $"Opravdu chcete uzavřít den {ViewModel.SessionDate:dd.MM.yyyy}?\n\n" +
                              $"Hotovost: {ViewModel.TodayCashSalesFormatted}\n" +
                              $"Karta: {ViewModel.TodayCardSalesFormatted}\n" +
                              $"Celkem: {ViewModel.TodayTotalSalesFormatted}\n\n" +
                              $"Po uzavření nebude možné provádět prodeje a vratky pro tento den.",
                    PrimaryButtonText = "Uzavřít",
                    CloseButtonText = "Zrušit",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = this.XamlRoot
                };

                var result = await dialog.ShowAsync();

                if (result == ContentDialogResult.Primary)
                {
                    var (success, message, dailyClose) = await ViewModel.CloseTodayAsync();

                    if (success)
                    {
                        var successDialog = new ContentDialog
                        {
                            Title = "✅ Den uzavřen",
                            Content = $"Den {ViewModel.SessionDate:dd.MM.yyyy} byl úspěšně uzavřen!\n\n{message}",
                            CloseButtonText = "OK",
                            XamlRoot = this.XamlRoot
                        };

                        await successDialog.ShowAsync();
                        await Task.Delay(300); // Dialog close delay
                        await LoadDataAsync();

                        // Pokud aktuální datum je VĚTŠÍ než session datum → nabídnout zahájení nového dne
                        var today = DateTime.Today;
                        if (today > ViewModel.SessionDate)
                        {
                            var newDayDialog = new ContentDialog
                            {
                                Title = "📅 Zahájit nový den?",
                                Content = $"Uzavřený den: {ViewModel.SessionDate:dd.MM.yyyy}\n" +
                                         $"Aktuální datum: {today:dd.MM.yyyy}\n\n" +
                                         $"Chcete nyní zahájit nový obchodní den pro {today:dd.MM.yyyy}?",
                                PrimaryButtonText = "Ano, zahájit nový den",
                                CloseButtonText = "Ne, zahájím později",
                                DefaultButton = ContentDialogButton.Primary,
                                XamlRoot = this.XamlRoot
                            };

                            var newDayResult = await newDayDialog.ShowAsync();

                            if (newDayResult == ContentDialogResult.Primary)
                            {
                                // Zahájit nový den - aktualizovat LastSaleLoginDate
                                var app = Application.Current as App;
                                var settingsService = app.Services.GetService(typeof(ISettingsService)) as ISettingsService;

                                settingsService.CurrentSettings.LastSaleLoginDate = today;
                                await settingsService.SaveSettingsAsync();
                                await Task.Delay(200); // Win10 file flush + settings propagation

                                var confirmDialog = new ContentDialog
                                {
                                    Title = "✅ Nový den zahájen",
                                    Content = $"Nový obchodní den {today:dd.MM.yyyy} byl úspěšně zahájen.\n\n" +
                                             "Můžete nyní provádět prodeje a vratky.",
                                    CloseButtonText = "OK",
                                    XamlRoot = this.XamlRoot
                                };

                                await confirmDialog.ShowAsync();
                                await Task.Delay(300); // Dialog close delay

                                // Explicitly reload settings to ensure ViewModel sees new date
                                await settingsService.LoadSettingsAsync();
                                await Task.Delay(300); // Win10 settings reload + file flush

                                // Notify ViewModels and refresh UI (pass new session date explicitly)
                                await ViewModel.NotifyNewDayStartedAsync(today);
                                await LoadDataAsync();
                                await Task.Delay(200); // Win10 UI propagation
                                await LoadDataAsync(); // Second refresh for Win10 reliability
                            }
                            else
                            {
                                var infoDialog = new ContentDialog
                                {
                                    Title = "ℹ️ Informace",
                                    Content = "Pro zahájení nového dne se odhlaste a znovu přihlaste.",
                                    CloseButtonText = "Rozumím",
                                    XamlRoot = this.XamlRoot
                                };

                                await infoDialog.ShowAsync();
                            }
                        }
                    }
                    else
                    {
                        var errorDialog = new ContentDialog
                        {
                            Title = "❌ Chyba",
                            Content = message,
                            CloseButtonText = "OK",
                            XamlRoot = this.XamlRoot
                        };

                        await errorDialog.ShowAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TrzbyUzavirkPage: Error closing day: {ex.Message}");
            }
        }

        /// <summary>
        /// Jednotný export handler (NEW UX - Týdenní/Měsíční/Čtvrtletní/Půlroční/Roční)
        /// </summary>
        private async void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var (success, filePath, errorMessage) = await ViewModel.ExportClosesAsync();

                if (success)
                {
                    var dialog = new ContentDialog
                    {
                        Title = "✅ Export úspěšný",
                        Content = $"Uzavírky byly exportovány do:\n\n{filePath}\n\n" +
                                 "Soubor byl otevřen v prohlížeči.\n" +
                                 "Můžete jej vytisknout nebo uložit jako PDF (Ctrl+P → Uložit jako PDF).",
                        CloseButtonText = "OK",
                        XamlRoot = this.XamlRoot
                    };

                    await dialog.ShowAsync();
                }
                else
                {
                    var dialog = new ContentDialog
                    {
                        Title = "❌ Chyba exportu",
                        Content = errorMessage,
                        CloseButtonText = "OK",
                        XamlRoot = this.XamlRoot
                    };

                    await dialog.ShowAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TrzbyUzavirkPage: Error exporting: {ex.Message}");
            }
        }
    }
}

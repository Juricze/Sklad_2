using Microsoft.Extensions.DependencyInjection;
using Sklad_2.Services;
using System.Collections.Generic;
using System.Linq;

namespace Sklad_2.Models
{
    public static class ProductCategories
    {
        // Dynamic loading from AppSettings
        public static List<string> All
        {
            get
            {
                try
                {
                    var app = Microsoft.UI.Xaml.Application.Current as App;
                    if (app?.Services != null)
                    {
                        var settingsService = app.Services.GetService<ISettingsService>();
                        if (settingsService?.CurrentSettings?.Categories != null && settingsService.CurrentSettings.Categories.Any())
                        {
                            return settingsService.CurrentSettings.Categories.OrderBy(c => c).ToList();
                        }
                    }
                }
                catch
                {
                    // Fallback to default if service unavailable
                }

                // Default categories (fallback) - alphabetically sorted
                return new List<string>
                {
                    "Drogerie",
                    "Elektronika",
                    "Ostatní",
                    "Potraviny"
                };
            }
        }
    }
}

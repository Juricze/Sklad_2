using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;
using Sklad_2.Services;
using System;

namespace Sklad_2.Converters
{
    /// <summary>
    /// Converts a product EAN to its thumbnail image.
    /// </summary>
    public class EanToThumbnailConverter : IValueConverter
    {
        private static IProductImageService _imageService;

        private static IProductImageService ImageService
        {
            get
            {
                if (_imageService == null)
                {
                    _imageService = ((App)Application.Current).Services.GetRequiredService<IProductImageService>();
                }
                return _imageService;
            }
        }

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is string ean && !string.IsNullOrEmpty(ean))
            {
                return ImageService.GetThumbnail(ean);
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}

using Microsoft.UI.Xaml.Media.Imaging;
using System.Threading.Tasks;
using Windows.Storage;

namespace Sklad_2.Services
{
    public interface IProductImageService
    {
        /// <summary>
        /// Saves an image for a product. Resizes to max 800x800, creates thumbnail 80x80.
        /// </summary>
        /// <param name="ean">Product EAN code</param>
        /// <param name="sourceFile">Source image file</param>
        /// <returns>Filename of saved image (e.g., "123456789.jpg")</returns>
        Task<string> SaveImageAsync(string ean, StorageFile sourceFile);

        /// <summary>
        /// Deletes product image and its thumbnail.
        /// </summary>
        /// <param name="ean">Product EAN code</param>
        void DeleteImage(string ean);

        /// <summary>
        /// Gets the main product image (800x800).
        /// </summary>
        /// <param name="ean">Product EAN code</param>
        /// <returns>BitmapImage or null if no image exists</returns>
        BitmapImage GetImage(string ean);

        /// <summary>
        /// Gets the thumbnail image (80x80).
        /// </summary>
        /// <param name="ean">Product EAN code</param>
        /// <returns>BitmapImage or null if no image exists</returns>
        BitmapImage GetThumbnail(string ean);

        /// <summary>
        /// Checks if a product has an image.
        /// </summary>
        /// <param name="ean">Product EAN code</param>
        /// <returns>True if image exists</returns>
        bool HasImage(string ean);

        /// <summary>
        /// Gets the path to the ProductImages folder.
        /// </summary>
        string GetImagesFolderPath();
    }
}

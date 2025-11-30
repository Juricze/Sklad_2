using Microsoft.UI.Xaml.Media.Imaging;
using SkiaSharp;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;

namespace Sklad_2.Services
{
    public class ProductImageService : IProductImageService
    {
        private const int MAX_IMAGE_SIZE = 1600;  // Zvětšeno z 800 pro lepší kvalitu v detail panelu
        private const int THUMBNAIL_SIZE = 80;
        private const int JPEG_QUALITY = 100;

        private readonly string _imagesFolderPath;

        public ProductImageService()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _imagesFolderPath = Path.Combine(appDataPath, "Sklad_2_Data", "ProductImages");

            // Ensure folder exists
            Directory.CreateDirectory(_imagesFolderPath);
        }

        public string GetImagesFolderPath() => _imagesFolderPath;

        public async Task<string> SaveImageAsync(string ean, StorageFile sourceFile)
        {
            try
            {
                // Read source file into memory
                using var stream = await sourceFile.OpenStreamForReadAsync();
                using var originalBitmap = SKBitmap.Decode(stream);

                if (originalBitmap == null)
                {
                    Debug.WriteLine($"ProductImageService: Failed to decode image for {ean}");
                    return null;
                }

                // Process and save main image (max 800x800)
                var mainFilename = $"{ean}.jpg";
                var mainPath = Path.Combine(_imagesFolderPath, mainFilename);
                SaveResizedImage(originalBitmap, mainPath, MAX_IMAGE_SIZE);

                // Process and save thumbnail (80x80)
                var thumbFilename = $"{ean}_thumb.jpg";
                var thumbPath = Path.Combine(_imagesFolderPath, thumbFilename);
                SaveResizedImage(originalBitmap, thumbPath, THUMBNAIL_SIZE);

                Debug.WriteLine($"ProductImageService: Saved images for {ean}");
                return mainFilename;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ProductImageService: Error saving image for {ean}: {ex.Message}");
                return null;
            }
        }

        private void SaveResizedImage(SKBitmap original, string outputPath, int maxSize)
        {
            // Calculate new dimensions maintaining aspect ratio
            int newWidth, newHeight;
            if (original.Width > original.Height)
            {
                newWidth = Math.Min(original.Width, maxSize);
                newHeight = (int)((float)original.Height / original.Width * newWidth);
            }
            else
            {
                newHeight = Math.Min(original.Height, maxSize);
                newWidth = (int)((float)original.Width / original.Height * newHeight);
            }

            // Create canvas with white background
            using var surface = SKSurface.Create(new SKImageInfo(maxSize, maxSize));
            var canvas = surface.Canvas;
            canvas.Clear(SKColors.White);

            // Resize the original image
            using var resizedBitmap = original.Resize(new SKImageInfo(newWidth, newHeight), SKFilterQuality.High);

            // Calculate position to center the image
            int x = (maxSize - newWidth) / 2;
            int y = (maxSize - newHeight) / 2;

            // Draw resized image centered
            canvas.DrawBitmap(resizedBitmap, x, y);

            // Save as JPEG with 100% quality
            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, JPEG_QUALITY);
            using var fileStream = File.OpenWrite(outputPath);
            data.SaveTo(fileStream);
        }

        public void DeleteImage(string ean)
        {
            try
            {
                var mainPath = Path.Combine(_imagesFolderPath, $"{ean}.jpg");
                var thumbPath = Path.Combine(_imagesFolderPath, $"{ean}_thumb.jpg");

                if (File.Exists(mainPath))
                {
                    File.Delete(mainPath);
                    Debug.WriteLine($"ProductImageService: Deleted main image for {ean}");
                }

                if (File.Exists(thumbPath))
                {
                    File.Delete(thumbPath);
                    Debug.WriteLine($"ProductImageService: Deleted thumbnail for {ean}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ProductImageService: Error deleting images for {ean}: {ex.Message}");
            }
        }

        public BitmapImage GetImage(string ean)
        {
            return LoadBitmapImage($"{ean}.jpg");
        }

        public BitmapImage GetThumbnail(string ean)
        {
            return LoadBitmapImage($"{ean}_thumb.jpg");
        }

        private BitmapImage LoadBitmapImage(string filename)
        {
            try
            {
                var path = Path.Combine(_imagesFolderPath, filename);
                if (!File.Exists(path))
                {
                    return null;
                }

                var bitmap = new BitmapImage();
                bitmap.UriSource = new Uri(path);
                return bitmap;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ProductImageService: Error loading image {filename}: {ex.Message}");
                return null;
            }
        }

        public bool HasImage(string ean)
        {
            var mainPath = Path.Combine(_imagesFolderPath, $"{ean}.jpg");
            return File.Exists(mainPath);
        }
    }
}

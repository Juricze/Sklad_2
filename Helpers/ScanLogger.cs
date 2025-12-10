using System;
using System.IO;
using System.Threading;

namespace Sklad_2.Helpers
{
    /// <summary>
    /// Thread-safe logger for barcode scanning diagnostics.
    /// Logs to: C:\Users\{User}\AppData\Local\Sklad_2_Data\scan_log.txt
    /// </summary>
    public static class ScanLogger
    {
        private static readonly string LogPath;
        private static readonly object _lock = new object();
        private const int MAX_LINES = 5000;

        static ScanLogger()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dataFolderPath = Path.Combine(appDataPath, "Sklad_2_Data");
            Directory.CreateDirectory(dataFolderPath);
            LogPath = Path.Combine(dataFolderPath, "scan_log.txt");
        }

        /// <summary>
        /// Log scan input (what scanner sent)
        /// </summary>
        public static void LogScan(string ean, int length)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var message = $"========================================\n" +
                         $"{timestamp}\n" +
                         $"========================================\n" +
                         $"[SCAN]  Input EAN: '{ean}' (length={length}, delay=300ms)\n";
            WriteToFile(message);
        }

        /// <summary>
        /// Log database query result
        /// </summary>
        public static void LogDatabase(Sklad_2.Models.Product product, string searchedEan)
        {
            string message;
            if (product != null)
            {
                message = $"[DB]    Found: {product.Name} (EAN: {product.Ean}) | Price: {product.SalePrice:F2} Kč | Stock: {product.StockQuantity} ks\n";
            }
            else
            {
                message = $"[DB]    NOT FOUND - no product with EAN '{searchedEan}'\n" +
                         $"[ERROR] Product not found in database\n";
            }
            WriteToFile(message);
        }

        /// <summary>
        /// Log cart operation (add new or increment)
        /// </summary>
        public static void LogCartAdd(string productName, decimal unitPrice, int qty, decimal totalPrice)
        {
            var message = $"[CART]  Added to cart: {productName} | UnitPrice: {unitPrice:F2} Kč | Qty: {qty} | TotalPrice: {totalPrice:F2} Kč\n" +
                         $"----------------------------------------\n\n";
            WriteToFile(message);
        }

        /// <summary>
        /// Log cart increment operation
        /// </summary>
        public static void LogCartIncrement(string productName, int oldQty, int newQty, decimal unitPrice, decimal totalPrice)
        {
            var message = $"[CART]  Incremented qty: {productName} | Qty: {oldQty} → {newQty} | UnitPrice: {unitPrice:F2} Kč | TotalPrice: {totalPrice:F2} Kč\n" +
                         $"----------------------------------------\n\n";
            WriteToFile(message);
        }

        /// <summary>
        /// Thread-safe write to log file with auto-rotation
        /// </summary>
        private static void WriteToFile(string message)
        {
            try
            {
                lock (_lock)
                {
                    // Check if rotation needed (file too large)
                    if (File.Exists(LogPath))
                    {
                        var lineCount = File.ReadAllLines(LogPath).Length;
                        if (lineCount > MAX_LINES)
                        {
                            // Keep only last 2500 lines
                            var lines = File.ReadAllLines(LogPath);
                            var keepLines = lines.Length - 2500;
                            if (keepLines > 0)
                            {
                                var remainingLines = new string[lines.Length - keepLines];
                                Array.Copy(lines, keepLines, remainingLines, 0, remainingLines.Length);
                                File.WriteAllLines(LogPath, remainingLines);
                            }
                        }
                    }

                    // Append message
                    File.AppendAllText(LogPath, message);
                }
            }
            catch (Exception ex)
            {
                // Logging failure should not crash the app
                System.Diagnostics.Debug.WriteLine($"ScanLogger ERROR: {ex.Message}");
            }
        }
    }
}

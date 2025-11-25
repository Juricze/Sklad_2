using ESCPOS_NET;
using ESCPOS_NET.Emitters;
using ESCPOS_NET.Utilities;
using ESCPOS_NET.Printers;
using Sklad_2.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Sklad_2.Services
{
    /// <summary>
    /// ESC/POS Print Service for Epson TM-T20III (80mm thermal printer)
    /// Supports both Windows DirectPrint and Serial (COM port) connections
    /// </summary>
    public class EscPosPrintService : IPrintService
    {
        private readonly ISettingsService _settingsService;

        public EscPosPrintService(ISettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        public async Task<bool> PrintReceiptAsync(Receipt receipt)
        {
            try
            {
                using var printer = CreatePrinter();
                if (printer == null)
                {
                    Debug.WriteLine("EscPosPrintService: Failed to create printer connection");
                    return false;
                }

                var e = new EPSON();

                // Build receipt data
                var receiptData = BuildReceiptData(e, receipt);

                // Print (run in background thread to avoid UI blocking)
                await Task.Run(() => printer.Write(receiptData));

                Debug.WriteLine($"EscPosPrintService: Receipt {receipt.FormattedReceiptNumber} printed successfully");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"EscPosPrintService: Print failed: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> TestPrintAsync(string printerPath)
        {
            try
            {
                // Save current path temporarily
                var originalPath = _settingsService.CurrentSettings.PrinterPath;
                _settingsService.CurrentSettings.PrinterPath = printerPath;

                using var printer = CreatePrinter();

                // Restore original path
                _settingsService.CurrentSettings.PrinterPath = originalPath;

                if (printer == null)
                {
                    Debug.WriteLine("EscPosPrintService: Failed to create printer for test");
                    return false;
                }

                var e = new EPSON();

                var connectionType = IsComPort(printerPath) ? $"Serial ({printerPath})" : "USB Direct";

                var testData = ByteSplicer.Combine(
                    e.CenterAlign(),
                    e.SetStyles(PrintStyle.Bold | PrintStyle.DoubleHeight),
                    e.PrintLine("TEST TISKU"),
                    e.SetStyles(PrintStyle.None),
                    e.PrintLine(""),
                    e.LeftAlign(),
                    e.PrintLine("Tiskarna je pripojena"),
                    e.PrintLine($"Typ: {connectionType}"),
                    e.PrintLine($"Cas: {DateTime.Now:dd.MM.yyyy HH:mm:ss}"),
                    e.PrintLine(""),
                    e.PrintLine("Ceske znaky: escrzyaie"),
                    e.PrintLine(""),
                    e.CenterAlign(),
                    e.PrintLine("Test uspesny"),
                    e.PrintLine(""),
                    e.PrintLine(""),
                    e.FullCutAfterFeed(3)
                );

                await Task.Run(() => printer.Write(testData));

                Debug.WriteLine($"EscPosPrintService: Test print successful on {printerPath}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"EscPosPrintService: Test print failed: {ex.Message}");
                return false;
            }
        }

        public bool IsPrinterConnected()
        {
            try
            {
                // Try to create printer connection - if successful, printer is connected
                using var printer = CreatePrinter();
                return printer != null;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Creates appropriate printer instance based on configuration
        /// Currently supports SerialPrinter (COM ports) only
        /// </summary>
        private BasePrinter CreatePrinter()
        {
            try
            {
                var printerPath = _settingsService.CurrentSettings.PrinterPath;

                if (string.IsNullOrWhiteSpace(printerPath))
                {
                    Debug.WriteLine("EscPosPrintService: Printer path not configured");
                    return null;
                }

                // Use serial port (COM)
                if (IsComPort(printerPath))
                {
                    Debug.WriteLine($"EscPosPrintService: Using Serial connection on {printerPath}");
                    return new SerialPrinter(portName: printerPath, baudRate: 115200);
                }

                // Invalid path
                Debug.WriteLine($"EscPosPrintService: Invalid printer path: {printerPath}. Please use COM port (e.g., COM1).");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"EscPosPrintService: Failed to create printer: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Checks if the given path is a COM port
        /// </summary>
        private bool IsComPort(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            return path.StartsWith("COM", StringComparison.OrdinalIgnoreCase);
        }

        private byte[] BuildReceiptData(EPSON e, Receipt receipt)
        {
            var commands = new List<byte[]>
            {
                // Header - Shop name and address
                e.CenterAlign(),
                e.SetStyles(PrintStyle.Bold | PrintStyle.FontB),
                e.PrintLine(receipt.ShopName ?? ""),
                e.SetStyles(PrintStyle.None),
                e.PrintLine(receipt.ShopAddress ?? ""),
            };

            // Company ID and VAT ID
            if (!string.IsNullOrWhiteSpace(receipt.CompanyId))
            {
                commands.Add(e.PrintLine($"IČ: {receipt.CompanyId}"));
            }

            if (receipt.IsVatPayer && !string.IsNullOrWhiteSpace(receipt.VatId))
            {
                commands.Add(e.PrintLine($"DIČ: {receipt.VatId}"));
            }

            commands.Add(e.PrintLine(""));

            // Receipt type header
            if (receipt.IsStorno)
            {
                commands.Add(e.SetStyles(PrintStyle.Bold | PrintStyle.DoubleHeight));
                commands.Add(e.PrintLine("❌ STORNO ❌"));
                commands.Add(e.SetStyles(PrintStyle.None));
                if (receipt.OriginalReceiptId.HasValue)
                {
                    commands.Add(e.PrintLine($"Storno účtenky #{receipt.OriginalReceiptId}"));
                }
                commands.Add(e.PrintLine(""));
            }
            else if (receipt.ContainsGiftCardSale && receipt.GiftCardSaleAmount > 0)
            {
                commands.Add(e.SetStyles(PrintStyle.Bold));
                commands.Add(e.PrintLine("🎁 DÁRKOVÝ POUKAZ 🎁"));
                commands.Add(e.SetStyles(PrintStyle.None));
                commands.Add(e.PrintLine(""));
            }

            // Receipt number and date
            commands.Add(e.LeftAlign());
            commands.Add(e.PrintLine($"Účtenka: {receipt.FormattedReceiptNumber}"));
            commands.Add(e.PrintLine($"Datum: {receipt.SaleDate:dd.MM.yyyy HH:mm}"));
            commands.Add(e.PrintLine($"Prodejce: {receipt.SellerName}"));
            commands.Add(e.PrintLine("================================"));

            // Items
            if (receipt.Items != null && receipt.Items.Count > 0)
            {
                foreach (var item in receipt.Items)
                {
                    commands.Add(e.PrintLine(item.ProductName ?? ""));

                    // Show discount if applicable
                    if (item.HasDiscount)
                    {
                        commands.Add(e.PrintLine(
                            $"  {item.Quantity}x {item.OriginalUnitPrice:N2} Kč " +
                            $"{item.DiscountPercentFormatted}"
                        ));
                        commands.Add(e.PrintLine(
                            $"  Po slevě: {item.UnitPrice:N2} Kč ... {item.TotalPrice:N2} Kč"
                        ));
                    }
                    else
                    {
                        commands.Add(e.PrintLine(
                            $"  {item.Quantity}x {item.UnitPrice:N2} Kč ... {item.TotalPrice:N2} Kč"
                        ));
                    }
                }
            }

            commands.Add(e.PrintLine("================================"));

            // Gift card redemption (used as payment)
            if (receipt.ContainsGiftCardRedemption && receipt.GiftCardRedemptionAmount > 0)
            {
                commands.Add(e.PrintLine(""));
                commands.Add(e.PrintLine($"Mezisoučet: {receipt.TotalAmount:N2} Kč"));
                commands.Add(e.SetStyles(PrintStyle.Bold));
                commands.Add(e.PrintLine($"Použitý poukaz: -{receipt.GiftCardRedemptionAmount:N2} Kč"));
                commands.Add(e.SetStyles(PrintStyle.None));
            }

            // VAT breakdown (only for VAT payers)
            if (receipt.IsVatPayer && receipt.Items != null && receipt.Items.Count > 0)
            {
                commands.Add(e.PrintLine(""));
                commands.Add(e.PrintLine("DPH:"));

                // Group items by VAT rate
                var vatGroups = receipt.Items
                    .GroupBy(item => item.VatRate)
                    .OrderBy(g => g.Key);

                foreach (var group in vatGroups)
                {
                    var vatRate = group.Key;
                    var totalVatAmount = group.Sum(item => item.VatAmount);
                    var totalWithoutVat = group.Sum(item => item.PriceWithoutVat);

                    commands.Add(e.PrintLine($"  Základ {vatRate}%: {totalWithoutVat:N2} Kč"));
                    commands.Add(e.PrintLine($"  DPH {vatRate}%: {totalVatAmount:N2} Kč"));
                }

                commands.Add(e.PrintLine(""));
                commands.Add(e.PrintLine($"Celkem bez DPH: {receipt.TotalAmountWithoutVat:N2} Kč"));
                commands.Add(e.PrintLine($"Celkem DPH: {receipt.TotalVatAmount:N2} Kč"));
            }

            // Total amount
            commands.Add(e.PrintLine(""));
            commands.Add(e.SetStyles(PrintStyle.Bold | PrintStyle.DoubleHeight));

            if (receipt.ContainsGiftCardRedemption && receipt.GiftCardRedemptionAmount > 0)
            {
                commands.Add(e.PrintLine($"K ÚHRADĚ: {receipt.AmountToPay:N2} Kč"));
            }
            else
            {
                commands.Add(e.PrintLine($"CELKEM: {receipt.TotalAmount:N2} Kč"));
            }

            commands.Add(e.SetStyles(PrintStyle.None));

            // Payment method
            commands.Add(e.PrintLine(""));
            commands.Add(e.PrintLine($"Platba: {receipt.PaymentMethod}"));

            // Received amount and change (for cash payments)
            if (receipt.ReceivedAmount > 0)
            {
                commands.Add(e.PrintLine($"Přijato: {receipt.ReceivedAmount:N2} Kč"));
                if (receipt.ChangeAmount > 0)
                {
                    commands.Add(e.PrintLine($"Vráceno: {receipt.ChangeAmount:N2} Kč"));
                }
            }

            // Footer
            commands.Add(e.PrintLine(""));
            commands.Add(e.CenterAlign());

            if (receipt.IsVatPayer)
            {
                commands.Add(e.SetStyles(PrintStyle.Bold));
                commands.Add(e.PrintLine("DAŇOVÝ DOKLAD"));
                commands.Add(e.SetStyles(PrintStyle.None));
                commands.Add(e.PrintLine(""));
            }

            commands.Add(e.PrintLine("Děkujeme za nákup!"));
            commands.Add(e.PrintLine(""));

            // Cut paper
            commands.Add(e.PrintLine(""));
            commands.Add(e.PrintLine(""));
            commands.Add(e.FullCutAfterFeed(3));

            return ByteSplicer.Combine(commands.ToArray());
        }
    }
}

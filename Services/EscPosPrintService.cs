using ESCPOS_NET;
using ESCPOS_NET.Emitters;
using ESCPOS_NET.Utilities;
using ESCPOS_NET.Printers;
using Sklad_2.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Text;
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
        private static readonly Encoding Cp852;

        static EscPosPrintService()
        {
            // Register CodePages provider for CP852 (Central European DOS encoding)
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            Cp852 = Encoding.GetEncoding(852);
        }

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
                // Validate COM port format
                if (!IsComPort(printerPath))
                {
                    Debug.WriteLine($"EscPosPrintService: Invalid COM port format: {printerPath}");
                    return false;
                }

                Debug.WriteLine($"EscPosPrintService: Opening SerialPort on {printerPath}");

                // Use direct SerialPort instead of ESCPOS_NET SerialPrinter
                await Task.Run(() =>
                {
                    using var port = new SerialPort(printerPath)
                    {
                        BaudRate = 38400,
                        Parity = Parity.None,
                        DataBits = 8,
                        StopBits = StopBits.One,
                        Handshake = Handshake.None,
                        WriteTimeout = 5000
                    };

                    port.Open();
                    Debug.WriteLine($"EscPosPrintService: Port opened successfully");

                    // ESC/POS commands
                    var commands = new List<byte>();

                    // Initialize printer: ESC @
                    commands.AddRange(new byte[] { 0x1B, 0x40 });

                    // Center align: ESC a 1
                    commands.AddRange(new byte[] { 0x1B, 0x61, 0x01 });

                    // Bold ON + Double height: ESC E 1, GS ! 0x10
                    commands.AddRange(new byte[] { 0x1B, 0x45, 0x01 });
                    commands.AddRange(new byte[] { 0x1D, 0x21, 0x10 });

                    // Print "TEST TISKU"
                    commands.AddRange(Cp852.GetBytes("TEST TISKU\n"));

                    // Reset styles: ESC E 0, GS ! 0
                    commands.AddRange(new byte[] { 0x1B, 0x45, 0x00 });
                    commands.AddRange(new byte[] { 0x1D, 0x21, 0x00 });

                    // Left align: ESC a 0
                    commands.AddRange(new byte[] { 0x1B, 0x61, 0x00 });

                    // Print info
                    commands.AddRange(Cp852.GetBytes("\n"));
                    commands.AddRange(Cp852.GetBytes("Tiskarna je pripojena\n"));
                    commands.AddRange(Cp852.GetBytes($"Port: {printerPath}\n"));
                    commands.AddRange(Cp852.GetBytes($"Cas: {DateTime.Now:dd.MM.yyyy HH:mm:ss}\n"));
                    commands.AddRange(Cp852.GetBytes("\n"));
                    commands.AddRange(Cp852.GetBytes("Ceske znaky: ěščřžýáíé\n"));
                    commands.AddRange(Cp852.GetBytes("\n"));

                    // Center align
                    commands.AddRange(new byte[] { 0x1B, 0x61, 0x01 });
                    commands.AddRange(Cp852.GetBytes("Test uspesny!\n"));

                    // Feed and cut: GS V 66 3
                    commands.AddRange(new byte[] { 0x0A, 0x0A, 0x0A });
                    commands.AddRange(new byte[] { 0x1D, 0x56, 0x42, 0x03 });

                    port.Write(commands.ToArray(), 0, commands.Count);
                    Debug.WriteLine($"EscPosPrintService: Wrote {commands.Count} bytes");

                    port.Close();
                });

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
                var printerPath = _settingsService.CurrentSettings.PrinterPath;

                if (string.IsNullOrWhiteSpace(printerPath) || !IsComPort(printerPath))
                {
                    return false;
                }

                // Try to open and immediately close the COM port to verify it exists
                using var port = new SerialPort(printerPath)
                {
                    BaudRate = 38400,
                    ReadTimeout = 500,
                    WriteTimeout = 500
                };
                port.Open();
                port.Close();
                return true;
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
                    return new SerialPrinter(portName: printerPath, baudRate: 38400);
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

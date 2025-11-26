using ESCPOS_NET;
using ESCPOS_NET.Emitters;
using ESCPOS_NET.Utilities;
using ESCPOS_NET.Printers;
using Sklad_2.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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

        // Receipt width in characters (Epson TM-T20III 80mm = 42 chars)
        private const int RECEIPT_WIDTH = 42;

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
                var printerPath = _settingsService.CurrentSettings.PrinterPath;

                // Check if printer is connected - if not, show preview instead
                if (!IsPrinterConnected())
                {
                    Debug.WriteLine($"EscPosPrintService: Printer not connected, showing preview instead");
                    await GenerateTextPreviewAsync(receipt);
                    return true; // Return true so the sale completes
                }

                Debug.WriteLine($"EscPosPrintService: Printing receipt {receipt.FormattedReceiptNumber} on {printerPath}");

                // Use direct SerialPort for reliable printing with CP852 encoding
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

                    // Build receipt data using raw ESC/POS commands
                    var commands = BuildReceiptCommands(receipt);

                    port.Write(commands.ToArray(), 0, commands.Count);
                    Debug.WriteLine($"EscPosPrintService: Wrote {commands.Count} bytes");

                    port.Close();
                });

                Debug.WriteLine($"EscPosPrintService: Receipt {receipt.FormattedReceiptNumber} printed successfully");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"EscPosPrintService: Print failed: {ex.Message}");
                // On error, try to show preview as fallback
                try
                {
                    await GenerateTextPreviewAsync(receipt);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        public async Task<bool> TestPrintAsync(string printerPath)
        {
            try
            {
                // Check if we can connect to the printer
                bool canConnect = false;
                if (IsComPort(printerPath))
                {
                    try
                    {
                        using var testPort = new SerialPort(printerPath) { ReadTimeout = 500, WriteTimeout = 500 };
                        testPort.Open();
                        testPort.Close();
                        canConnect = true;
                    }
                    catch
                    {
                        canConnect = false;
                    }
                }

                // If printer not available, show preview
                if (!canConnect)
                {
                    Debug.WriteLine($"EscPosPrintService: Printer not available, showing test preview");
                    await GenerateTestPreviewAsync(printerPath);
                    return true;
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

                    // Set character code page to CP852 (Central Europe): ESC t 18
                    commands.AddRange(new byte[] { 0x1B, 0x74, 0x12 });

                    // Center align: ESC a 1
                    commands.AddRange(new byte[] { 0x1B, 0x61, 0x01 });

                    // Bold ON + Double height: ESC E 1, GS ! 0x10
                    commands.AddRange(new byte[] { 0x1B, 0x45, 0x01 });
                    commands.AddRange(new byte[] { 0x1D, 0x21, 0x10 });

                    // Print "TEST TISKU"
                    commands.AddRange(Cp852.GetBytes("TEST TISKU\n"));

                    // Test Czech characters
                    commands.AddRange(new byte[] { 0x0A });
                    commands.AddRange(Cp852.GetBytes("Ceske znaky:\n"));
                    commands.AddRange(Cp852.GetBytes("escrzzyaieuu = ěščřžýáíéůú\n"));
                    commands.AddRange(Cp852.GetBytes("ESCRZZYAIEUU = ĚŠČŘŽÝÁÍÉŮÚ\n"));
                    commands.AddRange(Cp852.GetBytes("dtn = ďťň\n"));

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
                // Fallback to preview on error
                try
                {
                    await GenerateTestPreviewAsync(printerPath);
                    return true;
                }
                catch
                {
                    return false;
                }
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

        /// <summary>
        /// Builds raw ESC/POS commands for receipt printing with CP852 encoding
        /// </summary>
        private List<byte> BuildReceiptCommands(Receipt receipt)
        {
            var commands = new List<byte>();

            // Initialize printer: ESC @
            commands.AddRange(new byte[] { 0x1B, 0x40 });

            // Set character code page to CP852 (Central Europe): ESC t 18
            commands.AddRange(new byte[] { 0x1B, 0x74, 0x12 });

            // === HEADER ===
            // Center align: ESC a 1
            commands.AddRange(new byte[] { 0x1B, 0x61, 0x01 });

            // Bold ON + Double size (height+width): ESC E 1, GS ! 0x30
            commands.AddRange(new byte[] { 0x1B, 0x45, 0x01 });
            commands.AddRange(new byte[] { 0x1D, 0x21, 0x30 }); // Double height + width (2× size)
            commands.AddRange(Cp852.GetBytes(receipt.ShopName ?? ""));
            commands.AddRange(new byte[] { 0x0A }); // Line feed

            // Reset styles: Bold OFF, Normal size: ESC E 0, GS ! 0
            commands.AddRange(new byte[] { 0x1B, 0x45, 0x00 });
            commands.AddRange(new byte[] { 0x1D, 0x21, 0x00 });
            commands.AddRange(Cp852.GetBytes(receipt.ShopAddress ?? ""));
            commands.AddRange(new byte[] { 0x0A });

            // Company ID and VAT ID
            if (!string.IsNullOrWhiteSpace(receipt.CompanyId))
            {
                commands.AddRange(Cp852.GetBytes($"IČ: {receipt.CompanyId}"));
                commands.AddRange(new byte[] { 0x0A });
            }

            if (receipt.IsVatPayer && !string.IsNullOrWhiteSpace(receipt.VatId))
            {
                commands.AddRange(Cp852.GetBytes($"DIČ: {receipt.VatId}"));
                commands.AddRange(new byte[] { 0x0A });
            }

            commands.AddRange(new byte[] { 0x0A });

            // === RECEIPT TYPE HEADER ===
            if (receipt.IsStorno)
            {
                // Bold ON + Double height: ESC E 1, GS ! 0x10
                commands.AddRange(new byte[] { 0x1B, 0x45, 0x01 });
                commands.AddRange(new byte[] { 0x1D, 0x21, 0x10 });
                commands.AddRange(Cp852.GetBytes("STORNO"));
                commands.AddRange(new byte[] { 0x0A });
                // Reset styles: ESC E 0, GS ! 0
                commands.AddRange(new byte[] { 0x1B, 0x45, 0x00 });
                commands.AddRange(new byte[] { 0x1D, 0x21, 0x00 });

                if (receipt.OriginalReceiptId.HasValue)
                {
                    commands.AddRange(Cp852.GetBytes($"Storno účtenky #{receipt.OriginalReceiptId}"));
                    commands.AddRange(new byte[] { 0x0A });
                }
                commands.AddRange(new byte[] { 0x0A });
            }
            else if (receipt.ContainsGiftCardSale && receipt.GiftCardSaleAmount > 0)
            {
                // Bold ON: ESC E 1
                commands.AddRange(new byte[] { 0x1B, 0x45, 0x01 });
                commands.AddRange(Cp852.GetBytes("DÁRKOVÝ POUKAZ"));
                commands.AddRange(new byte[] { 0x0A });
                // Bold OFF: ESC E 0
                commands.AddRange(new byte[] { 0x1B, 0x45, 0x00 });
                commands.AddRange(new byte[] { 0x0A });
            }

            // === RECEIPT NUMBER AND DATE ===
            // Left align: ESC a 0
            commands.AddRange(new byte[] { 0x1B, 0x61, 0x00 });

            commands.AddRange(Cp852.GetBytes($"Účtenka: {receipt.FormattedReceiptNumber}"));
            commands.AddRange(new byte[] { 0x0A });
            commands.AddRange(Cp852.GetBytes($"Datum: {receipt.SaleDate:dd.MM.yyyy HH:mm}"));
            commands.AddRange(new byte[] { 0x0A });
            commands.AddRange(Cp852.GetBytes($"Prodejce: {receipt.SellerName}"));
            commands.AddRange(new byte[] { 0x0A });
            commands.AddRange(Cp852.GetBytes("================================"));
            commands.AddRange(new byte[] { 0x0A });

            // === ITEMS ===
            if (receipt.Items != null && receipt.Items.Count > 0)
            {
                foreach (var item in receipt.Items)
                {
                    commands.AddRange(Cp852.GetBytes(item.ProductName ?? ""));
                    commands.AddRange(new byte[] { 0x0A });

                    // Show discount if applicable
                    if (item.HasDiscount)
                    {
                        commands.AddRange(Cp852.GetBytes(
                            $"  {item.Quantity}x {item.OriginalUnitPrice:N2} Kč " +
                            $"{item.DiscountPercentFormatted}"
                        ));
                        commands.AddRange(new byte[] { 0x0A });
                        commands.AddRange(Cp852.GetBytes(
                            $"  Po slevě: {item.UnitPrice:N2} Kč ... {item.TotalPrice:N2} Kč"
                        ));
                        commands.AddRange(new byte[] { 0x0A });
                    }
                    else
                    {
                        commands.AddRange(Cp852.GetBytes(
                            $"  {item.Quantity}x {item.UnitPrice:N2} Kč ... {item.TotalPrice:N2} Kč"
                        ));
                        commands.AddRange(new byte[] { 0x0A });
                    }
                }
            }

            commands.AddRange(Cp852.GetBytes("================================"));
            commands.AddRange(new byte[] { 0x0A });

            // === GIFT CARD REDEMPTION ===
            if (receipt.ContainsGiftCardRedemption && receipt.GiftCardRedemptionAmount > 0)
            {
                commands.AddRange(new byte[] { 0x0A });
                commands.AddRange(Cp852.GetBytes($"Mezisoučet: {receipt.TotalAmount:N2} Kč"));
                commands.AddRange(new byte[] { 0x0A });
                // Bold ON: ESC E 1
                commands.AddRange(new byte[] { 0x1B, 0x45, 0x01 });
                commands.AddRange(Cp852.GetBytes($"Použitý poukaz: -{receipt.GiftCardRedemptionAmount:N2} Kč"));
                commands.AddRange(new byte[] { 0x0A });
                // Bold OFF: ESC E 0
                commands.AddRange(new byte[] { 0x1B, 0x45, 0x00 });
                // Show gift card EAN
                if (!string.IsNullOrWhiteSpace(receipt.RedeemedGiftCardEan))
                {
                    commands.AddRange(Cp852.GetBytes($"EAN poukazu: {receipt.RedeemedGiftCardEan}"));
                    commands.AddRange(new byte[] { 0x0A });
                }
            }

            // === VAT BREAKDOWN (only for VAT payers) ===
            if (receipt.IsVatPayer && receipt.Items != null && receipt.Items.Count > 0)
            {
                commands.AddRange(new byte[] { 0x0A });
                commands.AddRange(Cp852.GetBytes("DPH:"));
                commands.AddRange(new byte[] { 0x0A });

                // Group items by VAT rate
                var vatGroups = receipt.Items
                    .GroupBy(item => item.VatRate)
                    .OrderBy(g => g.Key);

                foreach (var group in vatGroups)
                {
                    var vatRate = group.Key;
                    var totalVatAmount = group.Sum(item => item.VatAmount);
                    var totalWithoutVat = group.Sum(item => item.PriceWithoutVat);

                    commands.AddRange(Cp852.GetBytes($"  Základ {vatRate}%: {totalWithoutVat:N2} Kč"));
                    commands.AddRange(new byte[] { 0x0A });
                    commands.AddRange(Cp852.GetBytes($"  DPH {vatRate}%: {totalVatAmount:N2} Kč"));
                    commands.AddRange(new byte[] { 0x0A });
                }

                commands.AddRange(new byte[] { 0x0A });
                commands.AddRange(Cp852.GetBytes($"Celkem bez DPH: {receipt.TotalAmountWithoutVat:N2} Kč"));
                commands.AddRange(new byte[] { 0x0A });
                commands.AddRange(Cp852.GetBytes($"Celkem DPH: {receipt.TotalVatAmount:N2} Kč"));
                commands.AddRange(new byte[] { 0x0A });
            }

            // === TOTAL AMOUNT ===
            commands.AddRange(new byte[] { 0x0A });
            // Bold ON + Double height: ESC E 1, GS ! 0x10
            commands.AddRange(new byte[] { 0x1B, 0x45, 0x01 });
            commands.AddRange(new byte[] { 0x1D, 0x21, 0x10 });

            if (receipt.ContainsGiftCardRedemption && receipt.GiftCardRedemptionAmount > 0)
            {
                commands.AddRange(Cp852.GetBytes($"K ÚHRADĚ: {receipt.AmountToPay:N2} Kč"));
            }
            else
            {
                commands.AddRange(Cp852.GetBytes($"CELKEM: {receipt.TotalAmount:N2} Kč"));
            }
            commands.AddRange(new byte[] { 0x0A });

            // Reset styles: ESC E 0, GS ! 0
            commands.AddRange(new byte[] { 0x1B, 0x45, 0x00 });
            commands.AddRange(new byte[] { 0x1D, 0x21, 0x00 });

            // === PAYMENT METHOD ===
            commands.AddRange(new byte[] { 0x0A });
            commands.AddRange(Cp852.GetBytes($"Platba: {receipt.PaymentMethod}"));
            commands.AddRange(new byte[] { 0x0A });

            // Received amount and change (for cash payments)
            if (receipt.ReceivedAmount > 0)
            {
                commands.AddRange(Cp852.GetBytes($"Přijato: {receipt.ReceivedAmount:N2} Kč"));
                commands.AddRange(new byte[] { 0x0A });
                if (receipt.ChangeAmount > 0)
                {
                    commands.AddRange(Cp852.GetBytes($"Vráceno: {receipt.ChangeAmount:N2} Kč"));
                    commands.AddRange(new byte[] { 0x0A });
                }
            }

            // === FOOTER ===
            commands.AddRange(new byte[] { 0x0A });
            // Center align: ESC a 1
            commands.AddRange(new byte[] { 0x1B, 0x61, 0x01 });

            if (receipt.IsVatPayer)
            {
                // Bold ON: ESC E 1
                commands.AddRange(new byte[] { 0x1B, 0x45, 0x01 });
                commands.AddRange(Cp852.GetBytes("DAŇOVÝ DOKLAD"));
                commands.AddRange(new byte[] { 0x0A });
                // Bold OFF: ESC E 0
                commands.AddRange(new byte[] { 0x1B, 0x45, 0x00 });
                commands.AddRange(new byte[] { 0x0A });
            }

            commands.AddRange(Cp852.GetBytes("Děkujeme za nákup!"));
            commands.AddRange(new byte[] { 0x0A });

            // Feed and cut: GS V 66 3
            commands.AddRange(new byte[] { 0x0A, 0x0A, 0x0A });
            commands.AddRange(new byte[] { 0x1D, 0x56, 0x42, 0x03 });

            return commands;
        }

        #region Return/Credit Note Printing

        public async Task<bool> PrintReturnAsync(Return returnDocument)
        {
            try
            {
                var printerPath = _settingsService.CurrentSettings.PrinterPath;

                // Check if printer is connected - if not, show preview instead
                if (!IsPrinterConnected())
                {
                    Debug.WriteLine($"EscPosPrintService: Printer not connected, showing return preview instead");
                    await GenerateReturnTextPreviewAsync(returnDocument);
                    return true;
                }

                Debug.WriteLine($"EscPosPrintService: Printing return {returnDocument.FormattedReturnNumber} on {printerPath}");

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

                    var commands = BuildReturnCommands(returnDocument);

                    port.Write(commands.ToArray(), 0, commands.Count);
                    Debug.WriteLine($"EscPosPrintService: Wrote {commands.Count} bytes");

                    port.Close();
                });

                Debug.WriteLine($"EscPosPrintService: Return {returnDocument.FormattedReturnNumber} printed successfully");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"EscPosPrintService: Return print failed: {ex.Message}");
                try
                {
                    await GenerateReturnTextPreviewAsync(returnDocument);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        private List<byte> BuildReturnCommands(Return returnDocument)
        {
            var commands = new List<byte>();

            // Initialize printer: ESC @
            commands.AddRange(new byte[] { 0x1B, 0x40 });

            // Set character code page to CP852 (Central Europe): ESC t 18
            commands.AddRange(new byte[] { 0x1B, 0x74, 0x12 });

            // === HEADER ===
            // Center align: ESC a 1
            commands.AddRange(new byte[] { 0x1B, 0x61, 0x01 });

            // Bold ON + Double size (height+width): ESC E 1, GS ! 0x30
            commands.AddRange(new byte[] { 0x1B, 0x45, 0x01 });
            commands.AddRange(new byte[] { 0x1D, 0x21, 0x30 }); // Double height + width (2× size)

            commands.AddRange(Cp852.GetBytes(returnDocument.ShopName ?? ""));
            commands.AddRange(new byte[] { 0x0A });

            // Reset styles: Bold OFF, Normal size: ESC E 0, GS ! 0
            commands.AddRange(new byte[] { 0x1B, 0x45, 0x00 });
            commands.AddRange(new byte[] { 0x1D, 0x21, 0x00 });

            commands.AddRange(Cp852.GetBytes(returnDocument.ShopAddress ?? ""));
            commands.AddRange(new byte[] { 0x0A });
            commands.AddRange(Cp852.GetBytes($"IC: {returnDocument.CompanyId}"));
            commands.AddRange(new byte[] { 0x0A });

            if (returnDocument.IsVatPayer && !string.IsNullOrWhiteSpace(returnDocument.VatId))
            {
                commands.AddRange(Cp852.GetBytes($"DIC: {returnDocument.VatId}"));
                commands.AddRange(new byte[] { 0x0A });
            }

            // Separator line
            commands.AddRange(Cp852.GetBytes("----------------------------------------"));
            commands.AddRange(new byte[] { 0x0A });

            // === DOCUMENT TYPE (DOBROPIS) ===
            // Bold ON + Double height
            commands.AddRange(new byte[] { 0x1B, 0x45, 0x01 });
            commands.AddRange(new byte[] { 0x1D, 0x21, 0x10 });
            commands.AddRange(Cp852.GetBytes("*** DOBROPIS ***"));
            commands.AddRange(new byte[] { 0x0A });

            // Reset styles
            commands.AddRange(new byte[] { 0x1B, 0x45, 0x00 });
            commands.AddRange(new byte[] { 0x1D, 0x21, 0x00 });

            if (returnDocument.IsVatPayer)
            {
                commands.AddRange(Cp852.GetBytes("OPRAVNY DANOVY DOKLAD"));
                commands.AddRange(new byte[] { 0x0A });
            }

            commands.AddRange(Cp852.GetBytes("----------------------------------------"));
            commands.AddRange(new byte[] { 0x0A });

            // === DOCUMENT INFO ===
            // Left align: ESC a 0
            commands.AddRange(new byte[] { 0x1B, 0x61, 0x00 });

            commands.AddRange(Cp852.GetBytes($"Dobropis c.: {returnDocument.FormattedReturnNumber}"));
            commands.AddRange(new byte[] { 0x0A });
            commands.AddRange(Cp852.GetBytes($"Datum: {returnDocument.ReturnDate:dd.MM.yyyy HH:mm}"));
            commands.AddRange(new byte[] { 0x0A });
            // Note: OriginalReceiptId is the sequence number, display with year
            commands.AddRange(Cp852.GetBytes($"K puvodni uctence c.: U{returnDocument.OriginalReceiptId:D4}/{returnDocument.ReturnDate.Year}"));
            commands.AddRange(new byte[] { 0x0A });

            // Separator
            commands.AddRange(Cp852.GetBytes("========================================"));
            commands.AddRange(new byte[] { 0x0A });

            // === ITEMS ===
            commands.AddRange(Cp852.GetBytes("Vracene polozky:"));
            commands.AddRange(new byte[] { 0x0A });

            if (returnDocument.Items != null)
            {
                foreach (var item in returnDocument.Items)
                {
                    commands.AddRange(Cp852.GetBytes(item.ProductName ?? ""));
                    commands.AddRange(new byte[] { 0x0A });
                    commands.AddRange(Cp852.GetBytes($"  {item.ReturnedQuantity}x {item.UnitPrice:N2} Kc"));

                    // Right-align total
                    var totalText = $"{item.TotalRefund:N2} Kc";
                    var spaces = Math.Max(1, 42 - 2 - item.ReturnedQuantity.ToString().Length - 1 - item.UnitPrice.ToString("N2").Length - 3 - totalText.Length);
                    commands.AddRange(Cp852.GetBytes(new string(' ', spaces)));
                    commands.AddRange(Cp852.GetBytes(totalText));
                    commands.AddRange(new byte[] { 0x0A });
                }
            }

            // Double separator
            commands.AddRange(Cp852.GetBytes("========================================"));
            commands.AddRange(new byte[] { 0x0A });

            // === VAT SUMMARY ===
            if (returnDocument.IsVatPayer && returnDocument.Items != null && returnDocument.Items.Count > 0)
            {
                commands.AddRange(Cp852.GetBytes("DPH:"));
                commands.AddRange(new byte[] { 0x0A });

                var vatGroups = returnDocument.Items
                    .GroupBy(item => item.VatRate)
                    .OrderBy(g => g.Key);

                foreach (var group in vatGroups)
                {
                    var vatRate = group.Key;
                    var totalVatAmount = group.Sum(item => item.VatAmount);
                    var totalWithoutVat = group.Sum(item => item.PriceWithoutVat);

                    commands.AddRange(Cp852.GetBytes($"  Zaklad {vatRate}%: {totalWithoutVat:N2} Kc"));
                    commands.AddRange(new byte[] { 0x0A });
                    commands.AddRange(Cp852.GetBytes($"  DPH {vatRate}%: {totalVatAmount:N2} Kc"));
                    commands.AddRange(new byte[] { 0x0A });
                }

                commands.AddRange(Cp852.GetBytes("----------------------------------------"));
                commands.AddRange(new byte[] { 0x0A });
            }

            // === TOTAL ===
            // Center align + Bold + Double height
            commands.AddRange(new byte[] { 0x1B, 0x61, 0x01 });
            commands.AddRange(new byte[] { 0x1B, 0x45, 0x01 });
            commands.AddRange(new byte[] { 0x1D, 0x21, 0x10 });
            commands.AddRange(new byte[] { 0x0A });
            commands.AddRange(Cp852.GetBytes($"*** VRACENO: {returnDocument.TotalRefundAmount:N2} Kc ***"));
            commands.AddRange(new byte[] { 0x0A });

            // Reset styles
            commands.AddRange(new byte[] { 0x1B, 0x45, 0x00 });
            commands.AddRange(new byte[] { 0x1D, 0x21, 0x00 });

            // Separator
            commands.AddRange(Cp852.GetBytes("========================================"));
            commands.AddRange(new byte[] { 0x0A });

            // === FOOTER ===
            commands.AddRange(Cp852.GetBytes("Dekujeme za pochopeni"));
            commands.AddRange(new byte[] { 0x0A });
            commands.AddRange(Cp852.GetBytes("----------------------------------------"));
            commands.AddRange(new byte[] { 0x0A });

            // Feed and cut: GS V 66 3
            commands.AddRange(new byte[] { 0x0A, 0x0A, 0x0A });
            commands.AddRange(new byte[] { 0x1D, 0x56, 0x42, 0x03 });

            return commands;
        }

        /// <summary>
        /// Generates a text preview of the return/credit note for debugging.
        /// </summary>
        public async Task<string> GenerateReturnTextPreviewAsync(Return returnDocument)
        {
            var sb = new StringBuilder();
            var separator = new string('-', RECEIPT_WIDTH);
            var doubleSeparator = new string('=', RECEIPT_WIDTH);

            // Top border
            sb.AppendLine($"|{separator}|");

            // === HEADER ===
            sb.AppendLine(FormatLine(returnDocument.ShopName ?? "", TextAlign.Center, bold: true));
            sb.AppendLine(FormatLine(returnDocument.ShopAddress ?? "", TextAlign.Center));

            if (!string.IsNullOrWhiteSpace(returnDocument.CompanyId))
            {
                sb.AppendLine(FormatLine($"IC: {returnDocument.CompanyId}", TextAlign.Center));
            }

            if (returnDocument.IsVatPayer && !string.IsNullOrWhiteSpace(returnDocument.VatId))
            {
                sb.AppendLine(FormatLine($"DIC: {returnDocument.VatId}", TextAlign.Center));
            }

            sb.AppendLine($"|{separator}|");

            // === DOCUMENT TYPE ===
            sb.AppendLine(FormatLine("*** DOBROPIS ***", TextAlign.Center, bold: true));
            if (returnDocument.IsVatPayer)
            {
                sb.AppendLine(FormatLine("OPRAVNY DANOVY DOKLAD", TextAlign.Center));
            }
            sb.AppendLine($"|{separator}|");

            // === DOCUMENT INFO ===
            sb.AppendLine(FormatLine($"Dobropis c.: {returnDocument.FormattedReturnNumber}", TextAlign.Left));
            sb.AppendLine(FormatLine($"Datum: {returnDocument.ReturnDate:dd.MM.yyyy HH:mm}", TextAlign.Left));
            sb.AppendLine(FormatLine($"K puvodni uctence c.: U{returnDocument.OriginalReceiptId:D4}/{returnDocument.ReturnDate.Year}", TextAlign.Left));
            sb.AppendLine($"|{doubleSeparator}|");

            // === ITEMS ===
            sb.AppendLine(FormatLine("Vracene polozky:", TextAlign.Left));

            if (returnDocument.Items != null && returnDocument.Items.Count > 0)
            {
                foreach (var item in returnDocument.Items)
                {
                    sb.AppendLine(FormatLine(item.ProductName ?? "", TextAlign.Left));
                    sb.AppendLine(FormatLineWithPrice(
                        $"  {item.ReturnedQuantity}x {item.UnitPrice:N2} Kc",
                        $"{item.TotalRefund:N2} Kc"));
                }
            }

            sb.AppendLine($"|{doubleSeparator}|");

            // === VAT SUMMARY ===
            if (returnDocument.IsVatPayer && returnDocument.Items != null && returnDocument.Items.Count > 0)
            {
                sb.AppendLine(FormatLine("DPH:", TextAlign.Left));

                var vatGroups = returnDocument.Items
                    .GroupBy(item => item.VatRate)
                    .OrderBy(g => g.Key);

                foreach (var group in vatGroups)
                {
                    var vatRate = group.Key;
                    var totalVatAmount = group.Sum(item => item.VatAmount);
                    var totalWithoutVat = group.Sum(item => item.PriceWithoutVat);

                    sb.AppendLine(FormatLineWithPrice($"  Zaklad {vatRate}%:", $"{totalWithoutVat:N2} Kc"));
                    sb.AppendLine(FormatLineWithPrice($"  DPH {vatRate}%:", $"{totalVatAmount:N2} Kc"));
                }

                sb.AppendLine($"|{separator}|");
            }

            // === TOTAL ===
            sb.AppendLine(FormatLine("", TextAlign.Left));
            sb.AppendLine(FormatLine($"*** VRACENO: {returnDocument.TotalRefundAmount:N2} Kc ***", TextAlign.Center, bold: true));
            sb.AppendLine(FormatLine("", TextAlign.Left));

            sb.AppendLine($"|{separator}|");

            // === FOOTER ===
            sb.AppendLine(FormatLine("Dekujeme za pochopeni", TextAlign.Center));
            sb.AppendLine($"|{separator}|");

            // Save and open
            var previewText = sb.ToString();
            var previewFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Sklad_2_Data", "receipts");
            Directory.CreateDirectory(previewFolder);
            var tempPath = Path.Combine(previewFolder, $"return_preview_{returnDocument.FormattedReturnNumber?.Replace("/", "_") ?? "temp"}.txt");

            // Use UTF-8 with BOM for proper encoding of Czech characters in Notepad
            await File.WriteAllTextAsync(tempPath, previewText, new System.Text.UTF8Encoding(true));

            // Open in Notepad
            Process.Start(new ProcessStartInfo
            {
                FileName = "notepad.exe",
                Arguments = tempPath,
                UseShellExecute = true
            });

            Debug.WriteLine($"EscPosPrintService: Return preview saved to {tempPath}");
            return previewText;
        }

        #endregion

        #region Text Preview (Debug Mode)

        /// <summary>
        /// Generates a text preview of the receipt for debugging without a physical printer.
        /// Opens the preview in Notepad automatically.
        /// </summary>
        public async Task<string> GenerateTextPreviewAsync(Receipt receipt)
        {
            var sb = new StringBuilder();
            var separator = new string('-', RECEIPT_WIDTH);
            var doubleSeparator = new string('=', RECEIPT_WIDTH);

            // Top border
            sb.AppendLine($"|{separator}|");

            // === HEADER ===
            sb.AppendLine(FormatLine(receipt.ShopName ?? "", TextAlign.Center, bold: true));
            sb.AppendLine(FormatLine(receipt.ShopAddress ?? "", TextAlign.Center));

            if (!string.IsNullOrWhiteSpace(receipt.CompanyId))
            {
                sb.AppendLine(FormatLine($"IČ: {receipt.CompanyId}", TextAlign.Center));
            }

            if (receipt.IsVatPayer && !string.IsNullOrWhiteSpace(receipt.VatId))
            {
                sb.AppendLine(FormatLine($"DIČ: {receipt.VatId}", TextAlign.Center));
            }

            sb.AppendLine($"|{separator}|");

            // === RECEIPT TYPE HEADER ===
            if (receipt.IsStorno)
            {
                sb.AppendLine(FormatLine("*** STORNO ***", TextAlign.Center, bold: true));
                if (receipt.OriginalReceiptId.HasValue)
                {
                    sb.AppendLine(FormatLine($"Storno účtenky #{receipt.OriginalReceiptId}", TextAlign.Center));
                }
                sb.AppendLine($"|{separator}|");
            }
            else if (receipt.ContainsGiftCardSale && receipt.GiftCardSaleAmount > 0)
            {
                sb.AppendLine(FormatLine("DÁRKOVÝ POUKAZ", TextAlign.Center, bold: true));
                sb.AppendLine($"|{separator}|");
            }

            // === RECEIPT INFO ===
            sb.AppendLine(FormatLine($"Účtenka: {receipt.FormattedReceiptNumber}", TextAlign.Left));
            sb.AppendLine(FormatLine($"Datum: {receipt.SaleDate:dd.MM.yyyy HH:mm}", TextAlign.Left));
            sb.AppendLine(FormatLine($"Prodejce: {receipt.SellerName}", TextAlign.Left));
            sb.AppendLine($"|{doubleSeparator}|");

            // === ITEMS ===
            if (receipt.Items != null && receipt.Items.Count > 0)
            {
                foreach (var item in receipt.Items)
                {
                    sb.AppendLine(FormatLine(item.ProductName ?? "", TextAlign.Left));

                    if (item.HasDiscount)
                    {
                        sb.AppendLine(FormatLine(
                            $"  {item.Quantity}x {item.OriginalUnitPrice:N2} Kč {item.DiscountPercentFormatted}",
                            TextAlign.Left));
                        sb.AppendLine(FormatLineWithPrice(
                            $"  Po slevě: {item.UnitPrice:N2} Kč",
                            $"{item.TotalPrice:N2} Kč"));
                    }
                    else
                    {
                        sb.AppendLine(FormatLineWithPrice(
                            $"  {item.Quantity}x {item.UnitPrice:N2} Kč",
                            $"{item.TotalPrice:N2} Kč"));
                    }
                }
            }

            sb.AppendLine($"|{doubleSeparator}|");

            // === GIFT CARD REDEMPTION ===
            if (receipt.ContainsGiftCardRedemption && receipt.GiftCardRedemptionAmount > 0)
            {
                sb.AppendLine(FormatLineWithPrice("Mezisoučet:", $"{receipt.TotalAmount:N2} Kč"));
                sb.AppendLine(FormatLineWithPrice("Použitý poukaz:", $"-{receipt.GiftCardRedemptionAmount:N2} Kč", bold: true));
                if (!string.IsNullOrWhiteSpace(receipt.RedeemedGiftCardEan))
                {
                    sb.AppendLine(FormatLine($"EAN poukazu: {receipt.RedeemedGiftCardEan}", TextAlign.Left));
                }
                sb.AppendLine($"|{separator}|");
            }

            // === VAT BREAKDOWN ===
            if (receipt.IsVatPayer && receipt.Items != null && receipt.Items.Count > 0)
            {
                sb.AppendLine(FormatLine("DPH:", TextAlign.Left));

                var vatGroups = receipt.Items
                    .GroupBy(item => item.VatRate)
                    .OrderBy(g => g.Key);

                foreach (var group in vatGroups)
                {
                    var vatRate = group.Key;
                    var totalVatAmount = group.Sum(item => item.VatAmount);
                    var totalWithoutVat = group.Sum(item => item.PriceWithoutVat);

                    sb.AppendLine(FormatLineWithPrice($"  Základ {vatRate}%:", $"{totalWithoutVat:N2} Kč"));
                    sb.AppendLine(FormatLineWithPrice($"  DPH {vatRate}%:", $"{totalVatAmount:N2} Kč"));
                }

                sb.AppendLine($"|{separator}|");
                sb.AppendLine(FormatLineWithPrice("Celkem bez DPH:", $"{receipt.TotalAmountWithoutVat:N2} Kč"));
                sb.AppendLine(FormatLineWithPrice("Celkem DPH:", $"{receipt.TotalVatAmount:N2} Kč"));
                sb.AppendLine($"|{separator}|");
            }

            // === TOTAL ===
            sb.AppendLine(FormatLine("", TextAlign.Left)); // Empty line

            if (receipt.ContainsGiftCardRedemption && receipt.GiftCardRedemptionAmount > 0)
            {
                sb.AppendLine(FormatLine($"*** K ÚHRADĚ: {receipt.AmountToPay:N2} Kč ***", TextAlign.Center, bold: true));
            }
            else
            {
                sb.AppendLine(FormatLine($"*** CELKEM: {receipt.TotalAmount:N2} Kč ***", TextAlign.Center, bold: true));
            }

            sb.AppendLine(FormatLine("", TextAlign.Left)); // Empty line

            // === PAYMENT ===
            sb.AppendLine(FormatLine($"Platba: {receipt.PaymentMethod}", TextAlign.Left));

            if (receipt.ReceivedAmount > 0)
            {
                sb.AppendLine(FormatLineWithPrice("Přijato:", $"{receipt.ReceivedAmount:N2} Kč"));
                if (receipt.ChangeAmount > 0)
                {
                    sb.AppendLine(FormatLineWithPrice("Vráceno:", $"{receipt.ChangeAmount:N2} Kč"));
                }
            }

            sb.AppendLine($"|{separator}|");

            // === FOOTER ===
            if (receipt.IsVatPayer)
            {
                sb.AppendLine(FormatLine("DAŇOVÝ DOKLAD", TextAlign.Center, bold: true));
            }

            sb.AppendLine(FormatLine("Děkujeme za nákup!", TextAlign.Center));
            sb.AppendLine($"|{separator}|");

            // Save and open
            var previewText = sb.ToString();
            var previewFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Sklad_2_Data", "receipts");
            Directory.CreateDirectory(previewFolder);
            var tempPath = Path.Combine(previewFolder, $"receipt_preview_{receipt.FormattedReceiptNumber?.Replace("/", "_") ?? "temp"}.txt");

            // Use UTF-8 with BOM for proper encoding of Czech characters in Notepad
            await File.WriteAllTextAsync(tempPath, previewText, new System.Text.UTF8Encoding(true));

            // Open in Notepad
            Process.Start(new ProcessStartInfo
            {
                FileName = "notepad.exe",
                Arguments = tempPath,
                UseShellExecute = true
            });

            Debug.WriteLine($"EscPosPrintService: Preview saved to {tempPath}");
            return previewText;
        }

        /// <summary>
        /// Generates a test print preview for debugging.
        /// </summary>
        public async Task<string> GenerateTestPreviewAsync(string printerPath)
        {
            var sb = new StringBuilder();
            var separator = new string('-', RECEIPT_WIDTH);

            sb.AppendLine($"|{separator}|");
            sb.AppendLine(FormatLine("TEST TISKU", TextAlign.Center, bold: true));
            sb.AppendLine($"|{separator}|");
            sb.AppendLine(FormatLine("", TextAlign.Left));
            sb.AppendLine(FormatLine("České znaky:", TextAlign.Center));
            sb.AppendLine(FormatLine("ěščřžýáíéůú ĚŠČŘŽÝÁÍÉŮÚ", TextAlign.Center));
            sb.AppendLine(FormatLine("ďťň ĎŤŇ", TextAlign.Center));
            sb.AppendLine(FormatLine("", TextAlign.Left));
            sb.AppendLine($"|{separator}|");
            sb.AppendLine(FormatLine("Tiskárna NENÍ připojena", TextAlign.Left));
            sb.AppendLine(FormatLine($"Port: {printerPath ?? "(nenastaveno)"}", TextAlign.Left));
            sb.AppendLine(FormatLine($"Čas: {DateTime.Now:dd.MM.yyyy HH:mm:ss}", TextAlign.Left));
            sb.AppendLine($"|{separator}|");
            sb.AppendLine(FormatLine("", TextAlign.Left));
            sb.AppendLine(FormatLine("Toto je NÁHLED tisku", TextAlign.Center, bold: true));
            sb.AppendLine(FormatLine("(bez fyzické tiskárny)", TextAlign.Center));
            sb.AppendLine($"|{separator}|");

            var previewText = sb.ToString();
            var previewFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Sklad_2_Data", "receipts");
            Directory.CreateDirectory(previewFolder);
            var tempPath = Path.Combine(previewFolder, "receipt_test_preview.txt");

            // Use UTF-8 with BOM for proper encoding of Czech characters in Notepad
            await File.WriteAllTextAsync(tempPath, previewText, new System.Text.UTF8Encoding(true));

            // Open in Notepad
            Process.Start(new ProcessStartInfo
            {
                FileName = "notepad.exe",
                Arguments = tempPath,
                UseShellExecute = true
            });

            Debug.WriteLine($"EscPosPrintService: Test preview saved to {tempPath}");
            return previewText;
        }

        #region Formatting Helpers

        private enum TextAlign { Left, Center, Right }

        /// <summary>
        /// Formats a line with alignment within the receipt width.
        /// </summary>
        private string FormatLine(string text, TextAlign align, bool bold = false)
        {
            // Truncate if too long
            if (text.Length > RECEIPT_WIDTH)
            {
                text = text.Substring(0, RECEIPT_WIDTH);
            }

            string content;
            switch (align)
            {
                case TextAlign.Center:
                    var leftPad = (RECEIPT_WIDTH - text.Length) / 2;
                    var rightPad = RECEIPT_WIDTH - text.Length - leftPad;
                    content = new string(' ', leftPad) + text + new string(' ', rightPad);
                    break;
                case TextAlign.Right:
                    content = text.PadLeft(RECEIPT_WIDTH);
                    break;
                case TextAlign.Left:
                default:
                    content = text.PadRight(RECEIPT_WIDTH);
                    break;
            }

            // Add bold markers for visual indication
            if (bold)
            {
                return $"|{content}|";
            }

            return $"|{content}|";
        }

        /// <summary>
        /// Formats a line with left text and right-aligned price.
        /// </summary>
        private string FormatLineWithPrice(string leftText, string rightText, bool bold = false)
        {
            var totalLen = leftText.Length + rightText.Length;

            if (totalLen > RECEIPT_WIDTH)
            {
                // Truncate left text if needed
                var maxLeftLen = RECEIPT_WIDTH - rightText.Length - 1;
                if (maxLeftLen > 0)
                {
                    leftText = leftText.Substring(0, Math.Min(leftText.Length, maxLeftLen));
                }
            }

            var spaces = RECEIPT_WIDTH - leftText.Length - rightText.Length;
            if (spaces < 1) spaces = 1;

            var content = leftText + new string(' ', spaces) + rightText;

            return $"|{content}|";
        }

        #endregion

        #endregion
    }
}

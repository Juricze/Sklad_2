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
                var printerPath = _settingsService.CurrentSettings.PrinterPath;

                // Validate COM port format
                if (!IsComPort(printerPath))
                {
                    Debug.WriteLine($"EscPosPrintService: Invalid COM port format: {printerPath}");
                    return false;
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

            // Bold ON: ESC E 1
            commands.AddRange(new byte[] { 0x1B, 0x45, 0x01 });
            commands.AddRange(Cp852.GetBytes(receipt.ShopName ?? ""));
            commands.AddRange(new byte[] { 0x0A }); // Line feed

            // Bold OFF: ESC E 0
            commands.AddRange(new byte[] { 0x1B, 0x45, 0x00 });
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
    }
}

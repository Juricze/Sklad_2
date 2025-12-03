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
using SkiaSharp;

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

        // Receipt width in characters (Epson TM-T20III 80mm = 48 chars)
        private const int RECEIPT_WIDTH = 48;

        // Left indent for non-centered text (1 empty + 2 visual like "==")
        private const string INDENT = "   "; // 3 spaces

        // Right margin for symmetry (1 empty + 2 visual like "==")
        private const int RIGHT_MARGIN = 3;

        // Effective width for text content (excluding left indent and right margin)
        private const int EFFECTIVE_WIDTH = RECEIPT_WIDTH - 3 - RIGHT_MARGIN; // 48 - 3 - 3 = 42

        // Maximum width for product names before wrapping
        private const int MAX_PRODUCT_NAME_WIDTH = 40;

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
        /// Wraps long text to multiple lines with specified max width.
        /// </summary>
        private List<string> WordWrap(string text, int maxWidth)
        {
            var lines = new List<string>();
            if (string.IsNullOrEmpty(text))
                return lines;

            var words = text.Split(' ');
            var currentLine = "";

            foreach (var word in words)
            {
                var testLine = string.IsNullOrEmpty(currentLine) ? word : $"{currentLine} {word}";

                if (testLine.Length <= maxWidth)
                {
                    currentLine = testLine;
                }
                else
                {
                    if (!string.IsNullOrEmpty(currentLine))
                    {
                        lines.Add(currentLine);
                        currentLine = word;
                    }
                    else
                    {
                        // Word is longer than maxWidth - truncate
                        lines.Add(word.Substring(0, Math.Min(word.Length, maxWidth)));
                        currentLine = "";
                    }
                }
            }

            if (!string.IsNullOrEmpty(currentLine))
                lines.Add(currentLine);

            return lines;
        }

        /// <summary>
        /// Formats a line with left text and right-aligned price.
        /// Includes right margin for symmetry with left indent.
        /// Uses dots to fill space between left and right text for better readability.
        /// </summary>
        private string FormatLineWithRightPrice(string leftText, string rightText, int totalWidth, bool useDots = true)
        {
            // Account for right margin (3 spaces)
            var effectiveWidth = totalWidth - RIGHT_MARGIN;

            if (leftText.Length + rightText.Length >= effectiveWidth)
            {
                // Truncate left text if needed
                var maxLeftLen = effectiveWidth - rightText.Length - 1;
                if (maxLeftLen > 0)
                    leftText = leftText.Substring(0, maxLeftLen);
            }

            var fillLength = effectiveWidth - leftText.Length - rightText.Length;
            if (fillLength < 1) fillLength = 1;

            // Use dots for prices, spaces for other lines
            var filler = useDots ? new string('.', fillLength) : new string(' ', fillLength);

            // Add right margin (3 spaces) at the end
            return leftText + filler + rightText + new string(' ', RIGHT_MARGIN);
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
        /// Loads and converts logo to ESC/POS raster image format.
        /// Converts color/grayscale BMP to 1-bit monochrome, scales to fit printer width (max 384px).
        /// Returns null if logo file not found or conversion fails.
        /// </summary>
        private List<byte> LoadLogoCommands()
        {
            try
            {
                var logoPath = Path.Combine(AppContext.BaseDirectory, "essets", "luvera_logo.bmp");

                if (!File.Exists(logoPath))
                {
                    Debug.WriteLine($"EscPosPrintService: Logo not found at {logoPath}");
                    return null;
                }

                // Load image with SkiaSharp
                using var originalBitmap = SKBitmap.Decode(logoPath);
                if (originalBitmap == null)
                {
                    Debug.WriteLine("EscPosPrintService: Failed to decode logo");
                    return null;
                }

                // Calculate scaled size (max width 384px for 80mm printer)
                const int maxWidth = 384;
                int scaledWidth = Math.Min(originalBitmap.Width, maxWidth);
                int scaledHeight = (int)(originalBitmap.Height * ((double)scaledWidth / originalBitmap.Width));

                // Resize image
                using var scaledBitmap = originalBitmap.Resize(new SKImageInfo(scaledWidth, scaledHeight), SKFilterQuality.High);

                // Convert to monochrome (1-bit black/white)
                var monoPixels = new bool[scaledWidth * scaledHeight];
                for (int y = 0; y < scaledHeight; y++)
                {
                    for (int x = 0; x < scaledWidth; x++)
                    {
                        var pixel = scaledBitmap.GetPixel(x, y);
                        // Convert to grayscale using standard formula
                        var gray = (int)(0.299 * pixel.Red + 0.587 * pixel.Green + 0.114 * pixel.Blue);
                        // Threshold: < 128 = black (true), >= 128 = white (false)
                        monoPixels[y * scaledWidth + x] = gray < 128;
                    }
                }

                // Convert to ESC/POS raster format (GS v 0)
                var commands = new List<byte>();

                // Center align for logo
                commands.AddRange(new byte[] { 0x1B, 0x61, 0x01 }); // ESC a 1

                // GS v 0: Print raster bit image
                // Format: GS v 0 m xL xH yL yH [data]
                // m = mode (0 = normal, 1 = double width, 2 = double height, 3 = quadruple)
                commands.Add(0x1D); // GS
                commands.Add(0x76); // v
                commands.Add(0x30); // 0
                commands.Add(0x00); // m = normal size

                // Width in bytes (each byte = 8 pixels)
                int widthBytes = (scaledWidth + 7) / 8;
                commands.Add((byte)(widthBytes & 0xFF));        // xL
                commands.Add((byte)((widthBytes >> 8) & 0xFF)); // xH

                // Height in pixels
                commands.Add((byte)(scaledHeight & 0xFF));        // yL
                commands.Add((byte)((scaledHeight >> 8) & 0xFF)); // yH

                // Pixel data (row by row, MSB first)
                for (int y = 0; y < scaledHeight; y++)
                {
                    for (int xByte = 0; xByte < widthBytes; xByte++)
                    {
                        byte b = 0;
                        for (int bit = 0; bit < 8; bit++)
                        {
                            int x = xByte * 8 + bit;
                            if (x < scaledWidth)
                            {
                                if (monoPixels[y * scaledWidth + x])
                                {
                                    b |= (byte)(1 << (7 - bit)); // Set bit (MSB first)
                                }
                            }
                        }
                        commands.Add(b);
                    }
                }

                // Add line feed after logo
                commands.AddRange(new byte[] { 0x0A });

                Debug.WriteLine($"EscPosPrintService: Logo loaded successfully ({scaledWidth}x{scaledHeight}px, {commands.Count} bytes)");
                return commands;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"EscPosPrintService: Failed to load logo: {ex.Message}");
                return null;
            }
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

            // === LOGO ===
            var logoCommands = LoadLogoCommands();
            if (logoCommands != null)
            {
                commands.AddRange(logoCommands);
            }
            else
            {
                // Fallback if logo not available - print shop name
                // Center align: ESC a 1
                commands.AddRange(new byte[] { 0x1B, 0x61, 0x01 });
                // Bold ON + Double size: ESC E 1, GS ! 0x30
                commands.AddRange(new byte[] { 0x1B, 0x45, 0x01 });
                commands.AddRange(new byte[] { 0x1D, 0x21, 0x30 });
                commands.AddRange(Cp852.GetBytes(receipt.ShopName ?? ""));
                commands.AddRange(new byte[] { 0x0A });
                // Reset styles: ESC E 0, GS ! 0
                commands.AddRange(new byte[] { 0x1B, 0x45, 0x00 });
                commands.AddRange(new byte[] { 0x1D, 0x21, 0x00 });
                commands.AddRange(new byte[] { 0x0A });
            }

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

            // === RECEIPT NUMBER AND DATE ===
            // Center align: ESC a 1
            commands.AddRange(new byte[] { 0x1B, 0x61, 0x01 });

            commands.AddRange(Cp852.GetBytes($"Účtenka: {receipt.FormattedReceiptNumber}"));
            commands.AddRange(new byte[] { 0x0A });
            commands.AddRange(Cp852.GetBytes($"Datum: {receipt.SaleDate:dd.MM.yyyy HH:mm}"));
            commands.AddRange(new byte[] { 0x0A });
            commands.AddRange(Cp852.GetBytes($"Prodejce: {receipt.SellerName}"));
            commands.AddRange(new byte[] { 0x0A });

            // Left align for items: ESC a 0
            commands.AddRange(new byte[] { 0x1B, 0x61, 0x00 });
            commands.AddRange(Cp852.GetBytes(new string('=', RECEIPT_WIDTH)));
            commands.AddRange(new byte[] { 0x0A });

            // === ITEMS ===
            if (receipt.Items != null && receipt.Items.Count > 0)
            {
                for (int i = 0; i < receipt.Items.Count; i++)
                {
                    var item = receipt.Items[i];

                    // Word wrap long product names
                    var nameLines = WordWrap(item.ProductName ?? "", MAX_PRODUCT_NAME_WIDTH);
                    foreach (var line in nameLines)
                    {
                        commands.AddRange(Cp852.GetBytes($"{INDENT}{line}"));
                        commands.AddRange(new byte[] { 0x0A });
                    }

                    // Show discount if applicable
                    if (item.HasDiscount)
                    {
                        // Original price + discount
                        var leftText = $"{INDENT}{item.Quantity}x {item.OriginalUnitPrice:N2} Kč {item.DiscountPercentFormatted}";
                        var rightText = $"{item.TotalPrice:N2} Kč";
                        var line = FormatLineWithRightPrice(leftText, rightText, RECEIPT_WIDTH, useDots: true);
                        commands.AddRange(Cp852.GetBytes(line));
                        commands.AddRange(new byte[] { 0x0A });

                        // Discounted price info
                        commands.AddRange(Cp852.GetBytes($"{INDENT}Po slevě: {item.UnitPrice:N2} Kč"));
                        commands.AddRange(new byte[] { 0x0A });
                    }
                    else
                    {
                        // Quantity x Price ... Total (right-aligned with dots)
                        var leftText = $"{INDENT}{item.Quantity}x {item.UnitPrice:N2} Kč";
                        var rightText = $"{item.TotalPrice:N2} Kč";
                        var line = FormatLineWithRightPrice(leftText, rightText, RECEIPT_WIDTH, useDots: true);
                        commands.AddRange(Cp852.GetBytes(line));
                        commands.AddRange(new byte[] { 0x0A });
                    }

                    // Separator line between items (except after last item)
                    if (i < receipt.Items.Count - 1)
                    {
                        commands.AddRange(Cp852.GetBytes(new string('-', RECEIPT_WIDTH)));
                        commands.AddRange(new byte[] { 0x0A });
                    }
                }
            }

            commands.AddRange(Cp852.GetBytes(new string('=', RECEIPT_WIDTH)));
            commands.AddRange(new byte[] { 0x0A });

            // === DISCOUNTS SECTION (Loyalty + Gift Card) ===
            if (receipt.HasAnyDiscount)
            {
                commands.AddRange(new byte[] { 0x0A });

                // Subtotal (right-aligned with dots)
                var leftText = $"{INDENT}Mezisoučet:";
                var rightText = $"{receipt.TotalAmount:N2} Kč";
                var line = FormatLineWithRightPrice(leftText, rightText, RECEIPT_WIDTH, useDots: true);
                commands.AddRange(Cp852.GetBytes(line));
                commands.AddRange(new byte[] { 0x0A });

                // === LOYALTY DISCOUNT ===
                if (receipt.HasLoyaltyDiscount && receipt.LoyaltyDiscountAmount > 0)
                {
                    // Bold ON: ESC E 1
                    commands.AddRange(new byte[] { 0x1B, 0x45, 0x01 });
                    leftText = $"{INDENT}Věrn. sleva ({receipt.LoyaltyDiscountPercent:N0}%):";
                    rightText = $"-{receipt.LoyaltyDiscountAmount:N2} Kč";
                    line = FormatLineWithRightPrice(leftText, rightText, RECEIPT_WIDTH, useDots: true);
                    commands.AddRange(Cp852.GetBytes(line));
                    commands.AddRange(new byte[] { 0x0A });
                    // Bold OFF: ESC E 0
                    commands.AddRange(new byte[] { 0x1B, 0x45, 0x00 });

                    // Show member contact (email or phone)
                    if (!string.IsNullOrWhiteSpace(receipt.LoyaltyCustomerContact))
                    {
                        commands.AddRange(Cp852.GetBytes($"{INDENT}Uživatel: {receipt.LoyaltyCustomerContact}"));
                        commands.AddRange(new byte[] { 0x0A });
                    }
                }

                // === GIFT CARD REDEMPTION ===
                if (receipt.ContainsGiftCardRedemption && receipt.GiftCardRedemptionAmount > 0)
                {
                    // Bold ON: ESC E 1
                    commands.AddRange(new byte[] { 0x1B, 0x45, 0x01 });
                    leftText = $"{INDENT}Použitý poukaz:";
                    rightText = $"-{receipt.GiftCardRedemptionAmount:N2} Kč";
                    line = FormatLineWithRightPrice(leftText, rightText, RECEIPT_WIDTH, useDots: true);
                    commands.AddRange(Cp852.GetBytes(line));
                    commands.AddRange(new byte[] { 0x0A });
                    // Bold OFF: ESC E 0
                    commands.AddRange(new byte[] { 0x1B, 0x45, 0x00 });

                    // Show gift card EANs (multiple cards possible)
                    if (receipt.RedeemedGiftCards != null && receipt.RedeemedGiftCards.Any())
                    {
                        foreach (var redemption in receipt.RedeemedGiftCards)
                        {
                            commands.AddRange(Cp852.GetBytes($"{INDENT}EAN poukazu: {redemption.GiftCardEan} ({redemption.RedeemedAmount:C})"));
                            commands.AddRange(new byte[] { 0x0A });
                        }
                    }
                }
            }

            // === VAT BREAKDOWN (only for VAT payers) ===
            if (receipt.IsVatPayer && receipt.Items != null && receipt.Items.Count > 0)
            {
                commands.AddRange(new byte[] { 0x0A });
                commands.AddRange(Cp852.GetBytes($"{INDENT}DPH:"));
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

                    var leftText = $"{INDENT}  Základ {vatRate}%:";
                    var rightText = $"{totalWithoutVat:N2} Kč";
                    var line = FormatLineWithRightPrice(leftText, rightText, RECEIPT_WIDTH, useDots: false);
                    commands.AddRange(Cp852.GetBytes(line));
                    commands.AddRange(new byte[] { 0x0A });

                    leftText = $"{INDENT}  DPH {vatRate}%:";
                    rightText = $"{totalVatAmount:N2} Kč";
                    line = FormatLineWithRightPrice(leftText, rightText, RECEIPT_WIDTH, useDots: false);
                    commands.AddRange(Cp852.GetBytes(line));
                    commands.AddRange(new byte[] { 0x0A });
                }

                commands.AddRange(new byte[] { 0x0A });

                var leftTotal = $"{INDENT}Celkem bez DPH:";
                var rightTotal = $"{receipt.TotalAmountWithoutVat:N2} Kč";
                var lineTotal = FormatLineWithRightPrice(leftTotal, rightTotal, RECEIPT_WIDTH, useDots: false);
                commands.AddRange(Cp852.GetBytes(lineTotal));
                commands.AddRange(new byte[] { 0x0A });

                leftTotal = $"{INDENT}Celkem DPH:";
                rightTotal = $"{receipt.TotalVatAmount:N2} Kč";
                lineTotal = FormatLineWithRightPrice(leftTotal, rightTotal, RECEIPT_WIDTH, useDots: false);
                commands.AddRange(Cp852.GetBytes(lineTotal));
                commands.AddRange(new byte[] { 0x0A });
            }

            // === TOTAL AMOUNT ===
            commands.AddRange(new byte[] { 0x0A });

            // Left align for precise amount display
            commands.AddRange(new byte[] { 0x1B, 0x61, 0x00 });

            // Zobrazit přesnou částku (s haléři)
            if (receipt.HasAnyDiscount)
            {
                var leftPrecise = $"{INDENT}Celkem (přesně):";
                var rightPrecise = $"{receipt.AmountToPay:N2} Kč";
                var linePrecise = FormatLineWithRightPrice(leftPrecise, rightPrecise, RECEIPT_WIDTH, useDots: true);
                commands.AddRange(Cp852.GetBytes(linePrecise));
            }
            else
            {
                var leftPrecise = $"{INDENT}Celkem (přesně):";
                var rightPrecise = $"{receipt.TotalAmount:N2} Kč";
                var linePrecise = FormatLineWithRightPrice(leftPrecise, rightPrecise, RECEIPT_WIDTH, useDots: true);
                commands.AddRange(Cp852.GetBytes(linePrecise));
            }
            commands.AddRange(new byte[] { 0x0A });

            // Zobrazit zaokrouhlení (pokud není 0)
            if (receipt.RoundingAmount != 0)
            {
                var leftRounding = $"{INDENT}Zaokrouhlení:";
                var rightRounding = receipt.RoundingAmountFormatted;
                var lineRounding = FormatLineWithRightPrice(leftRounding, rightRounding, RECEIPT_WIDTH, useDots: true);
                commands.AddRange(Cp852.GetBytes(lineRounding));
                commands.AddRange(new byte[] { 0x0A });
            }

            commands.AddRange(new byte[] { 0x0A });

            // Center align + Bold pro finální částku
            commands.AddRange(new byte[] { 0x1B, 0x61, 0x01 });
            commands.AddRange(new byte[] { 0x1B, 0x45, 0x01 });

            // Zobrazit zaokrouhlenou částku k úhradě (celé koruny)
            commands.AddRange(Cp852.GetBytes($"*** K ÚHRADĚ: {receipt.FinalAmountRounded:N0} Kč ***"));
            commands.AddRange(new byte[] { 0x0A });

            // Reset styles: ESC E 0
            commands.AddRange(new byte[] { 0x1B, 0x45, 0x00 });
            // Left align: ESC a 0
            commands.AddRange(new byte[] { 0x1B, 0x61, 0x00 });

            // === PAYMENT METHOD ===
            commands.AddRange(new byte[] { 0x0A });
            commands.AddRange(Cp852.GetBytes($"{INDENT}Platba: {receipt.PaymentMethod}"));
            commands.AddRange(new byte[] { 0x0A });

            // Received amount and change (for cash payments)
            if (receipt.ReceivedAmount > 0)
            {
                var leftText = $"{INDENT}Přijato:";
                var rightText = $"{receipt.ReceivedAmount:N2} Kč";
                var line = FormatLineWithRightPrice(leftText, rightText, RECEIPT_WIDTH, useDots: true);
                commands.AddRange(Cp852.GetBytes(line));
                commands.AddRange(new byte[] { 0x0A });

                if (receipt.ChangeAmount > 0)
                {
                    leftText = $"{INDENT}Vráceno:";
                    rightText = $"{receipt.ChangeAmount:N2} Kč";
                    line = FormatLineWithRightPrice(leftText, rightText, RECEIPT_WIDTH, useDots: true);
                    commands.AddRange(Cp852.GetBytes(line));
                    commands.AddRange(new byte[] { 0x0A });
                }
            }

            // === FOOTER ===
            commands.AddRange(new byte[] { 0x0A });
            // Center align: ESC a 1
            commands.AddRange(new byte[] { 0x1B, 0x61, 0x01 });

            // Shop address and company info
            if (!string.IsNullOrWhiteSpace(receipt.ShopAddress))
            {
                commands.AddRange(Cp852.GetBytes(receipt.ShopAddress));
                commands.AddRange(new byte[] { 0x0A });
            }

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

            // === LOGO ===
            var logoCommands = LoadLogoCommands();
            if (logoCommands != null)
            {
                commands.AddRange(logoCommands);
            }
            else
            {
                // Fallback if logo not available - print shop name
                // Center align: ESC a 1
                commands.AddRange(new byte[] { 0x1B, 0x61, 0x01 });
                // Bold ON + Double size: ESC E 1, GS ! 0x30
                commands.AddRange(new byte[] { 0x1B, 0x45, 0x01 });
                commands.AddRange(new byte[] { 0x1D, 0x21, 0x30 });
                commands.AddRange(Cp852.GetBytes(returnDocument.ShopName ?? ""));
                commands.AddRange(new byte[] { 0x0A });
                // Reset styles: ESC E 0, GS ! 0
                commands.AddRange(new byte[] { 0x1B, 0x45, 0x00 });
                commands.AddRange(new byte[] { 0x1D, 0x21, 0x00 });
                commands.AddRange(new byte[] { 0x0A });
            }

            // Center align: ESC a 1
            commands.AddRange(new byte[] { 0x1B, 0x61, 0x01 });

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
                commands.AddRange(Cp852.GetBytes("OPRAVNÝ DAŇOVÝ DOKLAD"));
                commands.AddRange(new byte[] { 0x0A });
            }

            commands.AddRange(Cp852.GetBytes(new string('-', RECEIPT_WIDTH)));
            commands.AddRange(new byte[] { 0x0A });

            // === DOCUMENT INFO ===
            // Center align: ESC a 1
            commands.AddRange(new byte[] { 0x1B, 0x61, 0x01 });

            commands.AddRange(Cp852.GetBytes($"Dobropis č.: {returnDocument.FormattedReturnNumber}"));
            commands.AddRange(new byte[] { 0x0A });
            commands.AddRange(Cp852.GetBytes($"Datum: {returnDocument.ReturnDate:dd.MM.yyyy HH:mm}"));
            commands.AddRange(new byte[] { 0x0A });
            // Note: OriginalReceiptId is the sequence number, display with year
            commands.AddRange(Cp852.GetBytes($"K původní účtence č.: U{returnDocument.OriginalReceiptId:D4}/{returnDocument.ReturnDate.Year}"));
            commands.AddRange(new byte[] { 0x0A });

            // Left align for items: ESC a 0
            commands.AddRange(new byte[] { 0x1B, 0x61, 0x00 });
            // Separator
            commands.AddRange(Cp852.GetBytes(new string('=', RECEIPT_WIDTH)));
            commands.AddRange(new byte[] { 0x0A });

            // === ITEMS ===
            commands.AddRange(Cp852.GetBytes($"{INDENT}Vrácené položky:"));
            commands.AddRange(new byte[] { 0x0A });

            if (returnDocument.Items != null)
            {
                for (int i = 0; i < returnDocument.Items.Count; i++)
                {
                    var item = returnDocument.Items[i];

                    // Word wrap long product names
                    var nameLines = WordWrap(item.ProductName ?? "", MAX_PRODUCT_NAME_WIDTH);
                    foreach (var line in nameLines)
                    {
                        commands.AddRange(Cp852.GetBytes($"{INDENT}{line}"));
                        commands.AddRange(new byte[] { 0x0A });
                    }

                    // Quantity x Price ... Total (right-aligned with dots)
                    var leftText2 = $"{INDENT}{item.ReturnedQuantity}x {item.UnitPrice:N2} Kč";
                    var rightText2 = $"{item.TotalRefund:N2} Kč";
                    var priceLine = FormatLineWithRightPrice(leftText2, rightText2, RECEIPT_WIDTH, useDots: true);
                    commands.AddRange(Cp852.GetBytes(priceLine));
                    commands.AddRange(new byte[] { 0x0A });

                    // Separator line between items (except after last item)
                    if (i < returnDocument.Items.Count - 1)
                    {
                        commands.AddRange(Cp852.GetBytes(new string('-', RECEIPT_WIDTH)));
                        commands.AddRange(new byte[] { 0x0A });
                    }
                }
            }

            // Double separator
            commands.AddRange(Cp852.GetBytes(new string('=', RECEIPT_WIDTH)));
            commands.AddRange(new byte[] { 0x0A });

            // === VAT SUMMARY ===
            if (returnDocument.IsVatPayer && returnDocument.Items != null && returnDocument.Items.Count > 0)
            {
                commands.AddRange(Cp852.GetBytes($"{INDENT}DPH:"));
                commands.AddRange(new byte[] { 0x0A });

                var vatGroups = returnDocument.Items
                    .GroupBy(item => item.VatRate)
                    .OrderBy(g => g.Key);

                foreach (var group in vatGroups)
                {
                    var vatRate = group.Key;
                    var totalVatAmount = group.Sum(item => item.VatAmount);
                    var totalWithoutVat = group.Sum(item => item.PriceWithoutVat);

                    var vatLeftText = $"{INDENT}  Základ {vatRate}%:";
                    var vatRightText = $"{totalWithoutVat:N2} Kč";
                    var vatLine = FormatLineWithRightPrice(vatLeftText, vatRightText, RECEIPT_WIDTH, useDots: false);
                    commands.AddRange(Cp852.GetBytes(vatLine));
                    commands.AddRange(new byte[] { 0x0A });

                    vatLeftText = $"{INDENT}  DPH {vatRate}%:";
                    vatRightText = $"{totalVatAmount:N2} Kč";
                    vatLine = FormatLineWithRightPrice(vatLeftText, vatRightText, RECEIPT_WIDTH, useDots: false);
                    commands.AddRange(Cp852.GetBytes(vatLine));
                    commands.AddRange(new byte[] { 0x0A });
                }

                commands.AddRange(Cp852.GetBytes(new string('-', RECEIPT_WIDTH)));
                commands.AddRange(new byte[] { 0x0A });
            }

            // === TOTAL ===
            // DRY: Use AmountToRefund (after loyalty discount) for actual refund amount
            commands.AddRange(new byte[] { 0x0A });

            // Left align for precise amount display
            commands.AddRange(new byte[] { 0x1B, 0x61, 0x00 });

            // Zobrazit přesnou částku vratky (s haléři)
            var leftRefundPrecise = $"{INDENT}Vratka (přesně):";
            var rightRefundPrecise = $"{returnDocument.AmountToRefund:N2} Kč";
            var lineRefundPrecise = FormatLineWithRightPrice(leftRefundPrecise, rightRefundPrecise, RECEIPT_WIDTH, useDots: true);
            commands.AddRange(Cp852.GetBytes(lineRefundPrecise));
            commands.AddRange(new byte[] { 0x0A });

            // Zobrazit zaokrouhlení vratky (pokud není 0)
            if (returnDocument.RefundRoundingAmount != 0)
            {
                var leftRefundRounding = $"{INDENT}Zaokrouhlení:";
                var rightRefundRounding = returnDocument.RefundRoundingAmountFormatted;
                var lineRefundRounding = FormatLineWithRightPrice(leftRefundRounding, rightRefundRounding, RECEIPT_WIDTH, useDots: true);
                commands.AddRange(Cp852.GetBytes(lineRefundRounding));
                commands.AddRange(new byte[] { 0x0A });
            }

            commands.AddRange(new byte[] { 0x0A });

            // Center align + Bold pro finální částku vratky (celé koruny)
            commands.AddRange(new byte[] { 0x1B, 0x61, 0x01 });
            commands.AddRange(new byte[] { 0x1B, 0x45, 0x01 });
            commands.AddRange(Cp852.GetBytes($"*** VRÁCENO: {returnDocument.FinalRefundRounded:N0} Kč ***"));
            commands.AddRange(new byte[] { 0x0A });

            // Reset styles
            commands.AddRange(new byte[] { 0x1B, 0x45, 0x00 });
            // Left align
            commands.AddRange(new byte[] { 0x1B, 0x61, 0x00 });

            // Separator
            commands.AddRange(Cp852.GetBytes(new string('=', RECEIPT_WIDTH)));
            commands.AddRange(new byte[] { 0x0A });

            // === FOOTER ===
            commands.AddRange(new byte[] { 0x0A });

            // Shop address and company info
            if (!string.IsNullOrWhiteSpace(returnDocument.ShopAddress))
            {
                commands.AddRange(Cp852.GetBytes(returnDocument.ShopAddress));
                commands.AddRange(new byte[] { 0x0A });
            }

            if (!string.IsNullOrWhiteSpace(returnDocument.CompanyId))
            {
                commands.AddRange(Cp852.GetBytes($"IČ: {returnDocument.CompanyId}"));
                commands.AddRange(new byte[] { 0x0A });
            }

            if (returnDocument.IsVatPayer && !string.IsNullOrWhiteSpace(returnDocument.VatId))
            {
                commands.AddRange(Cp852.GetBytes($"DIČ: {returnDocument.VatId}"));
                commands.AddRange(new byte[] { 0x0A });
            }

            commands.AddRange(new byte[] { 0x0A });
            commands.AddRange(Cp852.GetBytes("Děkujeme za pochopení"));
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

            // === HEADER (LOGO) ===
            sb.AppendLine(FormatLine("[LOGO]", TextAlign.Center, bold: true));
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
            // DRY: Use AmountToRefund (after loyalty discount) for actual refund amount
            sb.AppendLine(FormatLine("", TextAlign.Left));
            sb.AppendLine(FormatLine($"*** VRACENO: {returnDocument.AmountToRefund:N2} Kc ***", TextAlign.Center, bold: true));
            sb.AppendLine(FormatLine("", TextAlign.Left));

            sb.AppendLine($"|{separator}|");

            // === FOOTER ===
            if (!string.IsNullOrWhiteSpace(returnDocument.ShopAddress))
            {
                sb.AppendLine(FormatLine(returnDocument.ShopAddress, TextAlign.Center));
            }

            if (!string.IsNullOrWhiteSpace(returnDocument.CompanyId))
            {
                sb.AppendLine(FormatLine($"IČ: {returnDocument.CompanyId}", TextAlign.Center));
            }

            if (returnDocument.IsVatPayer && !string.IsNullOrWhiteSpace(returnDocument.VatId))
            {
                sb.AppendLine(FormatLine($"DIČ: {returnDocument.VatId}", TextAlign.Center));
            }

            sb.AppendLine(FormatLine("", TextAlign.Left)); // Empty line
            sb.AppendLine(FormatLine("Děkujeme za pochopení", TextAlign.Center));
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

            // === HEADER (LOGO) ===
            sb.AppendLine(FormatLine("[LOGO]", TextAlign.Center, bold: true));
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

            // === DISCOUNTS SECTION (Loyalty + Gift Card) ===
            if (receipt.HasAnyDiscount)
            {
                sb.AppendLine(FormatLineWithPrice("Mezisoučet:", $"{receipt.TotalAmount:N2} Kč"));

                // Loyalty discount
                if (receipt.HasLoyaltyDiscount && receipt.LoyaltyDiscountAmount > 0)
                {
                    sb.AppendLine(FormatLineWithPrice($"Věrn. sleva ({receipt.LoyaltyDiscountPercent:N0}%):", $"-{receipt.LoyaltyDiscountAmount:N2} Kč", bold: true));
                    if (!string.IsNullOrWhiteSpace(receipt.LoyaltyCustomerContact))
                    {
                        sb.AppendLine(FormatLine($"Uživatel: {receipt.LoyaltyCustomerContact}", TextAlign.Left));
                    }
                }

                // Gift card redemption
                if (receipt.ContainsGiftCardRedemption && receipt.GiftCardRedemptionAmount > 0)
                {
                    sb.AppendLine(FormatLineWithPrice("Použité poukazy:", $"-{receipt.GiftCardRedemptionAmount:N2} Kč", bold: true));
                    if (receipt.RedeemedGiftCards != null && receipt.RedeemedGiftCards.Any())
                    {
                        foreach (var redemption in receipt.RedeemedGiftCards)
                        {
                            sb.AppendLine(FormatLine($"EAN poukazu: {redemption.GiftCardEan} ({redemption.RedeemedAmount:C})", TextAlign.Left));
                        }
                    }
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

            // Použít AmountToPay pokud je jakákoliv sleva (věrnostní nebo poukaz)
            if (receipt.HasAnyDiscount)
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
            if (!string.IsNullOrWhiteSpace(receipt.ShopAddress))
            {
                sb.AppendLine(FormatLine(receipt.ShopAddress, TextAlign.Center));
            }

            if (!string.IsNullOrWhiteSpace(receipt.CompanyId))
            {
                sb.AppendLine(FormatLine($"IČ: {receipt.CompanyId}", TextAlign.Center));
            }

            if (receipt.IsVatPayer && !string.IsNullOrWhiteSpace(receipt.VatId))
            {
                sb.AppendLine(FormatLine($"DIČ: {receipt.VatId}", TextAlign.Center));
            }

            sb.AppendLine(FormatLine("", TextAlign.Left)); // Empty line

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

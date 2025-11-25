using Sklad_2.Models;
using System;
using System.Threading.Tasks;

namespace Sklad_2.Services
{
    /// <summary>
    /// Placeholder PrintService - always returns success
    /// Use EscPosPrintService for actual Epson TM-T20III printing
    /// </summary>
    public class PrintService : IPrintService
    {
        public async Task<bool> PrintReceiptAsync(Receipt receipt)
        {
            // Placeholder for printing logic
            // Simulate success/failure for testing
            await Task.Delay(1000); // Simulate print time

            return true; // Simulate print success
        }

        public async Task<bool> TestPrintAsync(string printerPath)
        {
            // Placeholder for actual test print logic
            await Task.Delay(500); // Simulate test print time

            return true; // Simulate test print success
        }

        public bool IsPrinterConnected()
        {
            // Placeholder - always returns true
            return true;
        }
    }
}
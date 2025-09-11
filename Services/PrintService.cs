using System;
using System.Threading.Tasks;

namespace Sklad_2.Services
{
    public class PrintService : IPrintService
    {
        public async Task<bool> PrintReceiptAsync(IReceiptService receipt)
        {
            // Placeholder for printing logic
            // Simulate success/failure for testing
            await Task.Delay(1000); // Simulate print time

            // For testing, let's make it fail sometimes
            // if (new Random().Next(0, 2) == 0) // 50% chance of failure
            // {
            //     return false; // Simulate print failure
            // }

            return true; // Simulate print success
        }

        public async Task<bool> TestPrintAsync(string printerPath) // Added
        {
            // Placeholder for actual test print logic
            // In a real app, this would try to send a small test job to the printerPath
            await Task.Delay(500); // Simulate test print time

            // For testing, let's make it fail sometimes
            // if (printerPath.ToLower().Contains("fail"))
            // {
            //     return false;
            // }

            return true; // Simulate test print success
        }
    }
}
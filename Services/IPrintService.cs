using Sklad_2.Models;
using System.Threading.Tasks;

namespace Sklad_2.Services
{
    public interface IPrintService
    {
        Task<bool> PrintReceiptAsync(Receipt receipt); // Returns true for success, false for failure
        Task<bool> TestPrintAsync(string printerPath); // Test print functionality
        bool IsPrinterConnected(); // Check if printer is connected
    }
}
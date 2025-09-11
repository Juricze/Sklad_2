using System.Threading.Tasks;

namespace Sklad_2.Services
{
    public interface IPrintService
    {
        Task<bool> PrintReceiptAsync(IReceiptService receipt); // Returns true for success, false for failure
        Task<bool> TestPrintAsync(string printerPath); // Added
    }
}
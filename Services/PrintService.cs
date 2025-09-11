using System.Threading.Tasks;

namespace Sklad_2.Services
{
    public class PrintService : IPrintService
    {
        public async Task PrintReceiptAsync(IReceiptService receipt)
        {
            // Placeholder for printing logic
            await Task.Delay(100); // Simulate async work
        }
    }
}
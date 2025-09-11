using System.Threading.Tasks;

namespace Sklad_2.Services
{
    public interface IPrintService
    {
        Task PrintReceiptAsync(IReceiptService receipt);
    }
}
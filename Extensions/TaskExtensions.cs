using System.Threading.Tasks;

namespace Sklad_2.Extensions
{
    public static class TaskExtensions
    {
        // Extension method to safely fire and forget a Task
        public static async void FireAndForgetSafeAsync(this Task task)
        {
            try
            {
                await task;
            }
            catch
            {
                // Log the exception here if needed
                // For now, we just suppress it to prevent crashing the app
            }
        }
    }
}

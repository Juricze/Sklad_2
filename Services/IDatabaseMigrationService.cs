using System.Threading.Tasks;

namespace Sklad_2.Services
{
    public interface IDatabaseMigrationService
    {
        Task<bool> MigrateToLatestAsync();
        Task<int> GetCurrentSchemaVersionAsync();
        Task<bool> IsDatabaseUpToDateAsync();
    }
}
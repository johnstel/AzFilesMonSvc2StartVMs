using System.Threading;
using System.Threading.Tasks;

namespace AzureFileShareMonitorService.Services
{
    public interface IFileShareMonitorService
    {
        Task MonitorAsync(CancellationToken cancellationToken);
    }
}

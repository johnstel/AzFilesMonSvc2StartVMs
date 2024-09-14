using AzureFileShareMonitorService.Models;
using System.Threading;
using System.Threading.Tasks;

namespace AzureFileShareMonitorService.Services
{
    public interface IVMManager
    {
        Task<VMState> GetVMStateAsync(FolderMapping mapping, CancellationToken cancellationToken);
        Task StartVMAsync(FolderMapping mapping, CancellationToken cancellationToken);
    }
}

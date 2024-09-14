using AzureFileShareMonitorService.Models;
using Azure.ResourceManager.Compute;
using Azure.ResourceManager.Compute.Models;
using Azure.Identity;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System;

namespace AzureFileShareMonitorService.Services
{
    public class VMManager : IVMManager
    {
        private readonly ILogger<VMManager> _logger;

        public VMManager(ILogger<VMManager> logger)
        {
            _logger = logger;
        }

        public async Task<VMState> GetVMStateAsync(FolderMapping mapping, CancellationToken cancellationToken)
        {
            try
            {
                var credential = new DefaultAzureCredential();
                var computeClient = new ComputeManagementClient(mapping.SubscriptionId, credential, new ComputeManagementClientOptions
                {
                    Diagnostics = { IsLoggingContentEnabled = false }
                });

                var vmInstanceViewResponse = await computeClient.VirtualMachines.InstanceViewAsync(mapping.ResourceGroupName, mapping.VMName, cancellationToken: cancellationToken).ConfigureAwait(false);
                var statuses = vmInstanceViewResponse.Value.Statuses;

                // The power state is typically in the second status
                var powerStateStatus = statuses.FirstOrDefault(s => s.Code.StartsWith("PowerState/"))?.Code;

                return powerStateStatus switch
                {
                    "PowerState/starting" => VMState.Starting,
                    "PowerState/running" => VMState.Running,
                    "PowerState/stopping" => VMState.Stopping,
                    "PowerState/stopped" => VMState.Stopped,
                    "PowerState/deallocating" => VMState.Deallocating,
                    "PowerState/deallocated" => VMState.Deallocated,
                    _ => VMState.Unknown
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to get VM state for {mapping.VMName}.");
                return VMState.Unknown;
            }
        }

        public async Task StartVMAsync(FolderMapping mapping, CancellationToken cancellationToken)
        {
            try
            {
                var credential = new DefaultAzureCredential();
                var computeClient = new ComputeManagementClient(mapping.SubscriptionId, credential);

                await computeClient.VirtualMachines.StartAsync(mapping.ResourceGroupName, mapping.VMName, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to start VM {mapping.VMName}.");
                throw;
            }
        }
    }
}

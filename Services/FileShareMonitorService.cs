using AzureFileShareMonitorService.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Azure.Storage.Files.Shares;
using Azure.Identity;
using Polly;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace AzureFileShareMonitorService.Services
{
    public class FileShareMonitorService : IFileShareMonitorService
    {
        private readonly ILogger<FileShareMonitorService> _logger;
        private readonly IVMManager _vmManager;
        private readonly IEnumerable<FolderMapping> _folderMappings;
        private readonly AzureSettings _azureSettings;

        public FileShareMonitorService(
            ILogger<FileShareMonitorService> logger,
            IVMManager vmManager,
            IOptions<AzureSettings> azureOptions,
            IOptions<List<FolderMapping>> folderMappings)
        {
            _logger = logger;
            _vmManager = vmManager;
            _folderMappings = folderMappings.Value;
            _azureSettings = azureOptions.Value;

            // Configuration validation
            ValidateConfiguration();
        }

        private void ValidateConfiguration()
        {
            if (string.IsNullOrEmpty(_azureSettings.StorageAccountName) ||
                string.IsNullOrEmpty(_azureSettings.FileShareName))
            {
                var message = "Azure Storage Account Name and File Share Name must be configured.";
                _logger.LogCritical(message);
                throw new InvalidOperationException(message);
            }

            if (!_folderMappings.Any())
            {
                var message = "At least one folder mapping must be configured.";
                _logger.LogCritical(message);
                throw new InvalidOperationException(message);
            }
        }

        public async Task MonitorAsync(CancellationToken cancellationToken)
        {
            var credential = new DefaultAzureCredential();

            // Use the storage account's private endpoint
            var shareUri = new Uri($"https://{_azureSettings.StorageAccountName}.file.core.windows.net/{_azureSettings.FileShareName}");
            var shareClient = new ShareClient(shareUri, credential);

            foreach (var mapping in _folderMappings)
            {
                // Implement retry policy with Polly
                var policy = Policy.Handle<HttpRequestException>()
                    .Or<TimeoutException>()
                    .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), (ex, time) =>
                    {
                        _logger.LogWarning(ex, $"Retrying due to error: {ex.Message}");
                    });

                await policy.ExecuteAsync(async () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var directoryClient = shareClient.GetDirectoryClient(mapping.FolderName);
                    var files = directoryClient.GetFilesAndDirectoriesAsync(cancellationToken: cancellationToken);

                    int fileCount = 0;
                    await foreach (var item in files.WithCancellation(cancellationToken).ConfigureAwait(false))
                    {
                        if (!item.IsDirectory)
                            fileCount++;
                    }

                    var vmState = await _vmManager.GetVMStateAsync(mapping, cancellationToken).ConfigureAwait(false);

                    switch (vmState)
                    {
                        case VMState.Stopped:
                        case VMState.Deallocated:
                            _logger.LogInformation($"Starting VM {mapping.VMName} as it is in {vmState} state.");
                            await _vmManager.StartVMAsync(mapping, cancellationToken).ConfigureAwait(false);
                            break;

                        case VMState.Running:
                        case VMState.Starting:
                        case VMState.Stopping:
                        case VMState.Deallocating:
                            _logger.LogInformation($"VM {mapping.VMName} is in {vmState} state. Folder '{mapping.FolderName}' has {fileCount} files.");
                            break;

                        case VMState.Unknown:
                        default:
                            _logger.LogWarning($"VM {mapping.VMName} state is unknown.");
                            break;
                    }
                });
            }
        }
    }
}

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
using Azure;

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

            // Use the storage account's endpoint
            var shareUri = new Uri($"https://{_azureSettings.StorageAccountName}.file.core.windows.net");
            var shareServiceClient = new ShareServiceClient(shareUri, credential);
            var shareClient = shareServiceClient.GetShareClient(_azureSettings.FileShareName);

            var shareExists = await shareClient.ExistsAsync(cancellationToken).ConfigureAwait(false);
            if (!shareExists)
            {
                var message = $"Azure file share '{_azureSettings.FileShareName}' does not exist in storage account '{_azureSettings.StorageAccountName}'.";
                _logger.LogCritical(message);
                throw new InvalidOperationException(message);
            }

            foreach (var mapping in _folderMappings)
            {
                var directoryClient = shareClient.GetDirectoryClient(mapping.FolderName);
                var directoryExists = await directoryClient.ExistsAsync(cancellationToken).ConfigureAwait(false);
                if (!directoryExists)
                {
                    _logger.LogError("Directory '{Folder}' does not exist in share '{Share}'. Skipping VM {VM}.",
                        mapping.FolderName,
                        _azureSettings.FileShareName,
                        mapping.VMName);
                    continue;
                }

                // Implement retry policy with Polly
                var policy = Policy.Handle<RequestFailedException>()
                    .Or<TimeoutException>()
                    .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), (ex, time) =>
                    {
                        _logger.LogWarning(ex, $"Retrying due to error: {ex.Message}");
                    });

                await policy.ExecuteAsync(async () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var files = directoryClient.GetFilesAndDirectoriesAsync(cancellationToken: cancellationToken);

                    int fileCount = 0;
                    await foreach (var item in files.WithCancellation(cancellationToken).ConfigureAwait(false))
                    {
                        if (!item.IsDirectory)
                            fileCount++;
                    }

                    var vmState = await _vmManager.GetVMStateAsync(mapping, cancellationToken).ConfigureAwait(false);
                    var threshold = Math.Max(0, mapping.StartThreshold);
                    var hasWork = fileCount > 0;
                    var meetsThreshold = hasWork && fileCount >= threshold;

                    switch (vmState)
                    {
                        case VMState.Stopped:
                        case VMState.Deallocated:
                            if (meetsThreshold)
                            {
                                _logger.LogInformation(
                                    "Starting VM {VM} because folder '{Folder}' contains {FileCount} files (threshold {Threshold}).",
                                    mapping.VMName,
                                    mapping.FolderName,
                                    fileCount,
                                    threshold);
                                await _vmManager.StartVMAsync(mapping, cancellationToken).ConfigureAwait(false);
                            }
                            else
                            {
                                if (!hasWork)
                                {
                                    _logger.LogInformation(
                                        "VM {VM} remains {State}; folder '{Folder}' is empty and threshold is {Threshold}.",
                                        mapping.VMName,
                                        vmState,
                                        mapping.FolderName,
                                        threshold);
                                }
                                else
                                {
                                    _logger.LogInformation(
                                        "VM {VM} remains {State}; folder '{Folder}' has {FileCount} files which is below threshold {Threshold}.",
                                        mapping.VMName,
                                        vmState,
                                        mapping.FolderName,
                                        fileCount,
                                        threshold);
                                }
                            }
                            break;

                        case VMState.Running:
                        case VMState.Starting:
                        case VMState.Stopping:
                        case VMState.Deallocating:
                            _logger.LogInformation(
                                "VM {VM} is in {State} state. Folder '{Folder}' has {FileCount} files.",
                                mapping.VMName,
                                vmState,
                                mapping.FolderName,
                                fileCount);
                            break;

                        case VMState.Unknown:
                        default:
                            _logger.LogWarning("VM {VM} state is unknown for folder '{Folder}'.", mapping.VMName, mapping.FolderName);
                            break;
                    }
                }).ConfigureAwait(false);
            }
        }
    }
}

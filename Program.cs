using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Azure.Identity;
using Azure.Extensions.AspNetCore.Configuration.Secrets;
using AzureFileShareMonitorService.Services;
using AzureFileShareMonitorService.Logging;
using Azure.Security.KeyVault.Secrets;
using AzureFileShareMonitorService.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using System;

namespace AzureFileShareMonitorService
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        private static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    // Build initial configuration
                    var settings = config.Build();
                    var keyVaultName = settings["AzureSettings:KeyVaultName"];

                    if (!string.IsNullOrEmpty(keyVaultName))
                    {
                        var keyVaultEndpoint = new Uri($"https://{keyVaultName}.vault.azure.net/");
                        var credential = new DefaultAzureCredential();

                        // Add Azure Key Vault to configuration sources
                        config.AddAzureKeyVault(keyVaultEndpoint, credential, new KeyVaultSecretManager());
                    }
                })
                .ConfigureServices((hostContext, services) =>
                {
                    // Bind configuration sections
                    services.Configure<PollingSettings>(hostContext.Configuration.GetSection("PollingSettings"));
                    services.Configure<AzureSettings>(hostContext.Configuration.GetSection("AzureSettings"));

                    // Bind FolderMappings from configuration
                    var folderMappings = new List<FolderMapping>();
                    hostContext.Configuration.GetSection("FolderMappings").Bind(folderMappings);
                    services.Configure<List<FolderMapping>>(options => options.AddRange(folderMappings));

                    services.AddSingleton<IVMManager, VMManager>();
                    services.AddSingleton<IFileShareMonitorService, FileShareMonitorService>();
                    services.AddHostedService<Worker>();

                    // Ensure that the configurations are valid at startup
                    var serviceProvider = services.BuildServiceProvider();

                    // Validate configurations
                    ValidateConfigurations(serviceProvider);
                })
                .ConfigureLogging((hostingContext, logging) =>
                {
                    logging.ClearProviders();
                    logging.AddFileLogger(options =>
                    {
                        options.LogFilePath = hostingContext.Configuration["Logging:LogFilePath"];
                    });
                });

        private static void ValidateConfigurations(ServiceProvider serviceProvider)
        {
            var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

            // Validate PollingSettings
            var pollingSettings = serviceProvider.GetRequiredService<IOptions<PollingSettings>>().Value;
            if (pollingSettings.IntervalInSeconds < 30)
            {
                logger.LogWarning("Polling interval is below 30 seconds. Setting it to 30 seconds.");
                pollingSettings.IntervalInSeconds = 30;
            }

            // Validate AzureSettings
            var azureSettings = serviceProvider.GetRequiredService<IOptions<AzureSettings>>().Value;
            if (string.IsNullOrEmpty(azureSettings.KeyVaultName))
            {
                var message = "Azure Key Vault name must be configured in 'AzureSettings:KeyVaultName'.";
                logger.LogCritical(message);
                throw new InvalidOperationException(message);
            }

            // Validate FolderMappings
            var folderMappings = serviceProvider.GetRequiredService<IOptions<List<FolderMapping>>>().Value;
            if (folderMappings == null || !folderMappings.Any())
            {
                var message = "At least one folder mapping must be configured in 'FolderMappings'.";
                logger.LogCritical(message);
                throw new InvalidOperationException(message);
            }
        }
    }
}

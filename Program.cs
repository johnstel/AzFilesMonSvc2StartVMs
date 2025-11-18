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
using System.Linq;

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
                    // Bind configuration sections with validation
                    services.AddOptions<PollingSettings>()
                        .Bind(hostContext.Configuration.GetSection("PollingSettings"))
                        .Validate(settings => settings.IntervalInSeconds >= 30,
                            "Polling interval must be at least 30 seconds.")
                        .PostConfigure(settings =>
                        {
                            settings.IntervalInSeconds = Math.Max(settings.IntervalInSeconds, 30);
                        });

                    services.AddOptions<AzureSettings>()
                        .Bind(hostContext.Configuration.GetSection("AzureSettings"))
                        .Validate(settings => !string.IsNullOrWhiteSpace(settings.KeyVaultName),
                            "Azure Key Vault name must be configured in 'AzureSettings:KeyVaultName'.");

                    services.AddOptions<List<FolderMapping>>()
                        .Bind(hostContext.Configuration.GetSection("FolderMappings"))
                        .Validate(mappings => mappings != null && mappings.Any(),
                            "At least one folder mapping must be configured in 'FolderMappings'.");

                    services.AddSingleton<IVMManager, VMManager>();
                    services.AddSingleton<IFileShareMonitorService, FileShareMonitorService>();
                    services.AddHostedService<Worker>();
                })
                .ConfigureLogging((hostingContext, logging) =>
                {
                    logging.ClearProviders();
                    logging.AddFileLogger(options =>
                    {
                        options.LogFilePath = hostingContext.Configuration["Logging:LogFilePath"];
                    });
                });
    }
}

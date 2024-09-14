using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Azure.Identity;
using Azure.Extensions.AspNetCore.Configuration.Secrets;
using AzureFileShareMonitorService.Services;
using AzureFileShareMonitorService.Logging;
using Azure.Security.KeyVault.Secrets;
using Azure.Core;

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
                        config.AddAzureKeyVault(keyVaultEndpoint, new KeyVaultSecretManager());
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

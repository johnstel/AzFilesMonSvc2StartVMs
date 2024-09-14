using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AzureFileShareMonitorService.Models;
using AzureFileShareMonitorService.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AzureFileShareMonitorService
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IFileShareMonitorService _monitorService;
        private readonly PollingSettings _pollingSettings;

        public Worker(ILogger<Worker> logger, IFileShareMonitorService monitorService, IOptions<PollingSettings> pollingOptions)
        {
            _logger = logger;
            _monitorService = monitorService;
            _pollingSettings = pollingOptions.Value;

            if (_pollingSettings.IntervalInSeconds < 30)
            {
                _logger.LogWarning("Polling interval is below 30 seconds. Setting it to 30 seconds.");
                _pollingSettings.IntervalInSeconds = 30;
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Service started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await _monitorService.MonitorAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred during monitoring.");
                }

                await Task.Delay(TimeSpan.FromSeconds(_pollingSettings.IntervalInSeconds), stoppingToken);
            }

            _logger.LogInformation("Service is stopping.");
        }
    }
}

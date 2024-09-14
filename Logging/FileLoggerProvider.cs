using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System;

namespace AzureFileShareMonitorService.Logging
{
    [ProviderAlias("File")]
    public class FileLoggerProvider : ILoggerProvider
    {
        private readonly FileLoggerOptions _options;
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);

        public FileLoggerProvider(IOptions<FileLoggerOptions> options)
        {
            _options = options.Value;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new FileLogger(_options, _lock);
        }

        public void Dispose()
        {
            _lock?.Dispose();
        }
    }

    public class FileLogger : ILogger
    {
        private readonly FileLoggerOptions _options;
        private readonly SemaphoreSlim _lock;

        public FileLogger(FileLoggerOptions options, SemaphoreSlim @lock)
        {
            _options = options;
            _lock = @lock;
        }

        public IDisposable BeginScope<TState>(TState state) => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= _options.MinimumLogLevel;

        // Updated method signature to prevent unobserved exceptions
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var message = $"{DateTime.UtcNow:u} [{logLevel}] {formatter(state, exception)}{Environment.NewLine}";

            // Synchronously log the message to prevent unobserved exceptions
            _lock.Wait();
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_options.LogFilePath));
                File.AppendAllText(_options.LogFilePath, message);
            }
            finally
            {
                _lock.Release();
            }
        }
    }

    public class FileLoggerOptions
    {
        public string LogFilePath { get; set; }
        public LogLevel MinimumLogLevel { get; set; } = LogLevel.Information;
    }
}

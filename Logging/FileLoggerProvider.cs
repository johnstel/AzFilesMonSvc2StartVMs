using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Text;
using System.Threading;

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

        // Updated to return a non-nullable IDisposable
        public IDisposable BeginScope<TState>(TState state) => NoopDisposable.Instance;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= _options.MinimumLogLevel;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var message = $"{DateTime.UtcNow:u} [{logLevel}] {formatter(state, exception)}{Environment.NewLine}";

            _lock.Wait();
            try
            {
                var directoryPath = Path.GetDirectoryName(_options.LogFilePath);
                if (!string.IsNullOrEmpty(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                File.AppendAllText(_options.LogFilePath, message, Encoding.UTF8);
            }
            finally
            {
                _lock.Release();
            }
        }
    }

    public class FileLoggerOptions
    {
        public string LogFilePath { get; set; } = string.Empty;
        public LogLevel MinimumLogLevel { get; set; } = LogLevel.Information;
    }

    // No-operation disposable to satisfy ILogger.BeginScope contract
    internal sealed class NoopDisposable : IDisposable
    {
        public static readonly NoopDisposable Instance = new NoopDisposable();

        private NoopDisposable() { }

        public void Dispose()
        {
            // No operation performed
        }
    }
}

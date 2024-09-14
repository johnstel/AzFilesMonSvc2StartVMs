using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace AzureFileShareMonitorService.Logging
{
    public static class LoggingExtensions
    {
        public static ILoggingBuilder AddFileLogger(this ILoggingBuilder builder, Action<FileLoggerOptions> configure)
        {
            builder.Services.AddSingleton<ILoggerProvider, FileLoggerProvider>();
            builder.Services.Configure(configure);
            return builder;
        }
    }
}

// See https://aka.ms/new-console-template for more information
using Microsoft.Extensions.Logging;
using ZLogger;

Console.WriteLine("Hello, World!");

using var factory = LoggerFactory.Create(builder =>
{
    builder.SetMinimumLevel(LogLevel.Trace);
    builder.AddZLoggerConsole(options =>
    {
        options.UsePlainTextFormatter(formatter =>
        {
            formatter.SetPrefixFormatter(
                $"{0:yyyy-MM-dd HH:mm:ss} [{1}] {2,-11} ",
                (in template, in info) => template.Format(info.Timestamp, info.Category, info.LogLevel));
        });
    });
});

var logger = factory.CreateLogger("Cat");
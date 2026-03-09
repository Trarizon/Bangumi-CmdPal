using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using Trarizon.Bangumi.CmdPal.Pages;
using ZLogger;

namespace Trarizon.Bangumi.CmdPal.Core;

internal sealed partial class BangumiContext(SettingsManager settings) : IDisposable
{
    public SettingsProvider Settings { get; } = new SettingsProvider(settings);
    public BangumiClient Client => field ??= new BangumiClient(Settings, LoggerFactory.CreateLogger<BangumiClient>());
    public ILoggerFactory LoggerFactory => field ??= Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
    {
#if DEBUG
        builder.SetMinimumLevel(LogLevel.Trace);
#else
        builder.SetMinimumLevel(LogLevel.Information);
#endif

        builder.AddZLoggerRollingFile(options =>
        {
            options.FilePathSelector = (time, i) => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "program-logs", $"bgm_cmdpal-{time:yyyyMMdd}_{i}.log");
            options.UsePlainTextFormatter(formatter =>
            {
                formatter.SetPrefixFormatter($"{0:yyyy-MM-dd HH:mm:ss} [{1}] [{2}]  ",
                    (in template, in info) => template.Format(info.Timestamp, info.LogLevel, info.Category));
            });
        });
    });
    public IMemoryCache Cache { get; } = new MemoryCache(new MemoryCacheOptions());

    public BangumiPage BangumiPage => field ??= new BangumiPage(Client, Settings, LoggerFactory.CreateLogger<BangumiPage>(),
        SearchPage, CollectionSearchPage);
    public SearchPage SearchPage => field ??= new SearchPage(Client, Settings, LoggerFactory.CreateLogger<SearchPage>());
    public CollectionSearchPage CollectionSearchPage => field ??= new CollectionSearchPage(Client, Settings, LoggerFactory.CreateLogger<CollectionSearchPage>());

    public void Dispose()
    {
        Client.Dispose();
        LoggerFactory.Dispose();

        BangumiPage.Dispose();
        SearchPage.Dispose();
        CollectionSearchPage.Dispose();
    }
}

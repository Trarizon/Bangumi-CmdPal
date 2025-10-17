using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Input;
using System;
using System.IO;
using Trarizon.Bangumi.CmdPal.Helpers;
using Trarizon.Bangumi.CmdPal.Utilities;
using ZLogger;

namespace Trarizon.Bangumi.CmdPal.Core;
internal sealed partial class BangumiExtensionContext : IDisposable
{
    private string _accessToken;
    private readonly ILoggerFactory _loggerFactory;

    public SettingsManager Settings { get; }

    public AuthorizableBangumiClient Client { get; private set; }

    public BangumiExtensionContext(SettingsManager settings)
    {
        Settings = settings;
        Settings.SettingsChanged += s =>
        {
            if (_accessToken != s.AccessToken) {
                _accessToken = s.AccessToken;
                Client?.Dispose();
                Client = new AuthorizableBangumiClient(_accessToken);
            }
        };

        _accessToken = settings.AccessToken;

        _loggerFactory = LoggerFactory.Create(builder =>
        {
#if DEBUG
            builder.SetMinimumLevel(LogLevel.Trace);
#else
            builder.SetMinimumLevel(LogLevel.Information);
#endif

            var file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "debug-bgm_cmdpal.txt");
            builder.AddZLoggerFile(file, options =>
            {
                options.UsePlainTextFormatter(formatter =>
                {
                    formatter.SetPrefixFormatter($"{0:yyyy-MM-dd HH:mm:ss} [{1}] {2,-11} ",
                        (in template, in info) => template.Format(info.Timestamp, info.Category, info.LogLevel));
                });
            });
        });

        Client = new AuthorizableBangumiClient(_accessToken);
    }

    public ILogger CreateLogger(string category)
        => _loggerFactory.CreateLogger(category);

    public void Dispose()
    {
        Client.Dispose();
        _loggerFactory.Dispose();
    }
}

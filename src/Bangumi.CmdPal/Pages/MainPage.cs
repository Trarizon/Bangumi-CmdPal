using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using System;
using System.Threading;
using System.Threading.Tasks;
using Trarizon.Bangumi.CmdPal.Core;
using Trarizon.Bangumi.CmdPal.Helpers;
using Trarizon.Bangumi.CmdPal.Utilities;

namespace Trarizon.Bangumi.CmdPal.Pages;

internal sealed partial class MainPage : ListPage, IDisposable
{
    private static readonly IListItem HomeListItem = new ListItem(new OpenUrlCommand(BangumiHelpers.HomeUrl) { Result = CommandResult.Dismiss() })
    {
        Title = "打开Bangumi",
        Subtitle = BangumiHelpers.HomeUrl,
    };
    private static readonly IListItem HelpListItem = new ListItem(new HelpPage())
    {
        Title = "帮助",
        Icon = CmdPalFactory.Icon("\uE90F"),
    };

    public MainPage(BangumiExtensionContext context)
    {
        _context = context;
        _context.Settings.SettingsChanged += async s =>
        {
            _cts.Reset();
            await SetSelfInfoAsync(_cts.Token).ConfigureAwait(false);
        };

        _searchListItem = new ListItem(_searchPage = new SubjectSearchPage(_context))
        {
            Title = "搜索条目",
            Icon = CmdPalFactory.Icon("\uE721"),
        };
        _collectionListItem = new ListItem()
        {
            Tags = [new Tag(":me")],
        };
        _ = SetSelfInfoAsync(_cts.Token);

        _items = [HomeListItem, HelpListItem, _searchListItem, _collectionListItem];
    }

    private readonly BangumiExtensionContext _context;
    private readonly ResettableCancellationTokenSource _cts = new();

    private readonly ListItem _searchListItem;
    private readonly SubjectSearchPage _searchPage;
    private readonly ListItem _collectionListItem;
    private CollectionSearchPage? _collectionSearchPage;
    private readonly IListItem[] _items;

    public override IListItem[] GetItems() => _items;

    private async Task SetSelfInfoAsync(CancellationToken cancellationToken)
    {
        _collectionListItem.Command = new NoOpCommand();
        _collectionListItem.Title = "认证中...";
        _collectionListItem.Icon = CmdPalFactory.Icon("\uE90F");

        var self = await _context.Client.GetAuthorizationAsync(cancellationToken).ConfigureAwait(false);
        if (self is null) {
            _collectionListItem.Title = "Access Token未认证";
            _collectionListItem.Icon = CmdPalFactory.Icon("\uE90F");
        }
        else {
            _collectionListItem.Command = _collectionSearchPage = new CollectionSearchPage(_context)
            {
                Name = "搜索我的时光机",
            };
            _collectionListItem.Title = "我的时光机";
            _collectionListItem.Subtitle = $"@{self.UserName}";
            _collectionListItem.MoreCommands = [
                new CommandContextItem(new OpenUrlCommand(BangumiHelpers.UserUrl(self)){
                    Name = "打开我的时光机",
                    Result = CommandResult.Dismiss(),
                }) {
                    Title = "打开我的时光机",
                }
            ];
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();

        _searchPage.Dispose();
        _collectionSearchPage?.Dispose();
    }
}

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Extensions.Logging;
using System;
using Trarizon.Bangumi.CmdPal.Core;
using Trarizon.Bangumi.CmdPal.Pages.ListItems;
using Trarizon.Bangumi.CmdPal.Toolkit;

#pragma warning disable BgmExprApi // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。

namespace Trarizon.Bangumi.CmdPal.Pages;

internal sealed partial class BangumiPage : ListPage, IDisposable
{
    private static readonly IListItem HomeListItem = new ListItem(
        new OpenUrlCommand(BangumiHelpers.HomeUrl) { Result = CommandResult.Dismiss() })
    {
        Title = "主页",
        Subtitle = BangumiHelpers.HomeUrl,
    };

    public BangumiPage(BangumiClient client,
        SettingsProvider settings, ILogger<BangumiPage> logger,
        SearchPage searchPage, CollectionSearchPage collectionSearchPage)
    {
        _client = client;
        _settings = settings;
        _settings.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName is nameof(SettingsProvider.AccessToken)) {
                _cts.Reset();
            }
        };

        _defaultItems = [
            _searchPageListItem = new ListItem(searchPage)
            {
                Title = "搜索条目",
                Icon = IconInfo.FromCode("\uE721"),
            },
            _userCollectionListItem = new UserListItem(_client, collectionSearchPage),
            HomeListItem,
        ];

        Title = "Bangumi";
        PlaceholderText = "键入以搜索条目";
        ShowDetails = true;

        logger.ZLogInitialized();
    }

    private BangumiClient _client;
    private SettingsProvider _settings;
    private ResettableCancellationTokenSource _cts = new();

    private readonly ListItem _searchPageListItem;
    private readonly UserListItem _userCollectionListItem;
    private readonly IListItem[] _defaultItems;

    public override IListItem[] GetItems() => _defaultItems;

    //public override void UpdateSearchText(string oldSearch, string newSearch)
    //{
    //    if (string.IsNullOrWhiteSpace(newSearch)) {
    //        SetItems(_defaultItems);
    //        return;
    //    }

    //    _searchPageListItem.Title = $"搜索条目: {newSearch}";
    //    SetItems([
    //        new ListItem(new AnonymousCommand(null){
    //            Result=CommandResult.Dismiss
    //        })
    //        {
    //            Title = $"搜索条目：{newSearch}",
    //            Icon = CmdPalFactory.Icon("\uE721"),
    //        }
    //    ]);
    //}



    public void Dispose()
    {
        _cts.Dispose();
        _userCollectionListItem.Dispose();
    }
}

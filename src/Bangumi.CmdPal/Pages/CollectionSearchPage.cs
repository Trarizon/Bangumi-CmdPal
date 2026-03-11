using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Trarizon.Bangumi.Api.Responses.Models.Collections;
using Trarizon.Bangumi.Api.Routes;
using Trarizon.Bangumi.Api.Toolkit;
using Trarizon.Bangumi.CmdPal.Core;
using Trarizon.Bangumi.CmdPal.Helpers;
using Trarizon.Bangumi.CmdPal.Pages.ListItems;
using Trarizon.Bangumi.CmdPal.Toolkit;
using ZLogger;

namespace Trarizon.Bangumi.CmdPal.Pages;

internal partial class CollectionSearchPage : ArrayDynamicListPage, IDisposable
{
    private static readonly ICommandItem NoResultCommandItem = new CommandItem(new NoOpCommand()) { Title = "无搜索结果", Icon = new IconInfo("\uE946") };

    public CollectionSearchPage(BangumiClient client, SettingsProvider settings, ILogger<CollectionSearchPage> logger)
    {
        _client = client;
        _settings = settings;
        _logger = logger;

        _searchHintListItem = new ListItem
        {
            Title = "搜索我的时光机",
            Icon = IconInfo.FromCode("\uE721"),
            Command = new AnonymousCommand(() =>
            {
                _cts.Reset();
                _ = SearchAsync(_searchText, SubjectCollectionType.Doing, _cts.Token);
            })
            {
                Name = "搜索",
                Result = CommandResult.KeepOpen(),
            }
        };

        Title = "我的时光机";
        Name = "搜索我的时光机";
        PlaceholderText = "键入以搜索时光机条目";
        ShowDetails = true;

        SetItem(_searchHintListItem);
    }

    private readonly BangumiClient _client;
    private readonly SettingsProvider _settings;
    private readonly ILogger _logger;
    private readonly ListItem _searchHintListItem;

    private readonly ResettableCancellationTokenSource _cts = new();

    private string _searchText = "";

    private void SetSearchText(string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText)) {
            _searchText = "";
            _searchHintListItem.Title = "搜索我的时光机";
        }
        else {
            _searchText = searchText;
            _searchHintListItem.Title = $"在我的时光机中搜索: {searchText}";
        }
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        if (string.IsNullOrWhiteSpace(newSearch)) {
            _cts.Cancel();
            SetSearchText("");
            SetItem(_searchHintListItem);
            return;
        }

        SetSearchText(newSearch);
        SetItem(_searchHintListItem);
    }

    private async Task SearchAsync(string searchText, SubjectCollectionType collectionType, CancellationToken cancellationToken)
    {
        using (this.EnterLoadingScope()) {

            var self = await _client.GetAuthorizationAsync(cancellationToken).ConfigureAwait(false);
            if (self is null)
                return;

            if (string.IsNullOrWhiteSpace(searchText)) {
                _logger.ZLogTrace($"Search empty");

                var collections = await _client.GetPagedUserSubjectCollectionsAsync(self.UserName,
                    collectionType: collectionType,
                    pagination: new(_settings.SearchCount),
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                _logger.ZLogTrace($"Search empty done");

                var result = collections.Datas
                    .Select(col => new SubjectListItem(col, _client, _logger, cancellationToken))
                    .ToArray();
                SetItems(result, NoResultCommandItem);
                return;
            }
            else {
                var result = await _client.GetUserSubjectCollections(self.UserName,
                    collectionType: collectionType)
                    .Where(x =>
                    {
                        return x.Subject.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase)
                            || x.Subject.ChineseName.Contains(searchText, StringComparison.OrdinalIgnoreCase);
                    })
                    .Take(_settings.SearchCount)
                    .Select(col => new SubjectListItem(col, _client, _logger, cancellationToken))
                    .ToArrayAsync(cancellationToken).ConfigureAwait(false);

                SetItems(result, NoResultCommandItem);
                return;
            }
        }
    }

    public void Dispose()
    {
        _cts.Dispose();
    }
}

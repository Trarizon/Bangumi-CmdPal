using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Trarizon.Bangumi.Api.Requests;
using Trarizon.Bangumi.Api.Responses.Models;
using Trarizon.Bangumi.Api.Routes;
using Trarizon.Bangumi.CmdPal.Core;
using Trarizon.Bangumi.CmdPal.Pages.ListItems;
using Trarizon.Bangumi.CmdPal.Toolkit;
using ZLogger;

#pragma warning disable BgmExprApi

namespace Trarizon.Bangumi.CmdPal.Pages;

internal sealed partial class SearchPage : ArrayDynamicListPage, IDisposable
{
    private static readonly CommandItem SearchHintResult = new CommandItem("键入以搜索条目")
    {
        Icon = IconInfo.FromCode("\uE721"),
    };

    private readonly ListItem _searchHintListItem;
    private readonly CommandItem _emptyResult;

    public SearchPage(BangumiClient client, SettingsProvider settings, ILogger<SearchPage> logger)
    {
        _client = client;
        _logger = logger;
        _settings = settings;
        _searchHintListItem = new ListItem(new AnonymousCommand(Search)
        {
            Name = "搜索",
            Result = CommandResult.KeepOpen(),
        })
        {
            Title = "搜索条目",
            Icon = IconInfo.FromCode("\uE721"),
        };
        _emptyResult = new CommandItem("No Result")
        {
            Icon = IconInfo.FromCode("\uE721")
        };

        Title = "搜索条目";
        Name = "搜索";
        ShowDetails = true;
        var filters = new ArrayFilters([
            new Filter
            {
                Id = SubjectType.All.ToFilterId(),
                Name = SubjectType.All.ToDisplayString(),
                Icon = SubjectType.All.GetIconInfo(),
            },
            new Filter
            {
                Id = SubjectType.Anime.ToFilterId(),
                Name = SubjectType.Anime.ToDisplayString(),
                Icon = SubjectType.Anime.GetIconInfo(),
            },
            new Filter
            {
                Id = SubjectType.Book.ToFilterId(),
                Name = SubjectType.Book.ToDisplayString(),
                Icon = SubjectType.Book.GetIconInfo(),
            },
            new Filter
            {
                Id = SubjectType.Game.ToFilterId(),
                Name = SubjectType.Game.ToDisplayString(),
                Icon = SubjectType.Game.GetIconInfo(),
            },
            new Filter
            {
                Id = SubjectType.Music.ToFilterId(),
                Name = SubjectType.Music.ToDisplayString(),
                Icon = SubjectType.Music.GetIconInfo(),
            },
            new Filter
            {
                Id = SubjectType.Real.ToFilterId(),
                Name = SubjectType.Real.ToDisplayString(),
                Icon = SubjectType.Real.GetIconInfo(),
            }
        ]);
        Filters = filters;
        filters.PropChanged += (s, e) =>
        {
            if (e.PropertyName is nameof(ArrayFilters.CurrentFilterId)) {
                Search();
            }
        };

        EmptyContent = SearchHintResult;

        logger.ZLogInitialized();
    }

    private readonly BangumiClient _client;
    private readonly ILogger _logger;
    private readonly SettingsProvider _settings;

    private readonly ResettableCancellationTokenSource _cts = new();

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        _cts.Cancel();
        if (string.IsNullOrWhiteSpace(newSearch)) {
            SetItems([], SearchHintResult);
            return;
        }
        SetItems([_searchHintListItem]);
    }

    public override async void LoadMore()
    {
        await SearchAsync(SearchText, false, _cts.Token);
    }

    private void Search()
    {
        _cts.Reset();
        if (string.IsNullOrWhiteSpace(SearchText)) {
            SetItems([], SearchHintResult);
            return;
        }
        _ = SearchAsync(SearchText, true, _cts.Token);
    }

    public async Task SearchAsync(string searchText, bool isFirst, CancellationToken cancellationToken = default)
    {
        using (this.EnterLoadingScope()) {
            _emptyResult.Subtitle = $"搜索：{searchText}";

            if (string.IsNullOrWhiteSpace(searchText)) {
                SetItems([], _emptyResult);
                return;
            }

            var result = await _client.Client.SearchPagedSubjectsAsync(
                new() { Keyword = searchText, Filter = new() { Types = SubjectType.FromFilterId(Filters?.CurrentFilterId) is var t && t == SubjectType.All ? [] : [t] } },
                new Pagination(_settings.SearchCount, 0),
                cancellationToken).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            _logger.ZLogInformation($"Search '{searchText}' filter '{Filters?.CurrentFilterId}' get {result.Offset}+{result.Datas.Length}/{result.Total} results");

            SetItems([.. isFirst ? [] : Items, .. result.Datas.Select(x => new SubjectListItem(x, _client, cancellationToken))],
                _emptyResult,
                result.Total > Items.Length + result.Datas.Length);
        }
    }

    public void Dispose() => _cts.Dispose();
}

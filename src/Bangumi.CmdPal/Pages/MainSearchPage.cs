using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Trarizon.Bangumi.Api.Exceptions;
using Trarizon.Bangumi.Api.Requests;
using Trarizon.Bangumi.Api.Requests.Payloads;
using Trarizon.Bangumi.Api.Responses.Models;
using Trarizon.Bangumi.Api.Responses.Models.Collections;
using Trarizon.Bangumi.Api.Routes;
using Trarizon.Bangumi.Api.Toolkit;
using Trarizon.Bangumi.CmdPal.Core;
using Trarizon.Bangumi.CmdPal.Helpers;
using Trarizon.Bangumi.CmdPal.Helpers.Searching;
using Trarizon.Bangumi.CmdPal.Pages.ListItems;
using Trarizon.Bangumi.CmdPal.Utilities;
using Trarizon.Library.Functional;

#pragma warning disable BgmExprApi // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。

namespace Trarizon.Bangumi.CmdPal.Pages;

[Obsolete("No longer used, remaining for reference")]
internal sealed partial class MainSearchPage : DynamicListPage, IDisposable
{
    private const int AsyncPageCollectionRequestInterval = 100;

    private static IListItem HomeListItem { get; } = new ListItem(BangumiHelpers.OpenHomeUrlCommand()) { Title = "打开Bangumi", Subtitle = BangumiHelpers.HomeUrl };
    private static IListItem HelpListItem { get; } = new ListItem(new HelpPage()) { Title = "帮助", Icon = new IconInfo("\uE90F") };

    private static ICommandItem UnauthorizedCommandItem => field ??= new CommandItem(new NoOpCommand()) { Title = "未认证", Icon = new IconInfo("\uE7BA") };
    private static ICommandItem NoResultCommandItem => field ??= new CommandItem(new NoOpCommand()) { Title = "无搜索结果", Icon = new IconInfo("\uE946") };

    private readonly BangumiExtensionContext _context;
    private SettingsManager _settings => _context.Settings;
    private readonly Debouncer<SearchOptions> _debouncer;
    private readonly ResettableCancellationTokenSource _cts = new();

    private string _accessToken;
    public AuthorizableBangumiClient Client => _context.Client;

    public MainSearchPage(BangumiExtensionContext context)
    {
        _context = context;

        _debouncer = new();
        _accessToken = _settings.AccessToken;

        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        Title = "Bangumi";
        Name = "Bangumi";
        PlaceholderText = "键入以搜索条目";
        ShowDetails = true;

        Filters = new Filt();
    }

    public partial class Filt : Filters
    {
        public override IFilterItem[] GetFilters() => [
            new Filter{Id="all",Name="All"},
            new Filter{Id="me",Name="ME"},
            ];
    }

    private IListItem[] Items { get => field; set { if (field != value) { field = value; RaiseItemsChanged(value.Length); } } } = [];

    public override IListItem[] GetItems() => Items;

    public override async void UpdateSearchText(string oldSearch, string newSearch)
    {
        Debugging.Log($"Handle search {oldSearch} -> {newSearch}");
        if (string.IsNullOrWhiteSpace(newSearch)) {
            _debouncer.CancelInvoke();
            IsLoading = true;
            Items = await GetDefaultListItemsAsync().ConfigureAwait(false);
            IsLoading = false;
            return;
        }

        Debugging.Log($"Auto search: {_settings.AutoSearch}");
        if (_settings.AutoSearch) {
            AutoSearchHandler(newSearch);
        }
        else {
            IsLoading = false;
            var searchOptions = SearchOptions.Parse(newSearch);
            Items = [
                GetSearchListItem(searchOptions,_cts.Token),
                .. Optional.Create(newSearch.EndsWith(" :", StringComparison.Ordinal), searchOptions)
                    .Match(opt => GetTextSuggestions(opt), () => [])
            ];
        }
    }

    private void AutoSearchHandler(string search)
    {
        var options = SearchOptions.Parse(search);
        if (search.EndsWith(" :", StringComparison.Ordinal)) {
            _cts.Cancel();
            Items = GetTextSuggestions(options);
            IsLoading = false;
            return;
        }

        _debouncer.DelayInvoke(options, async (options, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            using (this.EnterLoadingScope()) {
                var result = await SearchForListItemsAsync(options, cancellationToken).ConfigureAwait(false);
                result.Match(
                    items => (Items, EmptyContent) = (items, null),
                    empty => (Items, EmptyContent) = ([], empty));
            }
        }, _settings.SearchDebounce, _cts.Token);
    }

    private async ValueTask<IListItem[]> GetDefaultListItemsAsync()
    {
        var self = await Client.GetAuthorizationAsync().ConfigureAwait(false);
        return [
            HomeListItem,
            HelpListItem,
            .. Optional.OfNotNull(self)
                .Select(self => new ListItem(BangumiHelpers.OpenUserUrlCommand(self)) { Title = "我的时光机", Subtitle = $"@{self.UserName}" })
        ];
    }

    private ListItem GetSearchListItem(SearchOptions searchOptions, CancellationToken cancellationToken)
    {
        return new ListItem(new AnonymousCommand(async () =>
        {
            using (this.EnterLoadingScope()) {
                var res = await SearchForListItemsAsync(searchOptions, cancellationToken).ConfigureAwait(false);
                res.Match(
                    items => { EmptyContent = null; Items = items; },
                    item => { EmptyContent = item; Items = []; });
            }
        })
        {
            Result = CommandResult.KeepOpen(),
        })
        {
            Icon = new IconInfo("\uE721"),
            Title = searchOptions.Me
                ? $"搜索在看作品: {searchOptions.Keywords}"
                : $"搜索 {searchOptions.Keywords}",
        };
    }

    private static IListItem[] GetTextSuggestions(SearchOptions searchOptions)
    {
        var suggestions = new List<IListItem>();
        return searchOptions.GetUnsetOptions()
            .Select(info => new ListItem()
            {
                Icon = new IconInfo("\uE713"),
                Title = $":{info.Option}",
                Subtitle = info.Description,
                TextToSuggest = searchOptions.InputString + info.Option
            })
            .ToArray();
    }

    private async Task<Result<IListItem[], ICommandItem>> SearchForListItemsAsync(SearchOptions searchOptions, CancellationToken cancellationToken)
    {
        try {
            if (searchOptions.Me) {
                var res = await CoreForUserCollection(searchOptions.Keywords.ToString(),
                    searchOptions.SubjectTypes is [var first, ..] ? first : null,
                    searchOptions.Page, cancellationToken)
                    .ConfigureAwait(false);
                return res.ToResult(UnauthorizedCommandItem)
                    .Bind(arr => Result.Create(arr is not [], arr, NoResultCommandItem));
            }
            else {
                return Optional.Of(await Core(searchOptions.Keywords.ToString(),
                    searchOptions.SubjectTypes,
                    searchOptions.Page, cancellationToken).ConfigureAwait(false))
                    .Where(x => x is not [])
                    .ToResult(NoResultCommandItem);
            }
        }
        catch (BangumiApiException ex) {
            DebugMessage($"Request Error {ex.Message}");
            return Result.Success<IListItem[]>([new ListItem(new NoOpCommand()) {
                Title = $"Request Error: {ex.Message}",
                Details = new Details {
                    Body = ex.StackTrace ?? "",
                }
            }]);
        }
        catch (OperationCanceledException) {
            DebugMessage("Search cancelled");
            throw;
        }
        catch (Exception ex) {
            DebugMessage($"Error {ex.Message}");
            return Result.Success<IListItem[]>([new ListItem(new NoOpCommand()) {
                Title = $"Error: {ex.Message}",
                Details = new Details {
                    Body = ex.StackTrace ?? "",
                }
            }]);
        }

        async Task<Optional<IListItem[]>> CoreForUserCollection(string keyword, SubjectType? type, int page, CancellationToken cancellationToken)
        {
            var self = await Client.GetAuthorizationAsync(cancellationToken).ConfigureAwait(false);
            if (self is null) {
                return default;
            }

            if (string.IsNullOrWhiteSpace(keyword)) {
                var collections = await Client.GetPagedUserSubjectCollectionsAsync(self.UserName,
                    subjectType: type,
                    collectionType: SubjectCollectionType.Doing,
                    pagination: new(_settings.SearchCount, page * _settings.SearchCount),
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                Debugging.Log(string.Join("\n", collections.Datas.Select(x => $"{x.Subject.Name} - {x.Subject.ChineseName}")));
                return collections.Datas
                    .Select(col => new UserSubjectCollectionListItem(_context, col, cancellationToken))
                    .ToArray();
            }

            Debugging.Log($"----- Search collection '{keyword}', page {page}, take {_settings.SearchCount} -----\n");
            return await Client.GetUserSubjectCollections(self.UserName,
                subjectType: type,
                collectionType: SubjectCollectionType.Doing)
                .Where(x =>
                {
                    Debugging.Log($"{x.Subject.Name} - {x.Subject.ChineseName}");
                    return x.Subject.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                        || x.Subject.ChineseName.Contains(keyword, StringComparison.OrdinalIgnoreCase);
                })
                .Skip(page * _settings.SearchCount)
                .Take(_settings.SearchCount)
                .Select(collection => new UserSubjectCollectionListItem(_context, collection, cancellationToken))
                .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        }

        async Task<IListItem[]> Core(string keyword, List<SubjectType> types, int page, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return [];

            var subjects = await Client.SearchPagedSubjectsAsync(new SearchSubjectsRequestBody
            {
                Keyword = keyword,
                Filter = new()
                {
                    Types = types,
                }
            }, new(_settings.SearchCount, page * _settings.SearchCount), cancellationToken).ConfigureAwait(false);
            return subjects.Datas
                .Select(x => new SubjectListItem(_context, x, cancellationToken))
                .ToArray();
        }
    }

    // TODO: 我不知道这个什么时候会调用，可能根本不会
    public void Dispose()
    {
        _debouncer.Dispose();
        Client.Dispose();
    }

    [Conditional("DEBUG")]
    private void DebugMessage(string message)
    {
        Title = message;
    }
}

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Trarizon.Bangumi.Api.Exceptions;
using Trarizon.Bangumi.Api.Models.UserModels;
using Trarizon.Bangumi.Api.Requests;
using Trarizon.Bangumi.Api.Routes;
using Trarizon.Bangumi.Api.Toolkit;
using Trarizon.Bangumi.CommandPalette.Helpers;
using Trarizon.Bangumi.CommandPalette.Pages.ListItems;
using Trarizon.Bangumi.CommandPalette.Utilities;
using Trarizon.Library.Functional;

#pragma warning disable BgmExprApi // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。

namespace Trarizon.Bangumi.CommandPalette.Pages;

internal sealed partial class MainSearchPage : DynamicListPage, IDisposable
{
    private const int AsyncPageCollectionRequestInterval = 100;

    private static IListItem HomeListItem => field ??= new ListItem(BangumiHelpers.OpenHomeUrlCommand()) { Title = "打开Bangumi", Subtitle = BangumiHelpers.HomeUrl };
    private static IListItem HelpListItem => field ??= new ListItem(new HelpPage()) { Title = "帮助", Icon = new IconInfo("\uE90F") };
    private static IListItem UnauthorizedListItem => field ??= new ListItem(new NoOpCommand()) { Title = "未认证", Icon = new IconInfo("\uE7BA") };
    private static IListItem NoResultListItem => field ??= new ListItem(new NoOpCommand()) { Title = "无搜索结果", Icon = new IconInfo("\uE946") };

    private readonly SettingsManager _settings;
    private Debouncer<string> _debouncer;
    private readonly ResettableCancellationTokenSource _cts = new();

    private string _accessToken;
    public AuthorizableBangumiClient Client { get; private set; }

    public MainSearchPage(SettingsManager settingsManager)
    {
        _settings = settingsManager;
        _debouncer = new(UpdateSearchAsync);
        _accessToken = _settings.AccessToken;
        Client = new AuthorizableBangumiClient(_accessToken);

        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        Title = "Bangumi";
        Name = "Bangumi";
        PlaceholderText = "键入以搜索条目";
        ShowDetails = true;
    }

    private IListItem[] Items { get => field; set { field = value; RaiseItemsChanged(value.Length); } } = [];

    public override IListItem[] GetItems() => Items;

    public override async void UpdateSearchText(string oldSearch, string newSearch)
    {
        DebugMessage($"Handle search {oldSearch} -> {newSearch}");
        if (string.IsNullOrWhiteSpace(newSearch)) {
            _debouncer.CancelInvoke();
            IsLoading = false;
            Items = await GetDefaultListItemsAsync().ConfigureAwait(false);
            return;
        }

        _debouncer.DelayInvoke(newSearch, _settings.SearchDebounce);
    }

    private async Task UpdateSearchAsync(string keyword, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureClientUpdated();

        IsLoading = true;
        var trimmed = keyword.AsSpan().Trim(' ');
        try {
            var items = new List<IListItem>();
            items.AddOptional(Optional.Create("help".StartsWith(trimmed), HelpListItem));
            items.AddOptional(Optional.Create("bgm".StartsWith(trimmed) || "bangumi".StartsWith(trimmed), HomeListItem));

            // 搜索
            items.AddRange(await SearchForListItemsAsync(keyword, cancellationToken).ConfigureAwait(false));

            Items = items.ToArray();
        }
        catch (OperationCanceledException) {
            return;
        }
        catch {
            DebugMessage("Error from searching");
        }
        finally {
            IsLoading = false;
        }
    }

    private async ValueTask<IListItem[]> GetDefaultListItemsAsync() => [
        HomeListItem,
        HelpListItem,
        .. Optional.OfNotNull(await Client.GetSelfAsync().ConfigureAwait(false))
            .Select(x => new ListItem(BangumiHelpers.OpenUserUrlCommand(x)) { Title = "我的时光机", Subtitle = $"@{x.UserName}" })
    ];

    private void EnsureClientUpdated()
    {
        // HACK: 设置里修改access token没有notify，所以每次请求前检查一下
        // 实现很丑，但是懒得搞了
        if (_accessToken != _settings.AccessToken) {
            _accessToken = _settings.AccessToken;
            Client.Dispose();
            Client = new AuthorizableBangumiClient(_accessToken);
        }
    }

    private async Task<IListItem[]> SearchForListItemsAsync(string searchKeyword, CancellationToken cancellationToken)
    {
        // Login
        //if (TryGetLoginCommand(searchKeyword, out var login)) {
        //    return [LoginListItem];
        //}

        // Check flags
        var search = ParseSearchKeywords(searchKeyword);

        DebugMessage($"Searching '{search.Keywords}' ({search.Page} {_settings.SearchCount}){(search.Me ? " :me" : null)}");

        // Search
        try {
            IListItem[] searchItems = search.Me
                ? await CoreForUserCollection(search.Keywords.ToString(), search.Page, cancellationToken).ConfigureAwait(false)
                : await Core(search.Keywords.ToString(), search.Page, cancellationToken).ConfigureAwait(false);

            DebugMessage("Searched");
            if (searchItems is []) {
                return [NoResultListItem];
            }
            return searchItems;
        }
        catch (BangumiApiException ex) {
            DebugMessage($"Request Error {ex.Message}");
            return [new ListItem(new NoOpCommand()) {
                Title = $"Request Error: {ex.Message}",
                Details = new Details {
                    Body = ex.StackTrace ?? "",
                }
            }];
        }
        catch (OperationCanceledException) {
            DebugMessage("Search cancelled");
            throw;
        }
        catch (Exception ex) {
            DebugMessage($"Error {ex.Message}");
            return [new ListItem(new NoOpCommand()) {
                Title = $"Error: {ex.Message}",
                Details = new Details {
                    Body = ex.StackTrace ?? "",
                }
            }];
        }

        async Task<IListItem[]> CoreForUserCollection(string keyword, int page, CancellationToken cancellationToken)
        {
            var self = await Client.GetSelfAsync(_cts.Token).ConfigureAwait(false);
            if (self is null) {
                return [UnauthorizedListItem];
            }

            if (keyword is "") {
                var collections = await Client.GetPagedUserSubjectCollectionsAsync(self.UserName,
                    collectionType: SubjectCollectionType.Doing,
                    pageLimit: _settings.SearchCount,
                    pageOffset: page * _settings.SearchCount,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                return collections.Datas
                    .Select(col => new UserSubjectCollectionListItem(this, col, cancellationToken))
                    .ToArray();
            }

            Debugging.Log("----- Search Keyword: " + keyword + " -----\n");
            return await Client.GetUserSubjectCollections(self.UserName,
                collectionType: SubjectCollectionType.Doing,
                options: new() { RequestInterval = TimeSpan.FromMilliseconds(AsyncPageCollectionRequestInterval) })
                .Where(x =>
                {
                    Debugging.Log($"{x.Subject.Name} - {x.Subject.ChineseName}\n");
                    return x.Subject.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                        || x.Subject.ChineseName.Contains(keyword, StringComparison.OrdinalIgnoreCase);
                })
                .Select(collection => new UserSubjectCollectionListItem(this, collection, cancellationToken))
                .Skip(page)
                .Take(_settings.SearchCount)
                .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        }

        async Task<IListItem[]> Core(string keyword, int page, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return [];
            
            var subjects = await Client.SearchPagedSubjectsAsync(new SearchSubjectsRequestBody
            {
                Keyword = keyword,
            }, _settings.SearchCount, page * _settings.SearchCount, cancellationToken).ConfigureAwait(false);
            return subjects.Datas
                .Select(x => new SubjectListItem(x))
                .ToArray();
        }
    }

    private static SearchOptions ParseSearchKeywords(string inputKeyword)
    {
        // 输入规则：
        // 以空格分割，':'开头为选项，其他为值
        // 选项可以出现在开头或结尾
        // 出现第一个值以后，后续直到第一个选项为止的部分为实际搜索关键字，后续的值会被忽略
        // :opt :a search keywords: example :trailing data :>>
        // ^ option                         ^option        ^option
        //         ^ search keywords                  ^ignored

        var keyword = inputKeyword.AsSpan();
        int kwstart = -1;
        int kwend = -1;
        bool me = false;
        int page = 0;

        foreach (var range in keyword.Split(' ')) {
            var split = keyword[range];
            if (split.IsEmpty)
                continue;
            if (split[0] != ':') {
                if (kwstart < 0)
                    kwstart = range.Start.GetOffset(keyword.Length);
                continue;
            }
            if (kwstart >= 0 && kwend < 0) {
                kwend = range.Start.GetOffset(keyword.Length);
            }

            var option = split[1..];
            if (option.Equals("me", StringComparison.OrdinalIgnoreCase)) {
                me = true;
                continue;
            }
            if (IsPagination(option)) {
                page = option.Length;
                continue;
            }
        }

        ReadOnlySpan<char> kw = (kwstart, kwend) switch
        {
            ( < 0, _) => [],
            (_, < 0) => keyword[kwstart..].TrimEnd(),
            (_, _) => keyword[kwstart..kwend].TrimEnd(),
        };

        return new SearchOptions(kw)
        {
            Me = me,
            Page = page
        };

        static bool IsPagination(ReadOnlySpan<char> option)
        {
            if (option.Length == 0)
                return false;
            foreach (var ch in option) {
                if (ch is not '>')
                    return false;
            }
            return true;
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

    private readonly ref struct SearchOptions(ReadOnlySpan<char> keywords)
    {
        public ReadOnlySpan<char> Keywords { get; } = keywords;

        public bool Me { get; init; }
        public int Page { get; init; }
    }
}

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Trarizon.Bangumi.Api.Responses;
using Trarizon.Bangumi.Api.Responses.Models;
using Trarizon.Bangumi.Api.Routes;
using Trarizon.Bangumi.Api.Toolkit;
using Trarizon.Bangumi.Api.Toolkit.Collections;
using Trarizon.Bangumi.CmdPal.Core;
using Trarizon.Bangumi.CmdPal.Helpers;
using Trarizon.Bangumi.CmdPal.Pages.Filters;
using Trarizon.Bangumi.CmdPal.Pages.ListItems;
using Trarizon.Bangumi.CmdPal.Utilities;
using Trarizon.Library.Functional;

namespace Trarizon.Bangumi.CmdPal.Pages;

internal partial class SubjectSearchPage : DynamicListPage, IDisposable
{
    private static readonly ICommandItem SearchHintCommandItem = new CommandItem(new NoOpCommand()) { Title = "搜索条目", Icon = new IconInfo("\uE721") };
    private static readonly ICommandItem NoResultCommandItem = new CommandItem(new NoOpCommand()) { Title = "无搜索结果", Icon = new IconInfo("\uE946") };

    public SubjectSearchPage(BangumiExtensionContext context)
    {
        _context = context;

        Title = "搜索条目";
        PlaceholderText = "键入以搜索条目";
        ShowDetails = true;

        _searchListItems = [
            _searchListItem = new ListItem(new AnonymousCommand(() =>
            {
                _cts.Reset();
                _ = SearchAsync(_searchText, _cts.Token);
                //_ = SearchFirstPageAsync(_searchText, _cts.Token);
            }){
                Name = "搜索",
                Result = CmdPalFactory.KeepOpenResult()
            }) {
                Title = "搜索条目",
                Icon = CmdPalFactory.Icon("\uE721"),
            }
        ];

        EmptyContent = SearchHintCommandItem;
        _items = [];
    }

    private readonly BangumiExtensionContext _context;
    private readonly ResettableCancellationTokenSource _cts = new();

    private readonly IListItem[] _searchListItems;
    private readonly ListItem _searchListItem;

    private string _searchText = "";

    private int _searchResultCount;
    private IAsyncEnumerable<IListItem> _searchResults = default!;

    #region Items

    public override ICommandItem? EmptyContent
    {
        get => base.EmptyContent;
        set {
            if (base.EmptyContent != value) {
                base.EmptyContent = value;
            }
        }
    }

    public override bool HasMoreItems
    {
        get => base.HasMoreItems;
        set {
            if (base.HasMoreItems != value) {
                base.HasMoreItems = value;
            }
        }
    }

    private IListItem[] _items;
    public override IListItem[] GetItems() => _items;
    private void SetItems(IListItem[] items, bool hasMoreItems = false)
    {
        if (_items != items) {
            _items = items;
            RaiseItemsChanged(items.Length);
        }

        HasMoreItems = hasMoreItems;
    }

    #endregion

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        if (string.IsNullOrWhiteSpace(newSearch)) {
            _cts.Cancel();
            _searchText = "";
            _searchListItem.Title = "搜索条目";
            SetItems([]);
            EmptyContent = SearchHintCommandItem;
            return;
        }

        _searchText = newSearch;
        _searchListItem.Title = $"搜索条目: {newSearch}";
        SetItems(_searchListItems);
        EmptyContent = NoResultCommandItem;
    }

    public override async void LoadMore()
    {
        using (this.EnterLoadingScope()) {
            var newLength = _items.Length + _context.Settings.SearchCount;
            var items = await _searchResults.Take(newLength).ToArrayAsync(_cts.Token).ConfigureAwait(false);
            SetItems(items, items.Length < _searchResultCount);
        }

        return;
    }

#pragma warning disable BgmExprApi // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。

    private async Task SearchAsync(string searchText, CancellationToken cancellationToken)
    {
        using (this.EnterLoadingScope()) {

            if (string.IsNullOrWhiteSpace(searchText)) {
                SetItems([]);
                return;
            }

            var result = _context.Client.SearchSubjects(new() { Keyword = searchText }, _context.Settings.SearchCount);
            _searchResultCount = await result.CountAsync(cancellationToken).ConfigureAwait(false);
            _searchResults = result
                .Select(x =>
                {
                    Debugging.Log($"enumerated in search infinite: {x.Name}");
                    return new SubjectListItem(_context, x, cancellationToken);
                })
                .CacheEnumerated();

            Debugging.Log($"enumerated in search infinite done, total count: {_searchResultCount}");
            var items = await _searchResults.Take(_context.Settings.SearchCount).ToArrayAsync(_cts.Token).ConfigureAwait(false);
            SetItems(items, items.Length < _searchResultCount);
        }
    }

    public void Dispose()
    {
        _cts.Dispose();
    }
}

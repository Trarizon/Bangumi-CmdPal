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
using Trarizon.Bangumi.CmdPal.Core;
using Trarizon.Bangumi.CmdPal.Helpers;
using Trarizon.Bangumi.CmdPal.Pages.ListItems;
using Trarizon.Bangumi.CmdPal.Utilities;

namespace Trarizon.Bangumi.CmdPal.Pages;

internal partial class SearchPage : DynamicListPage, IDisposable
{
    public SearchPage(BangumiExtensionContext context)
    {
        _context = context;

        Title = "搜索条目";
        PlaceholderText = "键入以搜索条目";
        ShowDetails = true;

        _searchListItems = [
            _searchListItem = new ListItem(new AnonymousCommand(() =>
            {
                _cts.Reset();
                _ = SearchFirstPageAsync(_searchText, _cts.Token);
            }){
                Name = "搜索",
                Result = CmdPalFactory.KeepOpenResult()
            }) {
                Title = "搜索条目",
                Icon = CmdPalFactory.Icon("\uE721"),
            }
        ];
    }

    private readonly BangumiExtensionContext _context;
    private readonly ResettableCancellationTokenSource _cts = new();

    private readonly IListItem[] _searchListItems;
    private readonly ListItem _searchListItem;

    private string _searchText = "";

    #region Items

    private IListItem[] _items = [];
    public override IListItem[] GetItems() => _items;
    private void SetItems(IListItem[] items, bool hasMoreItems = false)
    {
        if (_items != items) {
            _items = items;
            RaiseItemsChanged(items.Length);
        }

        if (HasMoreItems != hasMoreItems) {
            HasMoreItems = hasMoreItems;
        }
    }

    #endregion

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        if (string.IsNullOrWhiteSpace(newSearch)) {
            _cts.Cancel();
            _searchText = "";
            _searchListItem.Title = "搜索条目";
            SetItems([]);
            return;
        }

        _searchText = newSearch;
        _searchListItem.Title = $"搜索条目: {newSearch}";
        SetItems(_searchListItems);
    }

    public override async void LoadMore()
    {
        var searchPage = _items.Length / _context.Settings.SearchCount;

        using (this.EnterLoadingScope()) {
            var newPage = await SearchSubjectsAsync(_searchText, searchPage, _cts.Token).ConfigureAwait(false);
            if (newPage is null) {
                HasMoreItems = false;
                return;
            }

            SetItems(
                [.. _items, .. newPage.Datas.Select(x => new SubjectListItem(_context, x, _cts.Token))],
                hasMoreItems: newPage.Offset + newPage.Datas.Length < newPage.Total);
        }
    }

    private async Task SearchFirstPageAsync(string searchText, CancellationToken cancellationToken)
    {
#pragma warning disable BgmExprApi // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。
        using (this.EnterLoadingScope()) {

            var page = await SearchSubjectsAsync(searchText, 0, cancellationToken).ConfigureAwait(false);
            if (page is null) {
                SetItems([], hasMoreItems: false);
                return;
            }

            var result = page.Datas
                .Select(x => new SubjectListItem(_context, x, cancellationToken))
                .ToArray();
            SetItems(result, hasMoreItems: page.Offset + page.Datas.Length < page.Total);
        }
    }

    private async Task<PagedData<SearchResponsedSubject>?> SearchSubjectsAsync(string searchText, int searchPage, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(searchText)) {
            return null;
        }

        return await _context.Client.SearchPagedSubjectsAsync(new()
        {
            Keyword = searchText,
        }, new(_context.Settings.SearchCount, searchPage * _context.Settings.SearchCount), cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        _cts.Dispose();
    }
}

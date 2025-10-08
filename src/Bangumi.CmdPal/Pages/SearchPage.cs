using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
                _ = SearchAsync(_searchText, _cts.Token);
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
    private void SetItems(IListItem[] items)
    {
        if (_items == items) return;
        _items = items;
        RaiseItemsChanged(items.Length);
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

#pragma warning disable BgmExprApi // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。

    private async Task SearchAsync(string searchText, CancellationToken cancellationToken)
    {
        using (this.EnterLoadingScope()) {
            if (string.IsNullOrWhiteSpace(searchText)) {
                SetItems([]);
                return;
            }

            var subjects = await _context.Client.SearchPagedSubjectsAsync(new()
            {
                Keyword = searchText,
            }, _context.Settings.SearchCount, 0, cancellationToken).ConfigureAwait(false);

            var result = subjects.Datas
                .Select(x => new SubjectListItem(_context, x, cancellationToken))
                .ToArray();
            SetItems(result);
        }
    }

    public void Dispose()
    {
        _cts.Dispose();
    }
}

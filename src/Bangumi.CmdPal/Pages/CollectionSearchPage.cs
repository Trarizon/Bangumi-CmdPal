using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
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
using Trarizon.Bangumi.CmdPal.Utilities;

namespace Trarizon.Bangumi.CmdPal.Pages;
internal partial class CollectionSearchPage : DynamicListPage, IDisposable
{
    private const int AsyncPageCollectionRequestInterval = 100;

    public CollectionSearchPage(BangumiExtensionContext context)
    {
        _context = context;

        Title = "我的时光机";
        PlaceholderText = "键入以搜索时光机条目";
        ShowDetails = true;

        _searchListItems = [
            _searchListItem = new ListItem(new AnonymousCommand(() =>
            {
                _cts.Reset();
                _ = SearchAsync(_searchText,_cts.Token);
            }){
                Name = "搜索",
                Result= CmdPalFactory.KeepOpenResult(),
            }) {
                Title = "搜索我的时光机",
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
            _searchListItem.Title = "搜索我的时光机";
            SetItems(_searchListItems);
            return;
        }

        _searchText = newSearch;
        _searchListItem.Title = $"在我的时光机中搜索: {newSearch}";
        SetItems(_searchListItems);
    }

    private async Task SearchAsync(string searchText, CancellationToken cancellationToken)
    {
        using (this.EnterLoadingScope()) {

            var self = await _context.Client.GetSelfAsync(cancellationToken).ConfigureAwait(false);
            if (self is null)
                return;

            if (string.IsNullOrWhiteSpace(searchText)) {
                Debugging.Log("Handle empty search");
                var collections = await _context.Client.GetPagedUserSubjectCollectionsAsync(self.UserName,
                    collectionType: SubjectCollectionType.Doing,
                    pageLimit: _context.Settings.SearchCount,
                    pageOffset: 0,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                
                Debugging.Log("Handle empty search done");

                var result = collections.Datas
                    .Select(col => new UserSubjectCollectionListItem(_context, col, cancellationToken))
                    .ToArray();
                SetItems(result);
                return;
            }
            else {
                var result = await _context.Client.GetUserSubjectCollections(self.UserName,
                    collectionType: SubjectCollectionType.Doing,
                    options: new() { RequestInterval = TimeSpan.FromMilliseconds(AsyncPageCollectionRequestInterval) })
                    .Where(x =>
                    {
                        return x.Subject.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase)
                            || x.Subject.ChineseName.Contains(searchText, StringComparison.OrdinalIgnoreCase);
                    })
                    .Take(_context.Settings.SearchCount)
                    .Select(col => new UserSubjectCollectionListItem(_context, col, cancellationToken))
                    .ToArrayAsync(cancellationToken).ConfigureAwait(false);
                SetItems(result);
                return;
            }
        }
    }

    public void Dispose()
    {
        _cts.Dispose();
    }

    private sealed partial class CollectionTypeFilters : Filters
    {
        private readonly Filter[] _filters = [
            new Filter { Id = "wish", Name = "想看" },
            new Filter { Id = "doing", Name = "在看" },
            new Filter { Id = "onhold", Name = "搁置" },
            new Filter { Id = "collect", Name = "已看" },
            new Filter { Id = "dropped", Name = "抛弃" },
        ];

        public override IFilterItem[] GetFilters() => _filters;
    }
}

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
using Trarizon.Bangumi.CmdPal.Pages.Filters;
using Trarizon.Bangumi.CmdPal.Pages.ListItems;
using Trarizon.Bangumi.CmdPal.Utilities;
using ZLogger;

namespace Trarizon.Bangumi.CmdPal.Pages;

internal partial class CollectionSearchPage : DynamicListPage, IDisposable
{
    private const int AsyncPageCollectionRequestInterval = 100;

    private static readonly ICommandItem NoResultCommandItem = new CommandItem(new NoOpCommand()) { Title = "无搜索结果", Icon = new IconInfo("\uE946") };

    public CollectionSearchPage(BangumiExtensionContext context)
    {
        _context = context;
        _logger = _context.CreateLogger("collection search");

        Title = "我的时光机";
        PlaceholderText = "键入以搜索时光机条目";
        ShowDetails = true;

        _searchHintListItems = [
            _searchHintListItem = new ListItem(new AnonymousCommand(() =>
            {
                _cts.Reset();
                _ = SearchAsync(_searchText, SubjectCollectionType.Doing, _cts.Token);
            }){
                Name = "搜索",
                Result= CmdPalFactory.KeepOpenResult(),
            }) {
                Title = "搜索我的时光机",
                Icon = CmdPalFactory.Icon("\uE721"),
            }
        ];

        EmptyContent = NoResultCommandItem;
        _items = _searchHintListItems;
    }

    private readonly BangumiExtensionContext _context;
    private readonly ILogger _logger;
    private readonly ResettableCancellationTokenSource _cts = new();

    private readonly IListItem[] _searchHintListItems;
    private readonly ListItem _searchHintListItem;

    private string _searchText = "";

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
            _searchHintListItem.Title = "搜索我的时光机";
            SetItems(_searchHintListItems);
            return;
        }

        _searchText = newSearch;
        _searchHintListItem.Title = $"在我的时光机中搜索: {newSearch}";
        SetItems(_searchHintListItems);
    }

    private async Task SearchAsync(string searchText, SubjectCollectionType collectionType, CancellationToken cancellationToken)
    {
        using (this.EnterLoadingScope()) {

            var self = await _context.Client.GetAuthorizationAsync(cancellationToken).ConfigureAwait(false);
            if (self is null)
                return;

            if (string.IsNullOrWhiteSpace(searchText)) {
                _logger.ZLogTrace($"Search empty");

                var collections = await _context.Client.GetPagedUserSubjectCollectionsAsync(self.UserName,
                    collectionType: collectionType,
                    pagination: new(_context.Settings.SearchCount),
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                _logger.ZLogTrace($"Search empty done");

                var result = collections.Datas
                    .Select(col => new UserSubjectCollectionListItem(_context, col, cancellationToken))
                    .ToArray();
                SetItems(result);
                return;
            }
            else {
                var result = await _context.Client.GetUserSubjectCollections(self.UserName,
                    collectionType: collectionType)
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
}

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using System;
using System.Threading.Tasks;

namespace Trarizon.Bangumi.CmdPal.Toolkit;

public abstract partial class ArrayDynamicListPage : DynamicListPage
{
    private IListItem[] _single = new IListItem[1];

    public IListItem[] Items { get; set; } = [];

    public override IListItem[] GetItems() => Items;

    protected void SetItems(IListItem[] items, ICommandItem? emptyContent = null, bool hasMoreItems = false)
    {
        SetItemsInternal(items, false, hasMoreItems, emptyContent);
    }

    protected void SetItem(IListItem item, bool hasMoreItems = false)
    {
        _single[0] = item;
        SetItemsInternal(_single, true, hasMoreItems, null);
    }

    private void SetItemsInternal(IListItem[] items, bool forceNotify, bool hasMoreItems, ICommandItem? emptyContent)
    {
        if (Items != items || forceNotify) {
            Items = items;
            RaiseItemsChanged(Items.Length);
        }
        if (EmptyContent != emptyContent)
            EmptyContent = emptyContent;
        if (HasMoreItems != hasMoreItems)
            HasMoreItems = hasMoreItems;
    }
}

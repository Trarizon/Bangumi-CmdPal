using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Trarizon.Bangumi.CmdPal.Toolkit;

internal sealed partial class ArrayFilters(IFilterItem[] items) : Filters
{
    public override IFilterItem[] GetFilters() => items;
}

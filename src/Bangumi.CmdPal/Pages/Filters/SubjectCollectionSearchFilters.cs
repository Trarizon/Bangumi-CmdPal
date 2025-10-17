using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Trarizon.Bangumi.Api.Responses.Models;
using Trarizon.Bangumi.Api.Responses.Models.Collections;
using Trarizon.Bangumi.CmdPal.Helpers;

namespace Trarizon.Bangumi.CmdPal.Pages.Filters;

internal sealed partial class SubjectCollectionSearchFilters : CmdPalFilters
{
    // SubjectCollectionType
    private static readonly IFilterItem[] _filters = [
        new Filter { Id = "wish", Name = SubjectCollectionType.Wish.ToDisplayString(SubjectType.Anime) },
        new Filter { Id = "collect", Name = SubjectCollectionType.Collect.ToDisplayString(SubjectType.Anime) },
        new Filter { Id = "doing", Name = SubjectCollectionType.Doing.ToDisplayString(SubjectType.Anime) },
        new Filter { Id = "on_hold", Name = SubjectCollectionType.OnHold.ToDisplayString(SubjectType.Anime) },
        new Filter { Id = "dropped", Name = SubjectCollectionType.Dropped.ToDisplayString(SubjectType.Anime) },
    ];

    public override IFilterItem[] GetFilters() => _filters;

    public SubjectCollectionType CurrentCollectionType
    {
        get => CurrentFilterId switch
        {
            "wish" => SubjectCollectionType.Wish,
            "collect" => SubjectCollectionType.Collect,
            "doing" => SubjectCollectionType.Doing,
            "on_hold" => SubjectCollectionType.OnHold,
            "dropped" => SubjectCollectionType.Dropped,
            _ => SubjectCollectionType.Doing
        };
        set => CurrentFilterId = value switch
        {
            SubjectCollectionType.Wish => "wish",
            SubjectCollectionType.Collect => "collect",
            SubjectCollectionType.Doing => "doing",
            SubjectCollectionType.OnHold => "on_hold",
            SubjectCollectionType.Dropped => "dropped",
            _ => "doing"
        };
    }
}

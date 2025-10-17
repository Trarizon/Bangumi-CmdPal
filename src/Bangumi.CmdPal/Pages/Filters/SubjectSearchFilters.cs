using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Trarizon.Bangumi.Api.Responses.Models;
using Trarizon.Bangumi.CmdPal.Helpers;

namespace Trarizon.Bangumi.CmdPal.Pages.Filters;

internal partial class SubjectSearchFilters : CmdPalFilters
{
    private static readonly IFilterItem[] _filters = [
        new Filter { Id = "all", Name = "全部", Icon = new IconInfo("\uE8A9") },
        new Filter { Id = "book", Name = SubjectType.Book.ToDisplayString(), Icon = SubjectType.Book.GetIconInfo() },
        new Filter { Id = "anime", Name = SubjectType.Anime.ToDisplayString(), Icon = SubjectType.Anime.GetIconInfo() },
        new Filter { Id = "music", Name = SubjectType.Music.ToDisplayString(), Icon = SubjectType.Music.GetIconInfo() },
        new Filter { Id = "game", Name = SubjectType.Game.ToDisplayString(), Icon = SubjectType.Game.GetIconInfo() },
        new Filter { Id = "real", Name = SubjectType.Real.ToDisplayString(), Icon = SubjectType.Real.GetIconInfo() },
    ];

    public override IFilterItem[] GetFilters() => _filters;

    public SubjectType? CurrentSubjectType
    {
        get => CurrentFilterId switch
        {
            "all" => null,
            "book" => SubjectType.Book,
            "anime" => SubjectType.Anime,
            "music" => SubjectType.Music,
            "game" => SubjectType.Game,
            "real" => SubjectType.Real,
            _ => null,
        };
    }
}

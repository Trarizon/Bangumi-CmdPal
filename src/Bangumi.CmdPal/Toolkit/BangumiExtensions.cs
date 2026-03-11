using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Trarizon.Bangumi.Api.Responses.Models;
using Trarizon.Bangumi.Api.Responses.Models.Collections;

namespace Trarizon.Bangumi.CmdPal.Toolkit;

internal static class BangumiExtensions
{
    private const SubjectType SubjectType_All = 0;
    private static readonly Dictionary<SubjectType, ITag> _subjectTypeTagDict = [];

    extension(SubjectType subjectType)
    {
        public static SubjectType All => SubjectType_All;

        public string ToFilterId() => subjectType switch
        {
            SubjectType_All => "all",
            SubjectType.Book => "book",
            SubjectType.Anime => "anime",
            SubjectType.Music => "music",
            SubjectType.Game => "game",
            SubjectType.Real => "real",
            _ => "",
        };

        public static SubjectType FromFilterId(string? id) => id switch
        {
            "book" => SubjectType.Book,
            "anime" => SubjectType.Anime,
            "music" => SubjectType.Music,
            "game" => SubjectType.Game,
            "real" => SubjectType.Real,
            _ => SubjectType_All
        };

        public string ToDisplayString() => subjectType switch
        {
            SubjectType_All => "全部",
            SubjectType.Book => "书籍",
            SubjectType.Anime => "动画",
            SubjectType.Music => "音乐",
            SubjectType.Game => "游戏",
            SubjectType.Real => "三次元",
            _ => throw new SwitchExpressionException(subjectType),
        };

        public IconInfo ToIconInfo() => subjectType switch
        {
            SubjectType_All => IconInfo.FromCode("\uE71D"),
            SubjectType.Book => IconInfo.FromCode("\uE82D"),
            SubjectType.Anime => IconInfo.FromCode("\uE7F4"),
            SubjectType.Music => IconInfo.FromCode("\uE8D6"),
            SubjectType.Game => IconInfo.FromCode("\uE7FC"),
            SubjectType.Real => IconInfo.FromCode("\uE714"),
            _ => throw new SwitchExpressionException(subjectType),
        };

        public ITag ToTag()
        {
            ref var val = ref CollectionsMarshal.GetValueRefOrAddDefault(_subjectTypeTagDict, subjectType, out bool exists);
            if (!exists) {
                val = new Tag(subjectType.ToDisplayString()) { Icon = subjectType.ToIconInfo() };
            }
            return val!;
        }
    }

    public static string ToDisplayString(this SubjectCollectionType collectionType, SubjectType subjectType) => (collectionType, subjectType) switch
    {
        (SubjectCollectionType.Wish, SubjectType.Book or SubjectType.Anime or SubjectType.Real)
            => "想看",
        (SubjectCollectionType.Wish, SubjectType.Game)
            => "想玩",
        (SubjectCollectionType.Wish, SubjectType.Music)
            => "想听",
        (SubjectCollectionType.Collect, SubjectType.Book or SubjectType.Anime or SubjectType.Real)
            => "看过",
        (SubjectCollectionType.Collect, SubjectType.Game)
            => "玩过",
        (SubjectCollectionType.Collect, SubjectType.Music)
            => "听过",
        (SubjectCollectionType.Doing, SubjectType.Book or SubjectType.Anime or SubjectType.Real)
            => "在看",
        (SubjectCollectionType.Doing, SubjectType.Game)
            => "在玩",
        (SubjectCollectionType.Doing, SubjectType.Music)
            => "在听",
        (SubjectCollectionType.OnHold, _) => "搁置",
        (SubjectCollectionType.Dropped, _) => "抛弃",
        _ => throw new SwitchExpressionException((collectionType, subjectType)),
    };

}

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Trarizon.Bangumi.Api.Responses.Models;
using Trarizon.Bangumi.Api.Responses.Models.Collections;

namespace Trarizon.Bangumi.CommandPalette.Utilities;
internal static class BangumiExtension
{
    private static Dictionary<SubjectType, ITag>? _tagDict;

    public static ITag ToTag(this SubjectType subjectType)
    {
        _tagDict ??= [];
        ref var val = ref CollectionsMarshal.GetValueRefOrAddDefault(_tagDict, subjectType, out bool exists);
        if (!exists) {
            val = subjectType switch
            {
                SubjectType.Book => new Tag("书籍") { Icon = new IconInfo("\uE82D") },
                SubjectType.Anime => new Tag("动画") { Icon = new IconInfo("\uE7F4") },
                SubjectType.Music => new Tag("音乐") { Icon = new IconInfo("\uE8D6") },
                SubjectType.Game => new Tag("游戏") { Icon = new IconInfo("\uE7FC") },
                SubjectType.Real => new Tag("三次元") { Icon = new IconInfo("\uE77B") },
                _ => throw new SwitchExpressionException(subjectType),
            };
        }
        return val!;
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

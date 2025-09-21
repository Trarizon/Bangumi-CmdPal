using System;
using System.Collections.Generic;
using Trarizon.Bangumi.Api.Responses.Models;
using Trarizon.Bangumi.CommandPalette.Utilities;

namespace Trarizon.Bangumi.CommandPalette.Helpers.Searching;
public readonly struct SearchOptions(string input, Range keywordRange)
{
    public string InputString => input;
    public ReadOnlySpan<char> Keywords => input.AsSpan(keywordRange);
    public bool Me { get; init; }
    public int Page { get; init; }
    public required List<SubjectType> SubjectTypes { get; init; }

    public static SearchOptions Parse(string rawInput)
    {
        // 输入规则：
        // 以空格分割，':'开头为选项，其他为值
        // 选项可以出现在开头或结尾
        // 出现第一个值以后，后续直到第一个选项为止的部分为实际搜索关键字，后续的值会被忽略
        // :opt :a search keywords: example :trailing data :>>
        // ^ option                         ^option        ^option
        //         ^ search keywords                  ^ignored
        var input = rawInput.AsSpan();

        int kwstart = -1;
        int kwend = -1;
        bool me = false;
        int page = 0;
        List<SubjectType> subjectTypes = [];

        foreach (var range in input.Split(' ')) {
            var split = input[range];
            if (split.IsEmpty)
                continue;

            if (split[0] != ':') {
                if (kwstart < 0)
                    kwstart = range.Start.GetOffset(input.Length);
                continue;
            }
            if (kwend < 0 && kwstart >= 0) {
                kwend = range.Start.GetOffset(input.Length);
            }

            var option = split[1..];
            if (option.Equals("me", StringComparison.OrdinalIgnoreCase)) {
                me = true;
                continue;
            }
            if (IsPaging(option)) {
                page = option.Length;
                continue;
            }
            if (TryGetSubjectType(option, out var subjectType)) {
                subjectTypes.Add(subjectType);
                continue;
            }
        }


        var ofs = (kwstart, kwend) switch
        {
            ( < 0, _) => 0..0,
            (_, < 0) => Utils.OffsetOf(input, input[kwstart..].TrimEnd()),
            (_, _) => Utils.OffsetOf(input, input[kwstart..kwend].TrimEnd()),
        };

        return new SearchOptions(rawInput, ofs)
        {
            Me = me,
            Page = page,
            SubjectTypes = subjectTypes,
        };

        // >>>
        static bool IsPaging(ReadOnlySpan<char> option)
        {
            if (option.Length == 0)
                return false;
            foreach (var ch in option) {
                if (ch is not '>')
                    return false;
            }
            return true;
        }

        // "anime", "book", "game", "music", "real"
        static bool TryGetSubjectType(ReadOnlySpan<char> option, out SubjectType subjectType)
        {
            ReadOnlySpan<SubjectType> types = [SubjectType.Anime, SubjectType.Book, SubjectType.Game, SubjectType.Music, SubjectType.Real];
            ReadOnlySpan<string> optionstrs = ["anime", "book", "game", "music", "real"];
            for (int i = 0; i < optionstrs.Length; i++) {
                var str = optionstrs[i];
                if (option.Equals(str, StringComparison.OrdinalIgnoreCase)) {
                    subjectType = types[i];
                    return true;
                }
            }
            subjectType = default;
            return false;
        }
    }

    public IEnumerable<SearchOptionInfo> GetUnsetOptions()
    {
        if (!Me) {
            yield return new("me", "搜索用户在看列表");
        }
    }
}

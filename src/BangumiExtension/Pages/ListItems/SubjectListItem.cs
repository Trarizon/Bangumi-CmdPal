using Microsoft.CommandPalette.Extensions.Toolkit;
using System;
using Trarizon.Bangumi.Api.Models.SubjectModels;
using Trarizon.Bangumi.CommandPalette.Helpers;
using Trarizon.Bangumi.CommandPalette.Utilities;
using Trarizon.Library.Functional;

namespace Trarizon.Bangumi.CommandPalette.Pages.ListItems;
internal sealed partial class SubjectListItem : ListItem
{
    private const int SummaryTruncateLength = 100;

    public SubjectListItem(SearchResponsedSubject subject)
    {
        Command = BangumiHelpers.OpenSubjectUrlCommand(subject);

        (Title, Subtitle) = (subject.Name, subject.ChineseName) switch
        {
            (var name, "") => (name, ""),
            (var name, var cnName) when name == cnName => (name, ""),
            (var name, var cnName) => (cnName, name)
        };

        Tags = [subject.Type.ToTag()];

        Details = new Details
        {
            Title = subject.Name,
            HeroImage = new IconInfo(subject.Images.Grid),
            Body = subject.Summary.Length > SummaryTruncateLength
                ? $"{subject.Summary.AsSpan(..SummaryTruncateLength)}..."
                : subject.Summary,
            Metadata = [
                ..Optional.Of(subject.ChineseName)
                    .Where(cn => !string.IsNullOrEmpty(cn))
                    .Select(x => new DetailsElement {
                        Key = "中文名",
                        Data = new DetailsLink{ Text = x }
                    }),
                new DetailsElement {
                    Key = "类型",
                    Data = new DetailsTags {
                        Tags = [subject.Type.ToTag()]
                    }
                },
            ]
        };
    }
}

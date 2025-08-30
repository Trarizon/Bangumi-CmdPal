using Microsoft.CommandPalette.Extensions.Toolkit;
using System;
using Trarizon.Bangumi.Api.Models.SubjectModels;
using Trarizon.Bangumi.CommandPalette.Utilities;

namespace Trarizon.Bangumi.CommandPalette.Pages.ListItems;
internal sealed partial class SubjectListItem : ListItem
{
    private const int SummaryTruncateLength = 100;

    public SubjectListItem(SearchResponsedSubject subject)
    {
        Command = new OpenUrlCommand($"https://bgm.tv/subject/{subject.Id}") { Result = CommandResult.Dismiss() };

        (Title, Subtitle) = (subject.Name, subject.ChineseName) switch
        {
            (var name, "" or null) => (name, ""),
            (var name, var cnName) when name == cnName => (name, ""),
            (var name, var cnName) => (cnName, name)
        };

        Tags = [subject.Type.ToTag()];

        var details = BangumiHelpers.ToDetails(subject);
        details.Body = subject.Summary.Length > SummaryTruncateLength
            ? $"{subject.Summary.AsSpan(..SummaryTruncateLength)}..."
            : subject.Summary;
        Details = details;
    }
}

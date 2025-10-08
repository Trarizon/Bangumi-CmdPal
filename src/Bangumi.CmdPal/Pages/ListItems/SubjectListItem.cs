using Microsoft.CommandPalette.Extensions.Toolkit;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Trarizon.Bangumi.Api.Exceptions;
using Trarizon.Bangumi.Api.Responses.Models;
using Trarizon.Bangumi.Api.Responses.Models.Collections;
using Trarizon.Bangumi.Api.Routes;
using Trarizon.Bangumi.CmdPal.Core;
using Trarizon.Bangumi.CmdPal.Helpers;
using Trarizon.Bangumi.CmdPal.Utilities;
using Trarizon.Library.Functional;

namespace Trarizon.Bangumi.CmdPal.Pages.ListItems;
internal sealed partial class SubjectListItem : ListItem
{
    private const int SummaryTruncateLength = 100;

    private readonly BangumiExtensionContext _context;
    private readonly SearchResponsedSubject _subject;
    private readonly CancellationToken _cancellationToken;

    public SubjectListItem(BangumiExtensionContext context, SearchResponsedSubject subject, CancellationToken cancellationToken)
    {
        _context = context;
        _subject = subject;
        _cancellationToken = cancellationToken;

        Command = new OpenUrlCommand(BangumiHelpers.SubjectUrl(subject))
        {
            Name = "打开条目页",
            Result = CommandResult.Dismiss(),
        };

        (Title, Subtitle) = (subject.Name, subject.ChineseName) switch
        {
            (var name, "") => (name, ""),
            (var name, var cnName) when name == cnName => (name, ""),
            (var name, var cnName) => (cnName, name)
        };

        Tags = [subject.Type.ToTag()];

        SetDetails();
        _ = SetMoreCommandsAsync(_cancellationToken);
    }

    private void SetDetails()
    {
        var subject = _subject;

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
                        Data = new DetailsLink { Text = x }
                    }),
                new DetailsElement {
                    Key = "信息",
                    Data = new DetailsTags {
                        Tags = [
                            subject.Type.ToTag(),
                            Optional.Of(subject.Rating)
                                .Where(x => x.Total > 0)
                                .Match(
                                    x => new Tag($"{x.Score} ({x.Total}人评分)"),
                                    () => new Tag("无评分")),
                        ]
                    }
                },
            ]
        };
    }

    private async Task SetMoreCommandsAsync(CancellationToken cancellationToken)
    {
        var self = await _context.Client.GetSelfAsync(cancellationToken).ConfigureAwait(false);
        if (self is null)
            return;

        try {
            var collection = await _context.Client.GetUserSubjectCollectionAsync(self.UserName, _subject.Id, cancellationToken).ConfigureAwait(false);
            if (collection.Type is SubjectCollectionType.Wish) {
                MoreCommands = [
                    new CommandContextItem("标记为在看",
                        subtitle: "",
                        name: "标记为在看",
                        action: MarkAsDoingAsyncCallback,
                        result: CommandResult.KeepOpen())
                ];
            }
            else if (collection.Type is SubjectCollectionType.OnHold) {
                MoreCommands = [
                    new CommandContextItem("标记为想看",
                    subtitle: "",
                    name: "标记为想看",
                    action: MarkAsWishAsyncCallback,
                    result: CommandResult.KeepOpen())
                ];
            }
            else {
                MoreCommands = [];
            }
        }
        catch (BangumiApiException e) when (e.HttpStatusCode is HttpStatusCode.NotFound) {
            MoreCommands = [
                new CommandContextItem("标记为想看",
                    subtitle: "",
                    name: "标记为想看",
                    action: MarkAsWishAsyncCallback,
                    result: CommandResult.KeepOpen())
            ];
        }
    }

    private async void MarkAsWishAsyncCallback()
    {
        await _context.Client.AddOrUpdateUserSubjectCollectionAsync(_subject.Id, new()
        {
            Type = SubjectCollectionType.Wish
        }, _cancellationToken).ConfigureAwait(false);
    }

    private async void MarkAsDoingAsyncCallback()
    {
        await _context.Client.AddOrUpdateUserSubjectCollectionAsync(_subject.Id, new()
        {
            Type = SubjectCollectionType.Doing,
        }, _cancellationToken).ConfigureAwait(false);
    }
}

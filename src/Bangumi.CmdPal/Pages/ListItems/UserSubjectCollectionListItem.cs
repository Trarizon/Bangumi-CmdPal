using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Trarizon.Bangumi.Api.Requests.Payloads;
using Trarizon.Bangumi.Api.Responses.Models;
using Trarizon.Bangumi.Api.Responses.Models.Collections;
using Trarizon.Bangumi.Api.Routes;
using Trarizon.Bangumi.Api.Toolkit;
using Trarizon.Bangumi.CmdPal.Core;
using Trarizon.Bangumi.CmdPal.Toolkit;
using Trarizon.Library.Functional;

namespace Trarizon.Bangumi.CmdPal.Pages.ListItems;

internal sealed partial class UserSubjectCollectionListItem : ListItem
{
    private const int EpNameTruncateLength = 12;

    private readonly BangumiClient _client;

    private UserSubjectCollection _collection;
    private readonly CancellationToken _cancellationToken;

    public UserSubjectCollectionListItem(BangumiClient client, UserSubjectCollection subjectCollection, CancellationToken cancellationToken)
    {
        _client = client;
        _collection = subjectCollection;
        _cancellationToken = cancellationToken;
        var subject = _collection.Subject;

        Command = new OpenUrlCommand(BangumiHelpers.SubjectUrl(subject))
        {
            Name = "打开条目页",
            Result = CommandResult.Dismiss(),
        };

        (Title, Subtitle) = (subject.Name, subject.ChineseName) switch
        {
            (var name, "" or null) => (name, ""),
            (var name, var cnName) when name == cnName => (name, ""),
            (var name, var cnName) => (cnName, name)
        };

        Tags = [subject.Type.ToTag()];

        _ = SetDetailsAsync();
        _ = SetMoreCommandsAsync(_cancellationToken);
    }

    private Episode? _nextEp;

    private async Task SetDetailsAsync()
    {
        var subject = _collection.Subject;

        DetailsTags episodeDetailsData;

        Details details;
        Details = details = new Details
        {
            Title = subject.Name,
            HeroImage = new IconInfo(subject.Images.Grid),
            Body = subject.TruncatedSummary,
            Metadata = [
                ..Optional.Of(subject.ChineseName)
                    .Where(cn => !string.IsNullOrEmpty(cn))
                    .Select(x => new DetailsElement {
                        Key = "中文名",
                        Data = new DetailsLink{ Text = x }
                    }),
                new DetailsElement{
                    Key = "章节",
                    Data = episodeDetailsData = new DetailsTags {
                        Tags = [new Tag("Loading...")]
                    },
                },
                new DetailsElement {
                    Key = "信息",
                    Data = new DetailsTags {
                        Tags = [
                            subject.Type.ToTag(),
                            new Tag($"评分: {subject.Score}"),
                            .. Optional.Of(_collection.Rate)
                                .Where(x => x > 0)
                                .Select(x => new Tag($"我的给分: {x}")),
                        ]
                    }
                },
            ]
        };

        List<Tag> tags = [];
        Episode? firstWatching = null;
        var epcols = _client.GetUserSubjectEpisodeCollections(subject.Id);
        await foreach (var epcol in epcols.WithCancellation(_cancellationToken)) {
            var ep = epcol.Episode;
            firstWatching ??= ep;
            tags.Add(new Tag($"{ep.Sort}")
            {
                Background = epcol.Type is EpisodeCollectionType.Collect ? new OptionalColor(true, new Color(0, 0, 255, 255))
                    : ep.AirDate <= DateOnly.FromDateTime(DateTime.Now) ? new OptionalColor(true, new Color(0, 255, 255, 255))
                    : default,
                Foreground = epcol.Type is EpisodeCollectionType.Collect ? new OptionalColor(true, new Color(255, 255, 255, 255))
                    : default,
                ToolTip = ep.Name,
            });
        }
        episodeDetailsData.Tags = tags.AsEnumerable<ITag>().ToArray();
        details.Metadata = details.Metadata; // Force raise notification
    }

    private async Task SetMoreCommandsAsync(CancellationToken cancellationToken)
    {
        // 已看完
        if (_collection.Type is not SubjectCollectionType.Doing) {
            MoreCommands = [];
            return;
        }

        MoreCommands = [
            .. Optional.OfNotNull(await GetMarkNextEpisodeDoneCommandItemAsync(cancellationToken).ConfigureAwait(false)),
            GetMarkSubjectAsDoneCommandItem(),
            .. Optional.OfNotNull(await GetOpenEpisodeUrlCommandItemAsync(cancellationToken).ConfigureAwait(false)),
        ];
    }

    private async Task<CommandContextItem?> GetMarkNextEpisodeDoneCommandItemAsync(CancellationToken cancellationToken)
    {
        var epCount = _collection.Subject.EpisodeCount;
        if (epCount == 0) {
            // wiki中没有值，从数据库获取
            var fullsubject = await _client.GetSubjectAsync(_collection.SubjectId, _cancellationToken).ConfigureAwait(false);
            epCount = (int)fullsubject.TotalEpisodeCount;
        }
        if (_collection.EpisodeStatus < epCount) {
            var nextEp = await GetNextEpisodeAsync(_cancellationToken).ConfigureAwait(false);
            var epName = nextEp.Name.Length > EpNameTruncateLength ? $"{nextEp.Name.AsSpan(..EpNameTruncateLength)}..." : nextEp.Name;
            var text = $"看过 ep.{nextEp.Sort} {epName}";
            return new CommandContextItem(
                title: text,
                subtitle: "",
                name: text,
                action: MarkNextEpisodeDoneAsyncCallback,
                CommandResult.KeepOpen());
        }
        return null;
    }

    private async void MarkNextEpisodeDoneAsyncCallback()
    {
        var cmdItem = (CommandContextItem)MoreCommands[0];
        cmdItem.Title = "API请求中...";
        cmdItem.Command = new NoOpCommand() { Name = "API请求中..." };

        var ep = await GetNextEpisodeAsync(_cancellationToken).ConfigureAwait(false);

        if (ep is null)
            return;

        try {
            await _client.UpdateUserSubjectEpisodeCollectionsAsync(_collection.Subject.Id, new UpdateUserSubjectEpisodeCollectionsRequestBody
            {
                EpisodeIds = [ep.Id],
                Type = EpisodeCollectionType.Collect
            }).ConfigureAwait(false);

            // 重设collection
            var self = await _client.GetAuthorizationAsync(_cancellationToken).ConfigureAwait(false);
            Debug.Assert(self is not null);
            await ReplaceCollectionAsync(await _client.GetUserSubjectCollectionAsync(self.UserName, _collection.SubjectId, _cancellationToken).ConfigureAwait(false));
        }
        catch {
            // TODO: 弹个toast？
        }
    }

    private CommandContextItem GetMarkSubjectAsDoneCommandItem()
    {
        var subjectDoneCommandName = $"标记为{SubjectCollectionType.Collect.ToDisplayString(_collection.SubjectType)}";
        return new CommandContextItem(
            title: subjectDoneCommandName,
            subtitle: "",
            name: subjectDoneCommandName,
            action: MarkSubjectAsDoneAsyncCallback,
            CommandResult.KeepOpen());
    }

    private async void MarkSubjectAsDoneAsyncCallback()
    {
        try {
            await _client.UpdateUserSubjectCollectionAsync(_collection.SubjectId, new UpdateUserSubjectCollectionRequestBody
            {
                Type = SubjectCollectionType.Collect,
            }, _cancellationToken).ConfigureAwait(false);

            await SetMoreCommandsAsync(_cancellationToken).ConfigureAwait(false);
        }
        catch (Exception) {
            // TODO: 弹个toast？
        }
    }

    private async Task<CommandContextItem?> GetOpenEpisodeUrlCommandItemAsync(CancellationToken cancellationToken)
    {
        var prevEp = await _client.GetEpisodes(_collection.SubjectId).ElementAtOrDefaultAsync(_collection.EpisodeStatus - 1, _cancellationToken).ConfigureAwait(false);
        if (prevEp is not null) {
            return new CommandContextItem(new OpenUrlCommand(BangumiHelpers.EpisodeUrl(prevEp)))
            {
                Title = $"打开Ep.{prevEp.Sort}页面",
                Subtitle = "",
            };
        }
        return null;
    }

    private async Task ReplaceCollectionAsync(UserSubjectCollection collection)
    {
        Debug.Assert(_collection.SubjectId == collection.SubjectId);
        _collection = collection;
        _nextEp = null;
        await SetMoreCommandsAsync(_cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Episode> GetNextEpisodeAsync(CancellationToken cancellationToken)
    {
        if (_nextEp is not null)
            return _nextEp;

        _nextEp = await _client.GetEpisodes(_collection.SubjectId)
            .ElementAtAsync(_collection.EpisodeStatus, cancellationToken).ConfigureAwait(false);

        Debug.Assert(_nextEp is not null);
        return _nextEp;
    }
}

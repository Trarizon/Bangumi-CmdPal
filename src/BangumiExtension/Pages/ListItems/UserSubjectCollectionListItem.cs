using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Trarizon.Bangumi.Api.Models.EpisodeModels;
using Trarizon.Bangumi.Api.Models.UserModels;
using Trarizon.Bangumi.Api.Requests;
using Trarizon.Bangumi.Api.Routes;
using Trarizon.Bangumi.Api.Toolkit;
using Trarizon.Bangumi.CommandPalette.Helpers;
using Trarizon.Bangumi.CommandPalette.Utilities;
using Trarizon.Library.Functional;

namespace Trarizon.Bangumi.CommandPalette.Pages.ListItems;
internal sealed partial class UserSubjectCollectionListItem : ListItem
{
    private const int EpNameTruncateLength = 12;

    private UserSubjectCollection _collection;
    private readonly MainSearchPage _page;
    private readonly CancellationToken _cancellationToken;

    public UserSubjectCollectionListItem(MainSearchPage page, UserSubjectCollection subjectCollection, CancellationToken cancellationToken)
    {
        _collection = subjectCollection;
        _page = page;
        _cancellationToken = cancellationToken;
        var subject = _collection.Subject;

        Command = new OpenUrlCommand(BangumiUrls.Subject(subject.Id)) { Result = CommandResult.Dismiss() };

        (Title, Subtitle) = (subject.Name, subject.ChineseName) switch
        {
            (var name, "" or null) => (name, ""),
            (var name, var cnName) when name == cnName => (name, ""),
            (var name, var cnName) => (cnName, name)
        };

        Tags = [subject.Type.ToTag()];

        SetDetails();
        _ = SetMoreCommandsAsync();
    }

    private Episode? _nextEp;

    private void SetDetails()
    {
        var subject = _collection.Subject;

        Details = new Details
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
                new DetailsElement {
                    Key = "类型",
                    Data = new DetailsTags {
                        Tags = [subject.Type.ToTag()]
                    }
                },
            ]
        };
    }

    private async Task SetMoreCommandsAsync()
    {
        // 已看完
        if (_collection.Type is not SubjectCollectionType.Doing) {
            MoreCommands = [];
            return;
        }

        CommandContextItem? item = null;
        var epCount = _collection.Subject.EpisodeCount;
        if (epCount == 0) {
            // wiki中没有值，从数据库获取
            var fullsubject = await _page.Client.GetSubjectAsync(_collection.SubjectId, _cancellationToken).ConfigureAwait(false);
            epCount = (int)fullsubject.TotalEpisodeCount;
        }
        if (_collection.EpisodeStatus < epCount) {
            var nextEp = await GetNextEpisodeAsync(_cancellationToken).ConfigureAwait(false);
            var epName = nextEp.Name.Length > EpNameTruncateLength ? $"{nextEp.Name.AsSpan(..EpNameTruncateLength)}..." : nextEp.Name;
            var text = $"看过 ep.{nextEp.Sort} {epName}";
            item = new CommandContextItem(
                title: text,
                subtitle: "",
                name: text,
                action: DoneAsyncCallback,
                CommandResult.KeepOpen());
        }

        var subjectDoneCommandName = $"标记为{SubjectCollectionType.Collect.ToDisplayString(_collection.SubjectType)}";
        var subjectDoneCommand = new CommandContextItem(
            title: subjectDoneCommandName,
            subtitle: "",
            name: subjectDoneCommandName,
            action: MarkSubjectAsDoneAsyncCallback,
            CommandResult.KeepOpen());

        var prevEp = await _page.Client.GetEpisodes(_collection.SubjectId).ElementAtOrDefaultAsync(_collection.EpisodeStatus - 1, _cancellationToken).ConfigureAwait(false);
        IContextItem? openEpUrlCommand = null;
        if (prevEp is not null) {
            var openEpUrlCommandName = $"打开Ep.{prevEp.Ep}页面";
            openEpUrlCommand = new CommandContextItem(new OpenUrlCommand(BangumiUrls.Episode(prevEp.Id))
            {
                Name = openEpUrlCommandName,
                Result = CommandResult.Dismiss(),
            })
            {
                Title = openEpUrlCommandName,
                Subtitle = "",
            };
        }

        MoreCommands = [
            .. Optional.OfNotNull(item),
            subjectDoneCommand,
            .. Optional.OfNotNull(openEpUrlCommand),
        ];
    }

    private async Task ReplaceCollectionAsync(UserSubjectCollection collection)
    {
        Debug.Assert(_collection.SubjectId == collection.SubjectId);
        _collection = collection;
        _nextEp = null;
        await SetMoreCommandsAsync().ConfigureAwait(false);
    }
    
    private async ValueTask<Episode> GetNextEpisodeAsync(CancellationToken cancellationToken)
    {
        if (_nextEp is not null)
            return _nextEp;

        _nextEp = await _page.Client.GetEpisodes(_collection.SubjectId)
            .ElementAtAsync(_collection.EpisodeStatus, cancellationToken).ConfigureAwait(false);

        Debug.Assert(_nextEp is not null);
        return _nextEp;
    }

    private async void DoneAsyncCallback()
    {
        var cmdItem = (CommandContextItem)MoreCommands[0];
        cmdItem.Title = "API请求中...";
        cmdItem.Command = new NoOpCommand() { Name = "API请求中..." };

        var ep = await GetNextEpisodeAsync(_cancellationToken).ConfigureAwait(false);

        if (ep is null)
            return;

        try {
            await _page.Client.UpdateUserSubjectEpisodeCollectionsAsync(_collection.Subject.Id, new UpdateUserSubjectEpisodeCollectionsRequestBody
            {
                EpisodeIds = [ep.Id],
                Type = EpisodeCollectionType.Collect
            }).ConfigureAwait(false);

            // 重设collection
            var self = await _page.Client.GetSelfAsync(_cancellationToken).ConfigureAwait(false);
            Debug.Assert(self is not null);
            await ReplaceCollectionAsync(await _page.Client.GetUserSubjectCollectionAsync(self.UserName, _collection.SubjectId, _cancellationToken).ConfigureAwait(false));
        }
        catch {
            // TODO: 弹个toast？
        }
    }

    private async void MarkSubjectAsDoneAsyncCallback()
    {
        try {
            await _page.Client.UpdateUserSubjectCollectionAsync(_collection.SubjectId, new UpdateUserSubjectCollectionRequestBody
            {
                Type = SubjectCollectionType.Collect,
            }, _cancellationToken).ConfigureAwait(false);

            await SetMoreCommandsAsync().ConfigureAwait(false);
        }
        catch (Exception) {
            // TODO: 弹个toast？
        }
    }
}

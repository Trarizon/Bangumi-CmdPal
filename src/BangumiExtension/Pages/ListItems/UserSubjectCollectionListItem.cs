using Microsoft.CommandPalette.Extensions.Toolkit;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Trarizon.Bangumi.Api.Models.EpisodeModels;
using Trarizon.Bangumi.Api.Models.UserModels;
using Trarizon.Bangumi.Api.Requests;
using Trarizon.Bangumi.Api.Routes;
using Trarizon.Bangumi.Api.Toolkit;
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

        Command = new OpenUrlCommand($"https://bgm.tv/subject/{subject.Id}") { Result = CommandResult.Dismiss() };

        (Title, Subtitle) = (subject.Name, subject.ChineseName) switch
        {
            (var name, "" or null) => (name, ""),
            (var name, var cnName) when name == cnName => (name, ""),
            (var name, var cnName) => (cnName, name)
        };

        Tags = [subject.Type.ToTag()];

        var details = BangumiHelpers.ToDetails(subject);
        details.Body = subject.TruncatedSummary;
        Details = details;

        _ = SetMoreCommandsAsync();
    }

    private Optional<Episode> _nextEp;

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
            var ep = await GetNextEpisodeAsync(_cancellationToken).ConfigureAwait(false);
            var epName = ep.Name.Length > EpNameTruncateLength ? $"{ep.Name.AsSpan(..EpNameTruncateLength)}..." : ep.Name;
            var text = $"看过 ep.{ep.Sort} {epName}";
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

        Debugging.Log($"item is null: {item is null}");

        if (item is null)
            MoreCommands = [subjectDoneCommand];
        else
            MoreCommands = [item, subjectDoneCommand];
    }

    private async ValueTask<Episode> GetNextEpisodeAsync(CancellationToken cancellationToken)
    {
        if (_nextEp.HasValue)
            return _nextEp.Value;

        _nextEp = await _page.Client.GetEpisodes(_collection.SubjectId)
            .ElementAtAsync(_collection.EpisodeStatus, cancellationToken).ConfigureAwait(false);

        Debug.Assert(_nextEp.GetValueOrDefault() is not null);
        return _nextEp.Value;
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
            _collection = await _page.Client.GetUserSubjectCollectionAsync(self.UserName, _collection.SubjectId, _cancellationToken).ConfigureAwait(false);
            _nextEp = Optional.None;

            await SetMoreCommandsAsync().ConfigureAwait(false);
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

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Trarizon.Bangumi.Api.Exceptions;
using Trarizon.Bangumi.Api.Requests.Payloads;
using Trarizon.Bangumi.Api.Responses.Models;
using Trarizon.Bangumi.Api.Responses.Models.Abstractions;
using Trarizon.Bangumi.Api.Responses.Models.Collections;
using Trarizon.Bangumi.Api.Routes;
using Trarizon.Bangumi.Api.Toolkit;
using Trarizon.Bangumi.CmdPal.Core;
using Trarizon.Bangumi.CmdPal.Toolkit;
using Trarizon.Library.Functional;
using ZLogger;

namespace Trarizon.Bangumi.CmdPal.Pages.ListItems;

internal sealed partial class SubjectListItem : ListItem
{
    private const int SummaryTruncateLength = 100;

    private readonly BangumiClient _client;
    private readonly ILogger _logger;

    private readonly ISubject _subject;
    private UserSubjectCollection? _subjectCollection;
    private Subject? _fullSubject;
    private readonly CancellationToken _cancellationToken;
    private bool _requesting;

    private Details _details;
    private IDetailsElement[] _metadatas;

    private bool _detailsAsyncInit;
    public override IDetails? Details
    {
        get {
            if (!Interlocked.CompareExchange(ref _detailsAsyncInit, true, false)) {
                InitAuthedDetailsInfoAsync().ContinueWith(t => OnPropertyChanged(nameof(Details)));
            }
            return base.Details;
        }
        set => base.Details = value;
    }

    private bool _moreCommandsAsyncInit;
    public override IContextItem[] MoreCommands
    {
        get {
            if (!Interlocked.CompareExchange(ref _moreCommandsAsyncInit, true, false)) {
                InitAuthedMoreCommandsInfoAsync().ContinueWith(t => OnPropertyChanged(nameof(MoreCommands)));
            }
            return base.MoreCommands;
        }
        set => base.MoreCommands = value;
    }



    private SubjectListItem(ISubject subject, BangumiClient client, ILogger logger, CancellationToken cancellationToken)
    {
        _client = client;
        _logger = logger;

        _subject = subject;
        _cancellationToken = cancellationToken;

        Command = new OpenUrlCommand(BangumiHelpers.SubjectUrl(subject))
        {
            Name = "打开条目页",
            Result = CommandResult.Dismiss(),
        };
        Icon = subject.Type.ToIconInfo();
        (Title, Subtitle) = (subject.Name, subject.ChineseName) switch
        {
            (var name, "") => (name, ""),
            (var name, var cnName) when name == cnName => (name, ""),
            (var name, var cnName) => (cnName, name)
        };

        Tags = [subject.Type.ToTag()];

        _details = default!;
        _metadatas = default!;
    }

    public SubjectListItem(SearchResponsedSubject subject, BangumiClient client, ILogger logger, CancellationToken cancellationToken)
        : this((ISubject)subject, client, logger, cancellationToken)
    {
        var nameMd = Optional.Of(subject.ChineseName)
            .Where(cn => !string.IsNullOrEmpty(cn))
            .Select(x => new DetailsElement
            {
                Key = "中文名",
                Data = new DetailsLink { Text = x }
            });
        var infoMd = new DetailsElement
        {
            Key = "信息",
            Data = new DetailsTags
            {
                Tags = [
                    subject.Type.ToTag(),
                    Optional.Of(subject.Rating)
                        .Where(x => x.Total > 0)
                        .Match(
                            x => new Tag($"{x.Score} ({x.Total}人评分)"),
                            () => new Tag("无评分")),
                    ..Optional.OfPredicate(subject.EpisodeCount, x => x > 0)
                        .Select(x=> new Tag($"共{x}章")),
                ]
            }
        };

        _details = new Details
        {
            Title = subject.Name,
            HeroImage = new IconInfo(subject.Images.Grid),
            Body = subject.Summary.Length > SummaryTruncateLength
                ? $"{subject.Summary.AsSpan(..SummaryTruncateLength)}..."
                : subject.Summary,
            Metadata = _metadatas = [.. nameMd, infoMd],
        };
        Details = _details;

        _logger.ZLogInformation($"Subject item '{_subject.Name}' inited.");
    }

    public SubjectListItem(UserSubjectCollection subjectCollection, BangumiClient client, ILogger logger, CancellationToken cancellationToken)
        : this((ISubject)subjectCollection.Subject, client, logger, cancellationToken)
    {
        _subjectCollection = subjectCollection;
        var subject = subjectCollection.Subject;

        var nameMd = Optional.Of(subject.ChineseName)
          .Where(cn => !string.IsNullOrEmpty(cn))
          .Select(x => new DetailsElement
          {
              Key = "中文名",
              Data = new DetailsLink { Text = x }
          });
        var infoMd = new DetailsElement
        {
            Key = "信息",
            Data = new DetailsTags
            {
                Tags = [
                    subject.Type.ToTag(),
                    new Tag($"评分：{subject.Score}"),
                ]
            }
        };

        _details = new Details
        {
            Title = subject.Name,
            HeroImage = new IconInfo(subject.Images.Grid),
            Body = subject.TruncatedSummary,
            Metadata = _metadatas = [.. nameMd, infoMd],
        };
        Details = _details;

        _logger.ZLogInformation($"Subject item '{_subject.Name}' inited.");
    }

    private Task<UserSubjectCollection>? _colTask;
    private async ValueTask<Optional<UserSubjectCollection>> GetUserSubjectCollectionAsync(string userName)
    {
        if (_subjectCollection is not null)
            return _subjectCollection;

        try {
            var res = await (_colTask ??= _client.Client.GetUserSubjectCollectionAsync(userName, _subject.Id, _cancellationToken)).ConfigureAwait(false);
            _subjectCollection = res;
            return res;
        }
        catch (BangumiApiException e) when (e.HttpStatusCode is HttpStatusCode.NotFound) {
            return default;
        }
    }

    private async Task InitAuthedDetailsInfoAsync()
    {
        if (_client.AuthorizedUser.OfNotNull().TryGetValue(out var user)) {
            try {
                Optional<DetailsElement> episodeMd = default;
                if (_subject.Type == SubjectType.Anime) {
                    episodeMd = new DetailsElement
                    {
                        Key = "章节",
                        Data = new DetailsTags
                        {
                            Tags = await _client.GetUserSubjectEpisodeCollections(_subject.Id)
                                .Select(x => new Tag($"{x.Episode.Sort}")
                                {
                                    Background = x.Type is EpisodeCollectionType.Collect ? new OptionalColor(true, new Color(72, 151, 255, 255))
                                        : x.Episode.AirDate <= DateOnly.FromDateTime(DateTime.Now) ? new OptionalColor(true, new Color(168, 194, 225, 255))
                                        : default,
                                    Foreground = x.Type is EpisodeCollectionType.Collect ? new OptionalColor(true, new Color(255, 255, 255, 255))
                                        : x.Episode.AirDate <= DateOnly.FromDateTime(DateTime.Now) ? new OptionalColor(true, new Color(0, 102, 206, 255))
                                        : default,
                                    ToolTip = x.Episode.Name
                                })
                                .ToArrayAsync(_cancellationToken).ConfigureAwait(false),
                        }
                    };
                }

                var colres = await GetUserSubjectCollectionAsync(user.UserName).ConfigureAwait(false);
                if (colres.TryGetValue(out var col)) {
                    _subjectCollection = col;
                    var userMd = new DetailsElement
                    {
                        Key = "用户数据",
                        Data = new DetailsTags
                        {
                            Tags = [
                                new Tag(col.Type.ToDisplayString(col.SubjectType)),
                                .. Optional.Of(col.Rate).Where(x => x > 0)
                                    .Select(x => new Tag($"我的给分: {x}")),
                            ]
                        }
                    };
                    _details.Metadata = [.. _metadatas, .. episodeMd, userMd];
                }
            }
            catch (Exception ex) {
                _logger.ZLogError($"Subject item '{_subject.Name}' authed init failed. {ex.Message} {ex.StackTrace}");
                throw;
            }
            _logger.ZLogInformation($"Subject item '{_subject.Name}' authed inited.");
        }
    }


    private static readonly CommandContextItem[] MoreCommandsPlaceHolder = [new CommandContextItem(new NoOpCommand() { Name = "Loading commands..." }) { Title = "Loading commands..." }];
    private async Task InitAuthedMoreCommandsInfoAsync()
    {
        if (_client.AuthorizedUser.OfNotNull().TryGetValue(out var user)) {
            MoreCommands = MoreCommandsPlaceHolder;
            try {
                var colres = await GetUserSubjectCollectionAsync(user.UserName).ConfigureAwait(false);

                if (colres.TryGetValue(out var col)) {
                    MoreCommands = col.Type switch
                    {
                        SubjectCollectionType.Wish => [
                            new CommandContextItem(new RequestCommand(this, MarkAsDoingAsyncCallback)
                            {
                                Name = "标记为" + SubjectCollectionType.Doing.ToDisplayString(_subject.Type),
                            })
                        ],
                        SubjectCollectionType.Doing => [
                            ..Optional.OfNotNull(await GetMarkNextEpisodeDoneItemAsync(_cancellationToken).ConfigureAwait(false)),
                            new CommandContextItem(new RequestCommand(this, MarkAsDoneAsyncCallback)
                            {
                                Name = "标记为" + SubjectCollectionType.Collect.ToDisplayString(_subject.Type),
                            }),
                            ..Optional.OfNotNull(await GetOpenEpisodeUrlCommandItemAsync(_cancellationToken).ConfigureAwait(false)),
                        ],
                        SubjectCollectionType.OnHold => [],
                        SubjectCollectionType.Collect => [],
                        SubjectCollectionType.Dropped => [],
                        _ => [],
                    };
                }
                else {
                    MoreCommands = [
                        new CommandContextItem(new RequestCommand(this, MarkAsDoingAsyncCallback)
                        {
                            Name = $"标记为{SubjectCollectionType.Doing.ToDisplayString(_subject.Type)}",
                        }),
                        new CommandContextItem(new RequestCommand(this, MarkAsWishAsyncCallback)
                        {
                            Name = $"标记为{SubjectCollectionType.Wish.ToDisplayString(_subject.Type)}",
                        }),
                    ];
                }
            }
            catch (Exception ex) {
                MoreCommands = [];
                _logger.ZLogError($"Subject item '{_subject.Name}' async inited failed. {ex.Message} {ex.StackTrace}");
                throw;
            }
            _logger.ZLogInformation($"Subject item '{_subject.Name}' async inited.");
        }

    }

    private async Task MarkAsWishAsyncCallback()
    {
        await _client.AddOrUpdateUserSubjectCollectionAsync(_subject.Id, new()
        {
            Type = SubjectCollectionType.Wish
        }, _cancellationToken).ConfigureAwait(false);
    }

    private async Task MarkAsDoingAsyncCallback()
    {
        await _client.AddOrUpdateUserSubjectCollectionAsync(_subject.Id, new()
        {
            Type = SubjectCollectionType.Doing,
        }, _cancellationToken).ConfigureAwait(false);
    }

    private async Task MarkAsDoneAsyncCallback()
    {
        await _client.UpdateUserSubjectCollectionAsync(_subject.Id, new Api.Requests.Payloads.UpdateUserSubjectCollectionRequestBody
        {
            Type = SubjectCollectionType.Collect
        }, _cancellationToken).ConfigureAwait(false);

        MoreCommands = [];
    }

    private const int EpNameTruncateLength = 12;

    private async Task<CommandContextItem?> GetMarkNextEpisodeDoneItemAsync(CancellationToken cancellationToken)
    {
        if (_subjectCollection is null)
            return null;

        var epCount = _subjectCollection.Subject.EpisodeCount;
        if (epCount == 0) {
            // wiki中没有值，从数据库获取
            _fullSubject ??= await _client.GetSubjectAsync(_subjectCollection.SubjectId, cancellationToken).ConfigureAwait(false);
            epCount = (int)_fullSubject.TotalEpisodeCount;
        }
        if (_subjectCollection.EpisodeStatus < epCount) {
            var nextEp = await GetNextEpisodeAsync(_subjectCollection.EpisodeStatus, cancellationToken).ConfigureAwait(false);
            if (nextEp is null)
                return null;
            var epName = nextEp.Name.Length > EpNameTruncateLength ? $"{nextEp.Name.AsSpan(..EpNameTruncateLength)}..." : nextEp.Name;
            var text = $"看过 ep.{nextEp.Sort} {epName}";
            return new CommandContextItem(new RequestCommand(this, MarkNextEpisodeDoneAsyncCallback)
            {
                Name = text,
            });
        }
        return null;
    }

    private async Task MarkNextEpisodeDoneAsyncCallback()
    {
        if (_subjectCollection is null)
            return;

        var ep = await GetNextEpisodeAsync(_subjectCollection.EpisodeStatus, _cancellationToken).ConfigureAwait(false);
        if (ep is null)
            return;

        await _client.UpdateUserSubjectEpisodeCollectionsAsync(_subjectCollection.Subject.Id, new UpdateUserSubjectEpisodeCollectionsRequestBody
        {
            EpisodeIds = [ep.Id],
            Type = EpisodeCollectionType.Collect
        }).ConfigureAwait(false);

        // 重设collection
        await InitAuthedMoreCommandsInfoAsync();
    }

    private async Task<CommandContextItem?> GetOpenEpisodeUrlCommandItemAsync(CancellationToken cancellationToken)
    {
        if (_subjectCollection is null)
            return null;
        var prevEp = await GetNextEpisodeAsync(int.Max(0, _subjectCollection.EpisodeStatus - 1), cancellationToken).ConfigureAwait(false);
        if (prevEp is null)
            return null;
        return new CommandContextItem(new OpenUrlCommand(BangumiHelpers.EpisodeUrl(prevEp))
        {
            Name = $"打开Ep.{prevEp.Sort}页面",
        });
    }

    private async ValueTask<Episode?> GetNextEpisodeAsync(int idx, CancellationToken cancellationToken)
    {
        var data = await _client.GetPagedEpisodesAsync(_subject.Id, pagination: new(1, idx), cancellationToken: cancellationToken).ConfigureAwait(false);
        return data.Datas.ElementAtOrDefault(0);
    }

    private sealed partial class RequestCommand(SubjectListItem item, Func<Task> func) : InvokableCommand
    {
        public override ICommandResult Invoke()
        {
            if (Interlocked.CompareExchange(ref item._requesting, true, false)) {
                return CommandResult.ShowToast("正在请求中，请稍后重试");
            }

            Task.Run(async () =>
            {
                try {
                    await func().ConfigureAwait(false);
                }
                finally {
                    Interlocked.Exchange(ref item._requesting, false);
                }
            });
            return CommandResult.KeepOpen();
        }
    }
}

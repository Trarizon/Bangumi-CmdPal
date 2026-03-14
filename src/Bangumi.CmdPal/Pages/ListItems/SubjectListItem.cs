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

        if (client.AuthorizedUser.TryGetValue(out var user)) {
            _ = FetchSubjectCollectionAsync(cancellationToken);
            MoreCommands = MoreCommandsPlaceHolder;
        }
    }

    public SubjectListItem(UserSubjectCollection subjectCollection, BangumiClient client, ILogger logger, CancellationToken cancellationToken)
        : this((ISubject)subjectCollection.Subject, client, logger, cancellationToken)
    {
        SubjectCollection = subjectCollection;
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

        _ = FetchSubjectCollectionAsync(cancellationToken);
        MoreCommands = MoreCommandsPlaceHolder;
    }

    public UserSubjectCollection? SubjectCollection
    {
        get;
        set {
            if (field != value) {
                field = value;
                OnSubjectCollectionChanged(field);
            }
        }
    }

    private DetailsElement? _episodesDetailsElement;

    private Optional<UserSubjectCollection?> _subjectCollection;
    private Task<UserSubjectCollection>? _subjectCollectionTask;
    private DetailsElement? _subjectCollectionDetailsElement;
    private CancellationTokenSource? _subjectCollectionCancellationTokenSource;

    private void RefreshDetails()
    {
        _details.Metadata = [
            .. _metadatas,
            .. Optional.OfNotNull(_episodesDetailsElement),
            .. Optional.OfNotNull(_subjectCollectionDetailsElement)
        ];
        OnPropertyChanged(nameof(Details));
    }

    private async Task FetchSubjectCollectionAsync(CancellationToken cancellationToken)
    {
        if (!_client.AuthorizedUser.OfNotNull().TryGetValue(out var user)) {
            SubjectCollection = null;
            return;
        }

        var result = await Result.TryTask(_subjectCollectionTask ??= _client.Client.GetUserSubjectCollectionAsync(user.UserName, _subject.Id, cancellationToken)).ConfigureAwait(false);
        _subjectCollectionTask = null;
        SubjectCollection = result.GetValueOrDefault();
    }

    private async Task InitAuthedDetailsInfoAsync()
    {
        if (_client.AuthorizedUser.OfNotNull().TryGetValue(out var user)) {
            try {
                if (_subject.Type == SubjectType.Anime) {
                    _episodesDetailsElement = new DetailsElement
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
                                    ToolTip = x.Episode.AirDate is null ? x.Episode.Name : $"{x.Episode.AirDate.Value:yy-MM-dd}\n{x.Episode.Name}"
                                })
                                .ToArrayAsync(_cancellationToken).ConfigureAwait(false),
                        }
                    };
                }
                else {
                    _episodesDetailsElement = null;
                }
                RefreshDetails();
            }
            catch (Exception ex) {
                _logger.ZLogError($"Subject item '{_subject.Name}' authed init failed. {ex.Message} {ex.StackTrace}");
                throw;
            }
            _logger.ZLogInformation($"Subject item '{_subject.Name}' authed inited.");
        }
    }


    private static readonly CommandContextItem[] MoreCommandsPlaceHolder = [new CommandContextItem(new NoOpCommand() { Name = "Loading commands..." }) { Title = "Loading commands..." }];

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
        if (SubjectCollection is null)
            return null;

        var epCount = SubjectCollection.Subject.EpisodeCount;
        if (epCount == 0) {
            // wiki中没有值，从数据库获取
            _fullSubject ??= await _client.GetSubjectAsync(SubjectCollection.SubjectId, cancellationToken).ConfigureAwait(false);
            epCount = (int)_fullSubject.TotalEpisodeCount;
        }
        if (SubjectCollection.EpisodeStatus < epCount) {
            var nextEp = await GetEpisodeAsync(SubjectCollection.EpisodeStatus, cancellationToken).ConfigureAwait(false);
            if (nextEp is null)
                return null;
            var epName = nextEp.Name.Length > EpNameTruncateLength ? $"{nextEp.Name.AsSpan(..EpNameTruncateLength)}..." : nextEp.Name;
            var text = $"看过 ep.{nextEp.Sort} {epName}";
            return new CommandContextItem(new RequestCommand(this, MarkNextEpisodeDoneAsyncCallback, $"正在标记 ep.{nextEp.Sort} {epName} 为看过")
            {
                Name = text,
            });
        }
        return null;
    }

    private async Task MarkNextEpisodeDoneAsyncCallback()
    {
        if (SubjectCollection is null)
            return;

        var ep = await GetEpisodeAsync(SubjectCollection.EpisodeStatus, _cancellationToken).ConfigureAwait(false);
        if (ep is null)
            return;

        await _client.UpdateUserSubjectEpisodeCollectionsAsync(SubjectCollection.Subject.Id, new UpdateUserSubjectEpisodeCollectionsRequestBody
        {
            EpisodeIds = [ep.Id],
            Type = EpisodeCollectionType.Collect
        }).ConfigureAwait(false);


        // 重设collection
        await FetchSubjectCollectionAsync(_cancellationToken).ConfigureAwait(false);
    }

    private async Task<CommandContextItem?> GetOpenEpisodeUrlCommandItemAsync(CancellationToken cancellationToken)
    {
        if (SubjectCollection is null)
            return null;
        var prevEp = await GetEpisodeAsync(int.Max(0, SubjectCollection.EpisodeStatus - 1), cancellationToken).ConfigureAwait(false);
        if (prevEp is null)
            return null;
        return new CommandContextItem(new OpenUrlCommand(BangumiHelpers.EpisodeUrl(prevEp))
        {
            Name = $"打开Ep.{prevEp.Sort}页面",
        });
    }

    private async ValueTask<Episode?> GetEpisodeAsync(int idx, CancellationToken cancellationToken)
    {
        var data = await _client.GetPagedEpisodesAsync(_subject.Id, pagination: new(1, idx), cancellationToken: cancellationToken).ConfigureAwait(false);
        return data.Datas.ElementAtOrDefault(0);
    }

    private async void OnSubjectCollectionChanged(UserSubjectCollection? collection)
    {
        if (collection is null)
            return;

        _subjectCollectionDetailsElement = new DetailsElement
        {
            Key = "用户数据",
            Data = new DetailsTags
            {
                Tags = [
                    new Tag(collection.Type.ToDisplayString(collection.SubjectType)),
                    .. Optional.Of(collection.Rate).Where(x => x > 0)
                        .Select(x => new Tag($"我的给分: {x}")),
                ]
            }
        };
        RefreshDetails();

        if (collection is null) {
            MoreCommands = [
                new CommandContextItem(new RequestCommand(this, MarkAsDoingAsyncCallback, $"正在标记为{SubjectCollectionType.Doing.ToDisplayString(_subject.Type)}")
                {
                    Name = $"标记为{SubjectCollectionType.Doing.ToDisplayString(_subject.Type)}",
                }),
                new CommandContextItem(new RequestCommand(this, MarkAsWishAsyncCallback, $"正在标记为{SubjectCollectionType.Wish.ToDisplayString(_subject.Type)}")
                {
                    Name = $"标记为{SubjectCollectionType.Wish.ToDisplayString(_subject.Type)}",
                }),
            ];
        }
        else {
            MoreCommands = collection.Type switch
            {
                SubjectCollectionType.Wish => [
                    new CommandContextItem(new RequestCommand(this, MarkAsDoingAsyncCallback, "正在标记为" + SubjectCollectionType.Doing.ToDisplayString(_subject.Type) )
                    {
                        Name = "标记为" + SubjectCollectionType.Doing.ToDisplayString(_subject.Type),
                    })
                ],
                SubjectCollectionType.Doing => [
                    ..Optional.OfNotNull(await GetMarkNextEpisodeDoneItemAsync(_cancellationToken).ConfigureAwait(false)),
                    new CommandContextItem(new RequestCommand(this, MarkAsDoneAsyncCallback, "正在标记为" + SubjectCollectionType.Collect.ToDisplayString(_subject.Type))
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
    }

    private sealed partial class RequestCommand(SubjectListItem item, Func<Task> func, string? toastMessage = null) : InvokableCommand
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

            if (toastMessage is null) {
                return CommandResult.KeepOpen();
            }
            else {
                return CommandResult.ShowToast(new ToastArgs
                {
                    Message = toastMessage,
                    Result = CommandResult.KeepOpen()
                });
            }
        }
    }
}

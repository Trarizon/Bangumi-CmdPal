using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Trarizon.Bangumi.Api;
using Trarizon.Bangumi.Api.Exceptions;
using Trarizon.Bangumi.Api.Responses.Models.Users;
using Trarizon.Bangumi.Api.Routes;
using Trarizon.Library.Functional;
using ZLogger;

namespace Trarizon.Bangumi.CmdPal.Core;

internal sealed partial class BangumiClient : IBangumiClient, IDisposable
{
    private const string UserAgent = "Trarizon/Trarizon.Bangumi.CommandPalette";

    private BangumiHttpClient _client;
    private readonly ILogger _logger;
    private Optional<UserSelf?> _self;

    public BangumiHttpClient Client => _client;

    public BangumiClient(SettingsProvider settings, ILogger<BangumiClient> logger)
    {
        _logger = logger;
        _ = Reauthorize(settings.AccessToken);
        settings.PropertyChanged += async (s, e) =>
        {
            if (e.PropertyName is nameof(SettingsProvider.AccessToken)) {
                _ = Reauthorize(settings.AccessToken);
            }
        };
    }

    public event Action? AuthorizationStatusChanging;
    public event Action<Optional<UserSelf?>>? AuthorizationStatusChanged;

    public Optional<UserSelf?> AuthorizedUser => _self;

    [MemberNotNull(nameof(_self), nameof(_client))]
    private async Task Reauthorize(string accessToken)
    {
        _self = default;
        AuthorizationStatusChanging?.Invoke();
        _client?.Dispose();
        _client = new BangumiHttpClient(UserAgent, accessToken);

        try {
            _self = await _client.GetSelfAsync().ConfigureAwait(false);
        }
        catch (BangumiApiException ex) when (ex.HttpStatusCode == HttpStatusCode.Unauthorized) {
            _self = null;
        }
        catch (Exception ex) {
            _self = default;
            _logger.ZLogError($"Error on updating access token: {ex.Message} {ex.StackTrace}");
        }
        if (_self.TryGetValue(out var self)) {
            if (self is not null)
                _logger.ZLogInformation($"Authorized @{self.NickName}.");
            else
                _logger.ZLogWarning($"Access token not authorized.");
        }

        AuthorizationStatusChanged?.Invoke(_self);
    }

    public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        return _client.SendAsync(request, cancellationToken);
    }

    public async ValueTask<UserSelf?> GetAuthorizationAsync(CancellationToken cancellationToken = default)
    {
        if (_self.TryGetValue(out var value))
            return value;

        try {
            _self = await _client.GetSelfAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (BangumiApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.Unauthorized) {
            _self = null;
        }
        return _self.Value;
    }

    public void Dispose() => _client.Dispose();
}

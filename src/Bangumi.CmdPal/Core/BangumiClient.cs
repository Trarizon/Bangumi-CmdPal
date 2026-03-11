using Microsoft.Extensions.Caching.Memory;
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
using Trarizon.Bangumi.CmdPal.Core.Http;
using Trarizon.Library.Functional;
using ZLogger;

namespace Trarizon.Bangumi.CmdPal.Core;

internal sealed partial class BangumiClient : IBangumiClient, IDisposable
{
    private const string UserAgent = "Trarizon/Trarizon.Bangumi.CommandPalette";

    private BangumiHttpClient _client;
    private readonly IMemoryCache _cache;
    private readonly ILogger _logger;
    private Optional<UserSelf?> _self;

    public BangumiHttpClient Client => _client;

    public BangumiClient(IMemoryCache cache, SettingsProvider settings, ILogger<BangumiClient> logger)
    {
        _cache = cache;
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

    public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        _logger.ZLogInformation($"Http Request {request.Method}:{request.RequestUri}");
        if (request.Method != HttpMethod.Get || (request.RequestUri?.ToString().Contains("/users/") ?? false)) {
            var dirResp = await _client.SendAsync(request, cancellationToken);
            _logger.ZLogInformation($"Http Response [{dirResp.StatusCode}] {request.Method}:{request.RequestUri}");
            return dirResp;
        }

        var key = $"bangumi-api-request_{request.Method}:{request.RequestUri}";
        if (_cache.TryGetValue(key, out var respCache)) {
            var cachedResp = ((HttpResponseCache)respCache!).ToResponseMessage();
            _logger.ZLogInformation($"Http Response [Cached] [{cachedResp.StatusCode}] {request.Method}:{request.RequestUri}");
            return cachedResp;
        }

        var resp = await _client.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (resp.IsSuccessStatusCode) {
            _cache.Set(key, await HttpResponseCache.CreateAsync(resp).ConfigureAwait(false), new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(4),
                SlidingExpiration = TimeSpan.FromHours(1),
            });
        }

        _logger.ZLogInformation($"Http Response [{resp.StatusCode}] {request.Method}:{request.RequestUri}");
        return resp;
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

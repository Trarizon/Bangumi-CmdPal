using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Trarizon.Bangumi.Api;
using Trarizon.Bangumi.Api.Exceptions;
using Trarizon.Bangumi.Api.Responses.Models.Users;
using Trarizon.Bangumi.Api.Routes;
using Trarizon.Library.Functional;

namespace Trarizon.Bangumi.CmdPal.Utilities;

internal sealed partial class AuthorizableBangumiClient : IBangumiClient, IDisposable
{
    private const string UserAgent = "Trarizon/Trarizon.Bangumi.CommandPalette";

    private readonly BangumiHttpClient _client;
    private Optional<UserSelf?> _self;

    public AuthorizableBangumiClient(string? accessToken = null)
    {
        _client = new BangumiHttpClient(UserAgent, accessToken);
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

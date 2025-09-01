using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Trarizon.Bangumi.Api;
using Trarizon.Bangumi.Api.Exceptions;
using Trarizon.Bangumi.Api.Models.UserModels;
using Trarizon.Bangumi.Api.Routes;
using Trarizon.Library.Functional;

namespace Trarizon.Bangumi.CommandPalette.Utilities;
internal sealed partial class AuthorizableBangumiClient : IBangumiClient, IDisposable
{
    private const string UserAgent = "Trarizon/Trarizon.Bangumi.CommandPalette";

    private BangumiClient _client;
    private Optional<UserSelf?> _self;
    public AuthorizableBangumiClient(string? accessToken=null)
    {
        _client = new BangumiClient(UserAgent, accessToken);
    }

    public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
            => _client.SendAsync(request, cancellationToken);

    public async ValueTask<bool> IsLoggedInAsync(CancellationToken cancellationToken = default)
        => await GetSelfAsync(cancellationToken).ConfigureAwait(false) is not null;

    public async ValueTask<UserSelf?> GetSelfAsync(CancellationToken cancellationToken = default)
    {
        if (_self.TryGetValue(out var value))
            return value;

        try {
            _self = await _client.GetSelfAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (BangumiApiException) {
            _self = null;
        }
        return _self.Value;
    }

    public void Dispose() => _client.Dispose();
}

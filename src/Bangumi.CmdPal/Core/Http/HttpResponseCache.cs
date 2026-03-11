using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Trarizon.Bangumi.CmdPal.Core.Http;

internal sealed class HttpResponseCache
{
    public required HttpStatusCode StatusCode { get; init; }
    public required byte[] Content { get; init; }
    public required Dictionary<string, IEnumerable<string>> Headers { get; init; }
    public required Dictionary<string, IEnumerable<string>>? ContentHeaders { get; init; }
    public required string Version { get; init; }

    private HttpResponseCache() { }

    public static async Task<HttpResponseCache> CreateAsync(HttpResponseMessage resp)
    {
        return new HttpResponseCache
        {
            StatusCode = resp.StatusCode,
            Content = await resp.Content.ReadAsByteArrayAsync().ConfigureAwait(false),
            Headers = resp.Headers.ToDictionary(x => x.Key, x => x.Value),
            ContentHeaders = resp.Content?.Headers.ToDictionary(x => x.Key, x => x.Value),
            Version = resp.Version.ToString()
        };
    }

    public HttpResponseMessage ToResponseMessage()
    {
        var response = new HttpResponseMessage(StatusCode)
        {
            Content = new ByteArrayContent(Content),
            Version = System.Version.Parse(Version),
        };
        foreach (var header in Headers) {
            response.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
        if (response.Content != null && ContentHeaders != null) {
            foreach (var header in ContentHeaders) {
                response.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }
        return response;
    }
}

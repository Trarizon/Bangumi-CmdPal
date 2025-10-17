using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Trarizon.Bangumi.CmdPal.Utilities;

internal static class EnumerableExtensions
{
    /// <remarks>
    /// The method is not thread-safe. Use it carefully.
    /// </remarks>
    public static IAsyncEnumerable<T> CacheEnumerated<T>(this IAsyncEnumerable<T> source)
        => new CacheEnumeratedAsyncEnumerable<T>(source);

    private sealed class CacheEnumeratedAsyncEnumerable<T>(IAsyncEnumerable<T> source) : IAsyncEnumerable<T>
    {
        private readonly IAsyncEnumerator<T> _enumerator = source.GetAsyncEnumerator();
        private readonly List<T> _cache = new();

        public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            foreach (var item in _cache) {
                yield return item;
            }

            while (await _enumerator.MoveNextAsync(cancellationToken).ConfigureAwait(false)) {
                _cache.Add(_enumerator.Current);
                yield return _enumerator.Current;
            }
        }
    }
}

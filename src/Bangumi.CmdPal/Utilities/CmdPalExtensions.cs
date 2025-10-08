using Microsoft.CommandPalette.Extensions.Toolkit;
using System;

namespace Trarizon.Bangumi.CmdPal.Utilities;
internal static class CmdPalExtensions
{
    public static PageLoadingScope EnterLoadingScope(this Page page) => new(page);

    public readonly struct PageLoadingScope : IDisposable
    {
        private readonly Page _page;
        internal PageLoadingScope(Page page)
        {
            _page = page;
            page.IsLoading = true;
        }

        public void Dispose() => _page.IsLoading = false;
    }
}

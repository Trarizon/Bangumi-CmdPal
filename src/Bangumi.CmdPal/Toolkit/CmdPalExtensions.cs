using Microsoft.CommandPalette.Extensions.Toolkit;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Trarizon.Bangumi.CmdPal.Toolkit;

internal static class CmdPalExtensions
{
    private static readonly Dictionary<string, IconInfo> _icons = new();

    extension(IconInfo)
    {
        public static IconInfo FromCode(string code)
        {
            ref var val = ref CollectionsMarshal.GetValueRefOrAddDefault(_icons, code, out var exists);
            if (!exists) {
                val = new IconInfo(code);
            }
            return val!;
        }
    }

    private static readonly NoOpCommand _sharedNoOpCommand = new();

    extension(NoOpCommand)
    {
        public static NoOpCommand Shared => _sharedNoOpCommand;
    }

    extension(Page page)
    {
        public PageLoadingScope EnterLoadingScope() => new(page);
    }

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

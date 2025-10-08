using Microsoft.CommandPalette.Extensions.Toolkit;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Trarizon.Bangumi.CmdPal.Helpers;
internal static class CmdPalFactory
{
    private static CommandResult _keepOpenResult = CommandResult.KeepOpen();
    public static CommandResult KeepOpenResult() => _keepOpenResult;

    private static readonly Dictionary<string, IconInfo> _icons = new();
    public static IconInfo Icon(string icon)
    {
        ref var res = ref CollectionsMarshal.GetValueRefOrAddDefault(_icons, icon, out var exists);
        if (exists)
            return res!;

        res = new IconInfo(icon);
        return res;
    }
}

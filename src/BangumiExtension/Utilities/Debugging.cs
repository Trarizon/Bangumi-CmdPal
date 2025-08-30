using System.Diagnostics;
using System.IO;

namespace Trarizon.Bangumi.CommandPalette.Utilities;
internal static class Debugging
{
    [Conditional("DEBUG")]
    public static void Log(string message)
        => File.AppendAllText(@"C:\Users\Lenovo\Desktop\debug.txt", message + "\n");
}

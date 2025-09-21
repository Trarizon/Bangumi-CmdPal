using Microsoft.CommandPalette.Extensions.Toolkit;
using System.IO;
using ToolkitUtilities = Microsoft.CommandPalette.Extensions.Toolkit.Utilities;

namespace Trarizon.Bangumi.CommandPalette.Helpers;
internal sealed class SettingsManager : JsonSettingsManager
{
    // Reference: https://github.com/microsoft/PowerToys/blob/main/src/modules/cmdpal/ext/Microsoft.CmdPal.Ext.Calc/Helper/SettingsManager.cs#L84

    private const string Namespace = "Trarizon.Bangumi.CommandPalette";

    public SettingsManager()
    {
        FilePath = GetSettingsJsonPath();

        Settings.Add(_accessToken);
        Settings.Add(_searchDebounce);
        Settings.Add(_searchCount);
        Settings.Add(_autoSearch);

        LoadSettings();
        Settings.SettingsChanged += (s, e) => SaveSettings();
    }

    private static string Namespaced(string key) => $"{Namespace}.{key}";

    private static string GetSettingsJsonPath()
    {
        // 打包情况下会塞进统一的settings.json里，不打包我也不知道塞哪.jpg
        var dir = ToolkitUtilities.BaseSettingsPath(Namespace);
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "settings.json");
    }

    private readonly TextSetting _accessToken = new(
        Namespaced(nameof(AccessToken)),
        "Access token",
        "Access token",
        "");

    private readonly TextSetting _searchDebounce = new(
        Namespaced(nameof(SearchDebounce)),
        "搜索防抖时间",
        "在搜索框键入后到执行搜索的等待时间，这可以避免在快速键入时执行不必要的搜索请求",
        "250");

    private readonly TextSetting _searchCount = new(
        Namespaced(nameof(SearchCount)),
        "搜索结果数",
        "单次显示的搜索结果数量",
        "10");

    private readonly ToggleSetting _autoSearch = new(
        Namespaced(nameof(AutoSearch)),
        "自动触发搜索",
        "键入字符一定时间后自动触发搜索",
        true);

    public string AccessToken => _accessToken.Value ?? "";

    public int SearchDebounce => int.TryParse(_searchDebounce.Value, out var val) ? val : 250;

    public int SearchCount => int.TryParse(_searchCount.Value, out var val) ? val : 10;

    public bool AutoSearch => _autoSearch.Value;
}

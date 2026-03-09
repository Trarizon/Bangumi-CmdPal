using CommunityToolkit.Mvvm.ComponentModel;

namespace Trarizon.Bangumi.CmdPal.Core;

internal sealed partial class SettingsProvider : ObservableObject
{
    private readonly SettingsManager _settings;

    public SettingsProvider(SettingsManager manager)
    {
        _settings = manager;
        UpdateValues();
        _settings.Settings.SettingsChanged += (s, e) => UpdateValues();
    }

    private void UpdateValues()
    {
        AccessToken = _settings._accessToken.Value ?? "";
        SearchDebounce = int.TryParse(_settings._searchDebounce.Value, out var val) ? val : 250;
        SearchCount = int.TryParse(_settings._searchCount.Value, out val) ? val : 10;
    }

    [ObservableProperty]
    public partial string AccessToken { get; private set; } = "";

    [ObservableProperty]
    public partial int SearchDebounce { get; private set; }

    [ObservableProperty]
    public partial int SearchCount { get; private set; }
}

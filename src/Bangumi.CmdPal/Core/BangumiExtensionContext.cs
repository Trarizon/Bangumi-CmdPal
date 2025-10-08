using Trarizon.Bangumi.CmdPal.Helpers;
using Trarizon.Bangumi.CmdPal.Utilities;

namespace Trarizon.Bangumi.CmdPal.Core;
internal sealed class BangumiExtensionContext
{
    private string _accessToken;

    public SettingsManager Settings { get; }

    public AuthorizableBangumiClient Client { get; private set; }

    public BangumiExtensionContext(SettingsManager settings)
    {
        Settings = settings;
        Settings.SettingsChanged += s =>
        {
            if (_accessToken != s.AccessToken) {
                _accessToken = s.AccessToken;
                Client?.Dispose();
                Client = new AuthorizableBangumiClient(_accessToken);
            }
        };

        _accessToken = settings.AccessToken;
        Client = new AuthorizableBangumiClient(_accessToken);
    }
}

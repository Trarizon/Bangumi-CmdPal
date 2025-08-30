// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Trarizon.Bangumi.CommandPalette.Helpers;
using Trarizon.Bangumi.CommandPalette.Pages;

namespace Trarizon.Bangumi.CommandPalette;

public partial class BangumiExtensionCommandsProvider : CommandProvider
{
    private static readonly SettingsManager _settingsManager = new();

    private readonly ICommandItem[] _commands;

    public BangumiExtensionCommandsProvider()
    {
        DisplayName = "Bangumi";
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        Settings = _settingsManager.Settings;
        _commands = [
            new CommandItem(new MainSearchPage(_settingsManager)) {
                Title = DisplayName,
                Subtitle = "搜索、记录Bangumi条目信息",
                MoreCommands = [
                    new CommandContextItem(_settingsManager.Settings.SettingsPage)
                ]
            },
        ];
    }

    public override ICommandItem[] TopLevelCommands()
    {
        return _commands;
    }

}

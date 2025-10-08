// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using System;
using Trarizon.Bangumi.CmdPal.Core;
using Trarizon.Bangumi.CmdPal.Helpers;
using Trarizon.Bangumi.CmdPal.Pages;

namespace Trarizon.Bangumi.CmdPal;

public sealed partial class BangumiExtensionCommandsProvider : CommandProvider, IDisposable
{
    private static readonly SettingsManager _settingsManager = new();

    private readonly ICommandItem[] _commands;
    private readonly MainPage _mainPage;

    private readonly BangumiExtensionContext _context;

    public BangumiExtensionCommandsProvider()
    {
        DisplayName = "Bangumi";
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        Settings = _settingsManager.Settings;
        _context = new(_settingsManager);
        _commands = [
            new CommandItem(_mainPage = new MainPage(_context)) {
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

    public override void Dispose()
    {
        base.Dispose();
        _mainPage.Dispose();
    }
}

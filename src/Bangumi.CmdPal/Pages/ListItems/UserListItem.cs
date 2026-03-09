using Microsoft.CommandPalette.Extensions.Toolkit;
using System;
using Trarizon.Bangumi.Api.Responses.Models.Users;
using Trarizon.Bangumi.CmdPal.Core;
using Trarizon.Bangumi.CmdPal.Toolkit;
using Trarizon.Library.Functional;

namespace Trarizon.Bangumi.CmdPal.Pages.ListItems;

internal sealed partial class UserListItem : ListItem, IDisposable
{
    private readonly BangumiClient _client;
    private readonly CollectionSearchPage _collectionSearchPage;

    public UserListItem(BangumiClient client, CollectionSearchPage collectionSearchPage)
    {
        _client = client;
        _collectionSearchPage = collectionSearchPage;

        Title = "Access Token未认证";
        Icon = IconInfo.FromCode("\uE8D7");

        Command = NoOpCommand.Shared;
        MoreCommands = [];

        _client.AuthorizationStatusChanging += _client_AuthorizationStatusChanging;
        _client.AuthorizationStatusChanged += _client_AuthorizationStatusChanged;

        if (_client.AuthorizedUser.OfNotNull().TryGetValue(out var self)) {
            _client_AuthorizationStatusChanged(self);
        }
    }

    private void _client_AuthorizationStatusChanged(Optional<UserSelf?> obj)
    {
        if (!obj.TryGetValue(out var v)) {
            Title = "Access Token未认证";
            Subtitle = "认证出错。";
            Icon = IconInfo.FromCode("\uE8D7");
            Command = NoOpCommand.Shared;
            MoreCommands = [];
        }
        if (v is null) {
            Title = "Access Token未认证";
            Subtitle = "无效Access Token或已过期。";
            Icon = IconInfo.FromCode("\uE8D7");
            Command = NoOpCommand.Shared;
            MoreCommands = [];
        }
        else {
            Title = "我的时光机";
            Subtitle = $"@{v.NickName}";
            Icon = IconInfo.FromCode("\uE728");
            Command = _collectionSearchPage;
            MoreCommands = [
                new CommandContextItem(new OpenUrlCommand(BangumiHelpers.UserUrl(v))
                {
                    Name = "打开我的时光机",
                    Result = CommandResult.Dismiss(),
                })
                {
                    Title = "打开我的时光机",
                }
            ];
        }
    }

    private void _client_AuthorizationStatusChanging()
    {
        Title = "认证中...";
        Subtitle = "";
        Icon = IconInfo.FromCode("\uE8D7");
        Command = NoOpCommand.Shared;
        MoreCommands = [];
    }

    public void Dispose()
    {
        _client.AuthorizationStatusChanging -= _client_AuthorizationStatusChanging;
        _client.AuthorizationStatusChanged -= _client_AuthorizationStatusChanged;
    }
}

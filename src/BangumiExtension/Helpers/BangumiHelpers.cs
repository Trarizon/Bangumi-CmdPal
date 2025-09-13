using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Trarizon.Bangumi.Api.Responses.Models;
using Trarizon.Bangumi.Api.Responses.Models.Abstractions;

namespace Trarizon.Bangumi.CommandPalette.Helpers;
internal static class BangumiHelpers
{
    public const string HomeUrl = "https://bgm.tv";

    public static ICommand OpenHomeUrlCommand()=> new OpenUrlCommand(HomeUrl)
    {
        Result = CommandResult.Dismiss(),
    };

    public static ICommand OpenSubjectUrlCommand(ISubjectIdentity subject) => new OpenUrlCommand($"https://bgm.tv/subject/{subject.Id}")
    {
        Result = CommandResult.Dismiss(),
    };

    public static ICommand OpenEpisodeUrlCommand(Episode episode) => new OpenUrlCommand($"https://bgm.tv/ep/{episode.Id}")
    {
        Result = CommandResult.Dismiss(),
    };

    public static ICommand OpenUserUrlCommand(IUserNamed user) => new OpenUrlCommand($"https://bgm.tv/user/{user.UserName}")
    {
        Result = CommandResult.Dismiss(),
    };
}

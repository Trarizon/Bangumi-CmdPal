using Trarizon.Bangumi.Api.Responses.Models;
using Trarizon.Bangumi.Api.Responses.Models.Abstractions;

namespace Trarizon.Bangumi.CmdPal.Toolkit;
internal static class BangumiHelpers
{
    public const string HomeUrl = "https://bgm.tv";

    public static string SubjectUrl(ISubjectIdentity subject) => $"https://bgm.tv/subject/{subject.Id}";
    public static string EpisodeUrl(Episode episode) => $"https://bgm.tv/ep/{episode.Id}";
    public static string UserUrl(IUserNamed user) => $"https://bgm.tv/user/{user.UserName}";
}

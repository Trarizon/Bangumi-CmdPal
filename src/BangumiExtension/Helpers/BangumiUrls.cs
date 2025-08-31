namespace Trarizon.Bangumi.CommandPalette.Helpers;
internal static class BangumiUrls
{
    public static string Home => "https://bgm.tv";

    public static string Subject(uint id) => $"https://bgm.tv/subject/{id}";
    public static string Episode(uint id) => $"https://bgm.tv/ep/{id}";
}

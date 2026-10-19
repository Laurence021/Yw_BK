namespace Yw_BK.model
{
    /// <summary>
    /// 參數檔位置
    /// </summary>
    public class ConfigFilePath
    {
        public static readonly string LeagueReplacePath = $"{new DirectoryInfo(Application.StartupPath).Parent.Parent.Parent.FullName}\\Info\\LeagueReplace\\LeagueReplace_{Config.sport}.json";
        public static readonly string TeamReplacePath = $"{new DirectoryInfo(Application.StartupPath).Parent.Parent.Parent.FullName}\\Info\\TeamReplace\\TeamReplace_{Config.sport}.json";

    }
}

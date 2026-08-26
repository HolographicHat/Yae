global using System.Diagnostics;
global using YaeAchievement.res;

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Proto;

namespace YaeAchievement;

public static class GlobalVars {

    public static bool PauseOnExit { get; set; } = true;
    public static Version AppVersion { get; } = Assembly.GetEntryAssembly()!.GetName().Version!;

    public static readonly string AppPath = AppDomain.CurrentDomain.BaseDirectory;
    private static readonly string CommonData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
    public static readonly string DataPath = Path.Combine(CommonData, "Yae");
    public static readonly string CachePath = Path.Combine(DataPath, "cache");
    public static readonly string LibFilePath = Path.Combine(DataPath, "YaeAchievement.dll");

    public const uint   AppVersionCode = 250;
    public const string AppVersionName = "5.8.0";

    public const string PipeName = "YaeAchievementPipe";

    [field:MaybeNull]
    public static AchievementInfo AchievementInfo =>
        field ??= AchievementInfo.Parser.ParseFrom(Utils.GetBucketFile("schicksal/metadata").GetAwaiter().GetResult());

    static GlobalVars() {
        Directory.CreateDirectory(DataPath);
        Directory.CreateDirectory(CachePath);
    }

}

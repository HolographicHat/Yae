﻿using System.Text;
using YaeAchievement;
using YaeAchievement.Parsers;
using YaeAchievement.res;
using static YaeAchievement.Utils;

Console.InputEncoding = Encoding.UTF8;
Console.OutputEncoding = Encoding.UTF8;

TryDisableQuickEdit();
InstallExitHook();
InstallExceptionHook();

CheckSelfIsRunning();
CheckGenshinIsRunning();

Console.WriteLine(@"----------------------------------------------------");
Console.WriteLine(App.AppBanner, GlobalVars.AppVersionName);
Console.WriteLine(@"https://github.com/HolographicHat/YaeAchievement");
Console.WriteLine(@"----------------------------------------------------");

AppConfig.Load(args.GetOrNull(0) ?? "auto");
Export.ExportTo = ToUIntOrNull(args.GetOrNull(1)) ?? uint.MaxValue;

await CheckUpdate(ToBooleanOrFalse(args.GetOrNull(2)));

var historyCache = new CacheFile("ExportData");

AchievementAllDataNotify? data = null;
try {
    data = AchievementAllDataNotify.ParseFrom(historyCache.Read().Content.ToByteArray());
} catch (Exception) { /* ignored */ }

if (historyCache.LastWriteTime.AddMinutes(60) > DateTime.UtcNow && data != null) {
    Console.WriteLine(App.UsePreviousData);
    if (Console.ReadLine()?.ToUpper() is "Y" or "YES") {
        Export.Choose(data);
        return;
    }
}

StartAndWaitResult(AppConfig.GamePath, str => {
    GlobalVars.UnexpectedExit = false;
    var bytes = Convert.FromBase64String(str);
    var list = AchievementAllDataNotify.ParseFrom(bytes);
    historyCache.Write(bytes);
    Export.Choose(list);
    return true;
});

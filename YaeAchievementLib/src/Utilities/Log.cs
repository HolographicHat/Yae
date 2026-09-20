using System.Runtime.CompilerServices;

// ReSharper disable MemberCanBePrivate.Global

namespace Yae.Utilities;

[Flags]
internal enum LogLevel : byte {
    Trace     = 0x00,
    Debug     = 0x01,
    Info      = 0x02,
    Warn      = 0x03,
    Error     = 0x04,
    Fatal     = 0x05,
    Time      = 0x06,
    LevelMask = 0x0F,
    FileOnly  = 0x10,
}

internal static class Log {

    public static readonly string LogPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Yae");

    private static TextWriter? _consoleWriter;

    public static void UseConsoleOutput() {
        _consoleWriter = new StreamWriter($"{LogPath}/lib.log") {
            AutoFlush = true
        };
    }

    [DoesNotReturn]
    public static void ErrorAndExit(string value, [CallerMemberName] string callerName = "") {
        WriteLog(value, callerName, LogLevel.Fatal);
        Environment.Exit(0);
    }

    public static void Error(string value, [CallerMemberName] string callerName = "") {
        WriteLog(value, callerName, LogLevel.Error);
    }

    public static void Warn(string value, [CallerMemberName] string callerName = "") {
        WriteLog(value, callerName, LogLevel.Warn);
    }

    public static void Info(string value, [CallerMemberName] string callerName = "") {
        WriteLog(value, callerName, LogLevel.Info);
    }

    public static void Debug(string value, [CallerMemberName] string callerName = "") {
        WriteLog(value, callerName, LogLevel.Debug);
    }

    public static void Trace(string value, [CallerMemberName] string callerName = "") {
        WriteLog(value, callerName, LogLevel.Trace);
    }

    public static void Time(string value, [CallerMemberName] string callerName = "") {
        WriteLog(value, callerName, LogLevel.Time);
    }

    public static void WriteLog(string message, string tag, LogLevel level) {
        var time = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        if (_consoleWriter != null) {
            _consoleWriter.Write($"[{time}][{level,5}] {tag} : ");
            _consoleWriter.WriteLine(message);
        }
        if (level == LogLevel.Fatal) {
            User32.MessageBoxW(0, "An critical error occurred.", "Error", 0x10);
        }
    }
}

using System.ComponentModel;
using System.Globalization;
using System.IO.Compression;
using System.IO.Pipes;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Console;
using Proto;
using Spectre.Console;
using YaeAchievement.Parsers;
using YaeAchievement.Utilities;

namespace YaeAchievement;

public static class Utils {

    public static HttpClient CHttpClient { get; } = new (new SentryHttpMessageHandler(new HttpClientHandler {
        AutomaticDecompression = DecompressionMethods.Brotli | DecompressionMethods.GZip
    })) {
        DefaultRequestHeaders = {
            UserAgent = {
                new ProductInfoHeaderValue("YaeAchievement", GlobalVars.AppVersion.ToString(2))
            }
        }
    };

    public static async Task<byte[]> GetBucketFile(string path, bool useCache = true) {
        var transaction = SentrySdk.StartTransaction(path, "bucket.get");
        SentrySdk.ConfigureScope(static (scope, transaction) => scope.Transaction = transaction, transaction);
        var cacheKey = useCache ? path : null;
        //
        if (_updateInfo?.CdnFiles.TryGetValue(path, out var cdnFile) == true) {
            try {
                var data = await cdnFile.Urls
                    .Select(url => GetFileFromCdn(url, cacheKey, cdnFile.Hash, cdnFile.Size))
                    .WhenFirstSuccessful()
                    .Unwrap();
                transaction.Finish();
                return data;
            } catch (Exception e) when (e is SocketException or TaskCanceledException or HttpRequestException or InvalidDataException) {}
        }
        //
        try {
            var data = await WhenFirstSuccessful([
                GetFileReal($"https://rin.holohat.work/{path}", cacheKey),
                GetFileReal($"https://ena-rin.holohat.work//{path}", cacheKey),
                GetFileReal($"https://cn-cd-1259389942.file.myqcloud.com/{path}", cacheKey)
            ]).Unwrap();
            transaction.Finish();
            return data;
        } catch (Exception e) when (e is SocketException or TaskCanceledException or HttpRequestException) {
            AnsiConsole.WriteLine(App.NetworkError, e.Message);
        }
        transaction.Finish();
        Environment.Exit(-1);
        throw new UnreachableException();
        static async Task<byte[]> GetFileFromCdn(string url, string? cacheKey, uint hash, uint size) {
            var data = await GetFileReal(url, cacheKey);
            if (data.Length != size || Crc32.Compute(data) != hash) {
                throw new InvalidDataException();
            }
            if (data.Length > 44 && Unsafe.As<byte, uint>(ref data[0]) == 0x38464947) { // GIF8
                var seed = Unsafe.As<byte, uint>(ref data[44]) ^ 0x01919810;
                var hush = Unsafe.As<byte, uint>(ref data[48]) - 0x32123432; // 　　　　　　　　　？！！
                var span = data.AsSpan()[52..];
                Span<byte> xorTable = stackalloc byte[4096];
                new Random((int) seed).NextBytes(xorTable);
                for (var i = 0; i < span.Length; i++) {
                    span[i] ^= xorTable[i % 4096];
                }
                using var dataStream = new MemoryStream();
                unsafe {
                    fixed (byte* p = span) {
                        var cmpStream = new UnmanagedMemoryStream(p, span.Length);
                        using var decompressor = new BrotliStream(cmpStream, CompressionMode.Decompress);
                        // ReSharper disable once MethodHasAsyncOverload
                        decompressor.CopyTo(dataStream);
                    }
                }
                data = dataStream.ToArray();
                if (Crc32.Compute(data) != hush) {
                    throw new InvalidDataException();
                }
            }
            return data;
        }
        static async Task<byte[]> GetFileReal(string url, string? cacheKey) {
            using var reqwest = new HttpRequestMessage(HttpMethod.Get, url);
            CacheItem? cache = null;
            if (cacheKey != null && CacheFile.TryRead(cacheKey, out cache)) {
                reqwest.Headers.TryAddWithoutValidation("If-None-Match", $"{cache.Etag}");
            }
            using var response = await CHttpClient.SendAsync(reqwest);
            if (cache != null && response.StatusCode == HttpStatusCode.NotModified) {
                return cache.Content.ToByteArray();
            }
            response.EnsureSuccessStatusCode();
            var bytes = await response.Content.ReadAsByteArrayAsync();
            if (cacheKey != null) {
                var etag = response.Headers.ETag!.Tag;
                CacheFile.Write(cacheKey, bytes, etag);
            }
            return bytes;
        }
    }

    public static int ToIntOrDefault(string? value, int defaultValue = 0) {
        return value != null && int.TryParse(value, out var result) ? result : defaultValue;
    }

    public static bool ToBooleanOrDefault(string? value, bool defaultValue = false) {
        return value != null && bool.TryParse(value, out var result) ? result : defaultValue;
    }

    public static unsafe void CopyToClipboard(string text) {
        if (Native.OpenClipboard(HWND.Null)) {
            Native.EmptyClipboard();
            var hGlobal = (HGLOBAL) Marshal.AllocHGlobal((text.Length + 1) * 2);
            var hPtr = (nint) Native.GlobalLock(hGlobal);
            Marshal.Copy(text.ToCharArray(), 0, hPtr, text.Length);
            Native.GlobalUnlock((HGLOBAL) hPtr);
            Native.SetClipboardData(13,  new HANDLE(hPtr));
            Marshal.FreeHGlobal(hGlobal);
            Native.CloseClipboard();
        } else {
            throw new Win32Exception();
        }
    }

    // ReSharper disable once NotAccessedField.Local
    private static UpdateInfo? _updateInfo;

    public static Task StartSpinnerAsync(string status, Func<StatusContext, Task> func) {
        return AnsiConsole.Status().Spinner(Spinner.Known.SimpleDotsScrolling).StartAsync(status, func);
    }

    public static Task<T> StartSpinnerAsync<T>(string status, Func<StatusContext, Task<T>> func) {
        return AnsiConsole.Status().Spinner(Spinner.Known.SimpleDotsScrolling).StartAsync(status, func);
    }

    public static async Task CheckUpdate(bool useLocalLib) {
        try {
            var versionData = await StartSpinnerAsync(App.UpdateChecking, _ => GetBucketFile("schicksal/version"));
            var versionInfo = _updateInfo = UpdateInfo.Parser.ParseFrom(versionData)!;
            if (GlobalVars.AppVersionCode < versionInfo.VersionCode) {
                AnsiConsole.WriteLine(App.UpdateNewVersion, GlobalVars.AppVersionName, versionInfo.VersionName);
                AnsiConsole.WriteLine(App.UpdateDescription, versionInfo.Description);
                if (versionInfo.EnableAutoUpdate) {
                    var newBin = await StartSpinnerAsync(App.UpdateDownloading, _ => GetBucketFile(versionInfo.PackageLink));
                    var tmpPath = Path.GetTempFileName();
                    var updaterPath = Path.Combine(GlobalVars.DataPath, "update.exe");
                    await using (var dstStream = File.Open($"{GlobalVars.DataPath}/update.exe", FileMode.Create)) {
                        await using var srcStream = typeof(Program).Assembly.GetManifestResourceStream("updater")!;
                        await srcStream.CopyToAsync(dstStream);
                    }
                    await File.WriteAllBytesAsync(tmpPath, newBin);
                    ShellOpen(updaterPath, $"{Environment.ProcessId} \"{tmpPath}\"");
                    await StartSpinnerAsync(App.UpdateChecking, _ => Task.Delay(1919810));
                    GlobalVars.PauseOnExit = false;
                    Environment.Exit(0);
                }
                AnsiConsole.MarkupLine($"[link]{App.DownloadLink}[/]", versionInfo.PackageLink);
                if (versionInfo.ForceUpdate) {
                    Environment.Exit(0);
                }
            }
            if (versionInfo.EnableLibDownload && !useLocalLib) {
                var data = await GetBucketFile("schicksal/lic.dll");
                await File.WriteAllBytesAsync(GlobalVars.LibFilePath, data); // 要求重启电脑
            }
        } catch (IOException e) when ((uint) e.HResult == 0x80070020) { // ERROR_SHARING_VIOLATION
            // IO_SharingViolation_File
            // The process cannot access the file '{0}' because it is being used by another process.
            AnsiConsole.WriteLine("文件 {0} 被其它程序占用，请 重启电脑 或 解除文件占用 后重试。", e.Message[36..^46]);
            Environment.Exit(-1);
        }
    }

    // ReSharper disable once UnusedMethodReturnValue.Global
    public static bool ShellOpen(string path, string args = "") {
        try {
            return new Process {
                StartInfo = new ProcessStartInfo {
                    FileName = path,
                    UseShellExecute = true,
                    Arguments = args
                }
            }.Start();
        } catch (Exception) {
            return false;
        }
    }

    internal static Process? GetGameProcess() => Process.GetProcessesByName("YuanShen")
        .Concat(Process.GetProcessesByName("GenshinImpact"))
        .FirstOrDefault(p => File.Exists($"{p.GetFileName()}/../HoYoKProtect.sys"));

    private static GameProcess? _proc;

    public static void InstallExitHook() {
        AppDomain.CurrentDomain.ProcessExit += (_, _) => {
            _proc?.Terminate(0);
            if (GlobalVars.PauseOnExit) {
                AnsiConsole.WriteLine(App.PressKeyToExit);
                Console.ReadKey();
            }
        };
    }

    public static void InstallExceptionHook() {
        AppDomain.CurrentDomain.UnhandledException += (_, e) => OnUnhandledException((Exception) e.ExceptionObject);
    }

    public static void OnUnhandledException(Exception ex) {
        SentrySdk.CaptureException(ex);
        switch (ex) {
            case ApplicationException ex1:
                AnsiConsole.WriteLine(ex1.Message);
                break;
            case SocketException ex2:
                AnsiConsole.WriteLine(App.ExceptionNetwork, nameof(SocketException), ex2.Message);
                break;
            case HttpRequestException ex3:
                AnsiConsole.WriteLine(App.ExceptionNetwork, nameof(HttpRequestException), ex3.Message);
                break;
            default:
                AnsiConsole.WriteLine(ex.ToString());
                break;
        }
        Environment.Exit(-1);
    }

    private static bool _isUnexpectedExit = true;

    // ReSharper disable once UnusedMethodReturnValue.Global
    public static void StartAndWaitResult(string exePath, Action onFinish) {
        var hash = GetGameHash(exePath);
        var nativeConf = GlobalVars.AchievementInfo.NativeConfig;
        if (!nativeConf.MethodRva.TryGetValue(hash, out var methodRva)) {
            AnsiConsole.WriteLine($"No match config {exePath} {hash:X8}");
            Environment.Exit(0);
            return;
        }
        Task.Run(() => {
            try {
                using var stream = new NamedPipeServerStream(GlobalVars.PipeName);
                using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, true);
                using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true);
                stream.WaitForConnection();
                int type;
                while ((type = stream.ReadByte()) != -1) {
                    switch (type) {
                        case 0x03:
                            writer.Write(PlayerPropNotify.OnReceive(reader));
                            break;
                        case 0x04:
                            var cmdId = reader.ReadUInt16();
                            if (cmdId == nativeConf.AchievementCmdId) {
                                writer.Write(AchievementAllDataNotify.OnReceive(reader));
                                break;
                            }
                            if (cmdId == nativeConf.StoreCmdId) {
                                writer.Write(PlayerStoreNotify.OnReceive(reader));
                                break;
                            }
                            writer.Write(true);
                            break;
                        case 0xFA:
                            writer.Write(nativeConf.AchievementCmdId);
                            writer.Write(nativeConf.StoreCmdId);
                            writer.Write(uint.MaxValue); // EOF
                            break;
                        case 0xFB:
                            foreach (var propType in PlayerPropNotify.Props.Keys) {
                                writer.Write((int) propType);
                            }
                            writer.Write(uint.MaxValue); // EOF
                            break;
                        case 0xFD:
                            writer.Write(methodRva.DoCmd);
                            writer.Write(methodRva.UpdateNormalProp);
                            writer.Write(methodRva.NewString);
                            writer.Write(methodRva.FindGameObject);
                            writer.Write(methodRva.EventSystemUpdate);
                            writer.Write(methodRva.SimulatePointerClick);
                            writer.Write(methodRva.ToInt32);
                            writer.Write(methodRva.TcpStatePtr);
                            writer.Write(methodRva.SharedInfoPtr);
                            writer.Write(methodRva.Decompress);
                            break;
                        case 0xFE:
                            _proc!.ResumeMainThread();
                            break;
                        case 0xFF:
                            writer.Write(true);
                            _isUnexpectedExit = false;
                            onFinish();
                            return;
                    }
                }
            } catch (IOException e) when (e.Message == "Pipe is broken.") { } // SR.IO_PipeBroken
        }).ContinueWith(task => { if (task.IsFaulted) OnUnhandledException(task.Exception!); });
        _proc = new GameProcess(exePath);
        _proc.LoadLibrary(GlobalVars.LibFilePath);
        _proc.OnExit += () => {
            if (_isUnexpectedExit) {
                _proc = null;
                AnsiConsole.WriteLine(App.GameProcessExit);
                Environment.Exit(114514);
            }
        };
        AnsiConsole.WriteLine(App.GameLoading, _proc.Id);
    }

    public static uint GetGameHash(string exePath) {
        try {
            Span<byte> buffer = stackalloc byte[0x10000];
            using var stream = File.OpenRead(exePath);
            _ = stream.ReadAtLeast(buffer, 0x10000, false);
            return Crc32.Compute(buffer);
        } catch (IOException) {
            return 0xFFFFFFFF;
        }
    }

    internal static unsafe void SetQuickEditMode(bool enable) {
        var handle = Native.GetStdHandle(STD_HANDLE.STD_INPUT_HANDLE);
        CONSOLE_MODE mode = default;
        Native.GetConsoleMode(handle, &mode);
        mode = enable ? mode | CONSOLE_MODE.ENABLE_QUICK_EDIT_MODE : mode &~CONSOLE_MODE.ENABLE_QUICK_EDIT_MODE;
        Native.SetConsoleMode(handle, mode);
    }

    internal static unsafe void FixTerminalFont() {
        if (!CultureInfo.CurrentCulture.Name.StartsWith("zh")) {
            return;
        }
        var handle = Native.GetStdHandle(STD_HANDLE.STD_OUTPUT_HANDLE);
        var fontInfo = new CONSOLE_FONT_INFOEX {
            cbSize = (uint) sizeof(CONSOLE_FONT_INFOEX)
        };
        if (!Native.GetCurrentConsoleFontEx(handle, false, &fontInfo)) {
            return;
        }
        if (fontInfo.FaceName.ToString() == "Terminal") { // 点阵字体
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US"); // todo: use better way like auto set console font etc.
        }
    }

    // https://stackoverflow.com/a/76953892
    private static async Task<Task<TResult>> WhenFirstSuccessful<TResult>(this IEnumerable<Task<TResult>> tasks) {
        var cts = new CancellationTokenSource();
        Task<TResult>? selectedTask = null;
        var continuations = tasks
            .TakeWhile(_ => !cts.IsCancellationRequested)
            .Select(task => {
                return task.ContinueWith(t => {
                    if (t.IsCompletedSuccessfully) {
                        if (Interlocked.CompareExchange(ref selectedTask, t, null) is null) {
                            cts.Cancel();
                        }
                    }
                }, cts.Token, TaskContinuationOptions.DenyChildAttach | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            });
        var whenAll = Task.WhenAll(continuations);
        try {
            await whenAll.ConfigureAwait(false);
        } catch when (whenAll.IsCanceled) { /* ignore */ }
        return selectedTask!;
    }
}

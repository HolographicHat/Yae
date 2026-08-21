using System.Buffers.Binary;
using System.Runtime.InteropServices;
using Yae.Utilities;
using static Yae.GameMethod;

namespace Yae;

internal static unsafe class Application {

    private static bool _initialized;

    [UnmanagedCallersOnly(EntryPoint = "YaeMain")]
    private static uint Awake(nint hModule) {
        if (Interlocked.Exchange(ref _initialized, true)) {
            return 1;
        }
        Native.RegisterUnhandledExceptionHandler();
        Log.UseConsoleOutput();
        Log.Trace("~");
        Goshujin.Init();
        Goshujin.LoadCmdTable();
        Goshujin.LoadMethodTable();
        Goshujin.ResumeMainThread();
        //
        Native.WaitMainWindow();
        Log.ResetConsole();
        //
        MinHook.Attach(ToInt32, &OnToInt32, out _toInt32);
        MinHook.Attach(UpdateNormalProp, &OnUpdateNormalProp, out _updateNormalProp);
        return 0;
    }

    [UnmanagedCallersOnly(EntryPoint = "YaeWndHook")]
    private static nint WndHook(int nCode, nint wParam, nint lParam) {
        ((delegate*unmanaged<nint, uint>) &Awake)(0);
        return User32.CallNextHookEx(0, nCode, wParam, lParam);
    }

    #region RecvPacket

    private static delegate*unmanaged<byte*, int, int> _toInt32;

    [UnmanagedCallersOnly]
    private static int OnToInt32(byte* val, int startIndex) {
        var ret = _toInt32(val, startIndex);
        if (startIndex != 6 || *(ushort*) (val += 0x20) != 0x6745) {
            return ret;
        }
        var cmdId = BinaryPrimitives.ReverseEndianness(*(ushort*) (val + 2));
        if (cmdId == CmdId.PlayerStoreNotify) {
            Goshujin.PushStoreData(GetData(val));
        } else if (cmdId == CmdId.AchievementAllDataNotify) {
            Goshujin.PushAchievementData(GetData(val));
        } else if (cmdId == CmdId.AvatarDataNotify) {
            Goshujin.PushAvatarData(GetData(val));
        }
        return ret;
        static Span<byte> GetData(byte* val) {
            var headLen = BinaryPrimitives.ReverseEndianness(*(ushort*) (val + 4));
            var headPtr = val + 10;
            var dataLen = BinaryPrimitives.ReverseEndianness(*(uint*) (val + 6));
            var dataPtr = val + 10 + headLen;
            var unzipLen = GetDecompressedSize(new Span<byte>(headPtr, headLen));
            if (unzipLen == 0) {
                return new Span<byte>(dataPtr, (int) dataLen);
            }
            var unzipBuf = NativeMemory.Alloc(unzipLen);
            if (!Decompress(*TcpStatePtr, *SharedInfoPtr, dataPtr, dataLen, unzipBuf, unzipLen)) {
                throw new InvalidDataException("Decompress failed.");
            }
            return new Span<byte>(unzipBuf, (int) unzipLen);
        }
    }

    private static uint GetDecompressedSize(Span<byte> header) {
        var offset = 0;
        ulong tag;
        while (offset != header.Length && (tag = ReadRawVarInt64(header, ref offset)) != 0) {
            if (tag == 64) {
                return (uint) ReadRawVarInt64(header, ref offset);
            }
            switch (tag & 7) {
                case 0:
                    ReadRawVarInt64(header, ref offset);
                    break;
                case 1:
                    offset += 8;
                    break;
                case 2:
                    offset += (int) ReadRawVarInt64(header, ref offset);
                    break;
                case 3:
                case 4:
                    throw new NotSupportedException();
                case 5:
                    offset += 4;
                    break;
            }
        }
        return 0;
    }

    private static ulong ReadRawVarInt64(Span<byte> span, ref int offset) {
        ulong result = 0;
        for (var i = 0; i < 8; i++) {
            var b = span[offset++];
            result |= (ulong) (b & 0x7F) << (i * 7);
            if (b < 0x80) {
                return result;
            }
        }
        throw new InvalidDataException("CodedInputStream encountered a malformed varint.");
    }

    #endregion

    #region Prop

    /*
     * PROP_PLAYER_HCOIN = 10015,
     * PROP_PLAYER_WAIT_SUB_HCOIN = 10022,
     * PROP_PLAYER_SCOIN = 10016,
     * PROP_PLAYER_WAIT_SUB_SCOIN = 10023,
     * PROP_PLAYER_MCOIN = 10025,
     * PROP_PLAYER_WAIT_SUB_MCOIN = 10026,
     * PROP_PLAYER_HOME_COIN = 10042,
     * PROP_PLAYER_WAIT_SUB_HOME_COIN = 10043,
     * PROP_PLAYER_ROLE_COMBAT_COIN = 10053,
     * PROP_PLAYER_MUSIC_GAME_BOOK_COIN = 10058,
     */
    public static HashSet<int> RequiredPlayerProperties { get; } = [
        10015, 10022, 10016, 10023, 10025, 10026, 10042, 10043, 10053, 10058
    ];

    private static delegate*unmanaged<nint, int, double, double, int, void> _updateNormalProp;

    [UnmanagedCallersOnly]
    private static void OnUpdateNormalProp(nint @this, int type, double value, double lastValue, int state) {
        _updateNormalProp(@this, type, value, lastValue, state);
        if (RequiredPlayerProperties.Remove(type)) {
            Goshujin.PushPlayerProp(type, value);
        }
    }

    #endregion

}

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
        Goshujin.LoadTasks();
        Goshujin.LoadMethodTable();
        Goshujin.ResumeMainThread();
        //
        Native.WaitMainWindow();
        Log.ResetConsole();
        //
        MinHook.Attach(ToInt32, &OnToInt32, out _toInt32);
        MinHook.Attach(UpdateNormalProp, &OnUpdateNormalProp, out _updateNormalProp);
        if ((nint) EventSystemUpdate != Native.ModuleBase) {
            MinHook.Attach(EventSystemUpdate, &OnEventSystemUpdate, out _eventSystemUpdate);
        }
        return 0;
    }

    [UnmanagedCallersOnly(EntryPoint = "YaeWndHook")]
    private static nint WndHook(int nCode, nint wParam, nint lParam) {
        ((delegate*unmanaged<nint, uint>) &Awake)(0);
        return User32.CallNextHookEx(0, nCode, wParam, lParam);
    }

    #region RecvPacket

    internal static readonly HashSet<uint> RequiredPackets = [];

    private static delegate*unmanaged<byte*, int, int> _toInt32;

    [UnmanagedCallersOnly]
    private static int OnToInt32(byte* val, int startIndex) {
        var ret = _toInt32(val, startIndex);
        if (startIndex != 6 || *(ushort*) (val += 0x20) != 0x6745) {
            return ret;
        }
        var cmdId = BinaryPrimitives.ReverseEndianness(*(ushort*) (val + 2));
        if (RequiredPackets.Contains(cmdId) && TryGetData(val, out var data)) {
            Goshujin.PushPacketData(cmdId, data);
        }
        return ret;
        static bool TryGetData(byte* val, out Span<byte> data) {
            var headLen = BinaryPrimitives.ReverseEndianness(*(ushort*) (val + 4));
            var headPtr = val + 10;
            var dataLen = BinaryPrimitives.ReverseEndianness(*(uint*) (val + 6));
            var dataPtr = val + 10 + headLen;
            if (*(ushort*) (val + 10 + headLen + dataLen) != 0xAB89) {
                data = default;
                return false;
            }
            var unzipLen = GetDecompressedSize(new Span<byte>(headPtr, headLen));
            if (unzipLen == 0) {
                data = new Span<byte>(dataPtr, (int) dataLen);
                return true;
            }
            var unzipBuf = NativeMemory.Alloc(unzipLen);
            if (!Decompress(*TcpStatePtr, *SharedInfoPtr, dataPtr, dataLen, unzipBuf, unzipLen)) {
                throw new InvalidDataException("Decompress failed.");
            }
            data = new Span<byte>(unzipBuf, (int) unzipLen);
            return true;
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

    internal static readonly HashSet<int> RequiredPlayerProperties = [];

    private static delegate*unmanaged<nint, int, double, double, int, void> _updateNormalProp;

    [UnmanagedCallersOnly]
    private static void OnUpdateNormalProp(nint @this, int type, double value, double lastValue, int state) {
        _updateNormalProp(@this, type, value, lastValue, state);
        Goshujin.PushPlayerProp(type, value);
    }

    #endregion

    #region EnterGate

    private static long _lastTryEnterTime;
    
    private static delegate*unmanaged<nint, void> _eventSystemUpdate;

    [UnmanagedCallersOnly]
    public static void OnEventSystemUpdate(nint @this) {
        _eventSystemUpdate(@this);
        if (Environment.TickCount64 - _lastTryEnterTime > 200) {
            var obj = FindGameObject(NewString("BtnStart"u8.AsPointer()));
            if (obj != 0 && SimulatePointerClick(@this, obj)) {
                MinHook.Detach((nint) EventSystemUpdate);
            }
            _lastTryEnterTime = Environment.TickCount64;
        }
    }

    #endregion

}

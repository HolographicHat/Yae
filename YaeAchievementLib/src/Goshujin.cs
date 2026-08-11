using System.IO.Pipes;
using Yae.Utilities;

namespace Yae;

internal static class CmdId {

    public static uint AchievementAllDataNotify { get; set; }

    public static uint PlayerStoreNotify { get; set; }

}

internal static unsafe class GameMethod {

    public static delegate*unmanaged<nint, int, double, double, int, void> UpdateNormalProp { get; set; }

    public static delegate*unmanaged<nint, nint> NewString { get; set; }

    public static delegate*unmanaged<nint, nint> FindGameObject { get; set; }

    public static delegate*unmanaged<nint, void> EventSystemUpdate { get; set; }

    public static delegate*unmanaged<nint, nint, bool> SimulatePointerClick { get; set; }

    public static delegate*unmanaged<byte*, int, int> ToInt32 { get; set; }

    public static void** TcpStatePtr { get; set; }

    public static void** SharedInfoPtr { get; set; }

    public static delegate*unmanaged<void*, void*, void*, uint, void*, uint, bool> Decompress { get; set; }

}

internal static class Goshujin {

    private static NamedPipeClientStream _pipeStream = null!;
    private static BinaryReader _pipeReader = null!;
    private static BinaryWriter _pipeWriter = null!;
    private static Lock _lock = null!;

    public static void Init(string pipeName = "YaeAchievementPipe") {
        _lock = new Lock();
        _pipeStream = new NamedPipeClientStream(pipeName);
        _pipeReader = new BinaryReader(_pipeStream);
        _pipeWriter = new BinaryWriter(_pipeStream);
        _pipeStream.Connect();
        Log.Trace("Pipe server connected.");
    }

    public static void PushAchievementData(Span<byte> data) {
        using (_lock.EnterScope()) {
            _pipeWriter.Write((byte) 1);
            _pipeWriter.Write(data.Length);
            _pipeWriter.Write(data);
            _achievementDataPushed = true;
            ExitIfFinished();
        }
    }

    public static void PushStoreData(Span<byte> data) {
        using (_lock.EnterScope()) {
            _pipeWriter.Write((byte) 2);
            _pipeWriter.Write(data.Length);
            _pipeWriter.Write(data);
            _storeDataPushed = true;
            ExitIfFinished();
        }
    }

    public static void PushPlayerProp(int type, double value) {
        using (_lock.EnterScope()) {
            _pipeWriter.Write((byte) 3);
            _pipeWriter.Write(type);
            _pipeWriter.Write(value);
            ExitIfFinished();
        }
    }

    public static void LoadCmdTable() {
        _pipeWriter.Write((byte) 0xFC);
        CmdId.AchievementAllDataNotify = _pipeReader.ReadUInt32();
        CmdId.PlayerStoreNotify = _pipeReader.ReadUInt32();
    }

    public static unsafe void LoadMethodTable() {
        _pipeWriter.Write((byte) 0xFD);
        _ = _pipeReader.ReadUInt32();
        GameMethod.UpdateNormalProp = (delegate*unmanaged<nint, int, double, double, int, void>) Native.RVAToVA(_pipeReader.ReadUInt32());
        GameMethod.NewString = (delegate*unmanaged<nint, nint>) Native.RVAToVA(_pipeReader.ReadUInt32());
        GameMethod.FindGameObject = (delegate*unmanaged<nint, nint>) Native.RVAToVA(_pipeReader.ReadUInt32());
        GameMethod.EventSystemUpdate = (delegate*unmanaged<nint, void>) Native.RVAToVA(_pipeReader.ReadUInt32());
        GameMethod.SimulatePointerClick = (delegate*unmanaged<nint, nint, bool>) Native.RVAToVA(_pipeReader.ReadUInt32());
        GameMethod.ToInt32 = (delegate*unmanaged<byte*, int, int>) Native.RVAToVA(_pipeReader.ReadUInt32());
        GameMethod.TcpStatePtr = (void**) Native.RVAToVA(_pipeReader.ReadUInt32());
        GameMethod.SharedInfoPtr = (void**) Native.RVAToVA(_pipeReader.ReadUInt32());
        GameMethod.Decompress = (delegate*unmanaged<void*, void*, void*, uint, void*, uint, bool>) Native.RVAToVA(_pipeReader.ReadUInt32());
    }

    public static void ResumeMainThread() {
        _pipeWriter.Write((byte) 0xFE);
    }

    private static bool _storeDataPushed;

    private static bool _achievementDataPushed;

    private static void ExitIfFinished() {
        if (_storeDataPushed && _achievementDataPushed && Application.RequiredPlayerProperties.Count == 0) {
            _pipeWriter.Write((byte) 0xFF);
            _pipeReader.ReadBoolean();
            Environment.Exit(0);
        }
    }
}

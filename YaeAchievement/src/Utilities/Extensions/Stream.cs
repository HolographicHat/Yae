using System.ComponentModel;
using System.Runtime.CompilerServices;
using Spectre.Console;

// ReSharper disable CheckNamespace

namespace Google.Protobuf;

[EditorBrowsable(EditorBrowsableState.Never)]
internal static class BinaryReaderExtensions {

    public static byte[] ReadBytes(this BinaryReader reader) {
        try {
            var length = reader.ReadInt32();
            if (length is < 0 or > 114514 * 2) {
                throw new ArgumentException(nameof(length));
            }
            return reader.ReadBytes(length);
        } catch (Exception e) when (e is IOException or ArgumentException) {
            AnsiConsole.WriteLine(App.StreamReadDataFail);
            Environment.Exit(-1);
            throw new UnreachableException();
        }
    }

}

[EditorBrowsable(EditorBrowsableState.Never)]
internal static class CodedInputStreamExtensions {

    [UnsafeAccessor(UnsafeAccessorKind.Method)]
    private static extern byte[] ReadRawBytes(CodedInputStream stream, int size);

    public static CodedInputStream ReadLengthDelimitedAsStream(this CodedInputStream stream) {
        return new CodedInputStream(ReadRawBytes(stream, stream.ReadLength()));
    }

    public static byte[] ReadLengthDelimitedBytes(this CodedInputStream stream) {
        return ReadRawBytes(stream, stream.ReadLength());
    }

}

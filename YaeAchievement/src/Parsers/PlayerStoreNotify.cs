using Google.Protobuf;
using Proto;
using Spectre.Console;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable CollectionNeverQueried.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global

namespace YaeAchievement.Parsers;

public sealed class PlayerStoreNotify {

    public uint WeightLimit { get; set; }

    public StoreType StoreType { get; set; }

    public List<Item> ItemList { get; set; } = [];

    public static PlayerStoreNotify Instance { get; } = new ();

    public static bool OnReceive(BinaryReader reader) {
        var bytes = reader.ReadBytes();
        //Instance.ParseFrom(bytes);
        return true;
    }

    private void ParseFrom(byte[] bytes) {
        using var stream = new CodedInputStream(bytes);
        try {
            uint tag;
            while ((tag = stream.ReadTag()) != 0) {
                var wireType = tag & 7;
                switch (wireType) {
                    case 0: { // is VarInt
                        var value = stream.ReadUInt32();
                        if (value < 10) {
                            StoreType = (StoreType) value;
                        } else {
                            WeightLimit = value;
                        }
                        continue;
                    }
                    case 2: { // is LengthDelimited
                        using var eStream = stream.ReadLengthDelimitedAsStream();
                        while (eStream.PeekTag() != 0) {
                            ItemList.Add(Item.Parser.ParseFrom(eStream));
                        }
                        break;
                    }
                }
            }
        } catch (InvalidProtocolBufferException) {
            // ReSharper disable once LocalizableElement
            AnsiConsole.WriteLine("Parse failed");
            File.WriteAllBytes("store_raw_data.bin", bytes);
            Environment.Exit(0);
        }
    }

}

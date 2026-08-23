using Google.Protobuf;
using Proto;
using Spectre.Console;

namespace YaeAchievement.Parsers;

public sealed class AvatarDataNotify
{

    public static List<AvatarInfo> AvatarList { get; private set; } = [];

    private static bool _received;

    public static bool OnReceive(BinaryReader reader)
    {
        var bytes = reader.ReadBytes();
        _received = true;
        AvatarList = ParseFrom(bytes);
        return true;
    }

    public static void OnFinish()
    {
        if (!_received)
        {
            AnsiConsole.WriteLine("AvatarDataNotify not received");
            return;
        }
    }

    // 只解析 repeated AvatarInfo avatar_list，其余字段一律丢弃。
    // 由于每个版本 avatar_list 的字段号会变，这里不写死字段号，
    // 而是对所有顶层 wire=2 字段按元素"是否像 AvatarInfo"打分，取最高者。
    public static List<AvatarInfo> ParseFrom(byte[] bytes)
    {
        using var stream = new CodedInputStream(bytes);
        var candidates = new Dictionary<uint, List<byte[]>>();
        try
        {
            uint tag;
            while ((tag = stream.ReadTag()) != 0)
            {
                var field = tag >> 3;
                switch (tag & 7)
                {
                    case 0: stream.ReadUInt64(); break;
                    case 1: stream.ReadFixed64(); break;
                    case 2:
                        {
                            if (!candidates.TryGetValue(field, out var list)) candidates[field] = list = [];
                            list.Add(stream.ReadLengthDelimitedBytes());
                            break;
                        }
                    case 5: stream.ReadFixed32(); break;
                    default:
                        throw new InvalidDataException();
                }
            }
        }
        catch (Exception)
        {
            AnsiConsole.WriteLine("AvatarDataNotify parse failed");
            File.WriteAllBytes("avatar_data_raw.bin", bytes);
            Environment.Exit(0);
        }
        var bestScore = 0;
        var bestField = 0;
        foreach (var (field, elements) in candidates)
        {
            var score = elements.Count(LooksLikeAvatarInfo);
            if (score > bestScore)
            {
                bestScore = score;
                bestField = (int)field;
            }
        }
        if (bestField == 0)
        {
            AnsiConsole.WriteLine("No AvatarInfo field found in AvatarDataNotify");
            File.WriteAllBytes("avatar_data_raw.bin", bytes);
            Environment.Exit(0);
        }
        return candidates[(uint)bestField].Select(AvatarInfo.Parser.ParseFrom).ToList();
    }

    // AvatarInfo 内部签名：字段 1/2 都是 varint（avatar_id / guid，历代稳定），
    // 且有 >= 4 个去重字段。map 项 / rename 项只有 1~2 个字段，会被排除。
    private static bool LooksLikeAvatarInfo(byte[] element)
    {
        try
        {
            var seen = new HashSet<uint>();
            var hasField1 = false;
            var hasField2 = false;
            using var stream = new CodedInputStream(element);
            uint tag;
            while ((tag = stream.ReadTag()) != 0)
            {
                var field = tag >> 3;
                seen.Add(field);
                switch (tag & 7)
                {
                    case 0:
                        stream.ReadUInt64();
                        if (field == 1) hasField1 = true;
                        else if (field == 2) hasField2 = true;
                        break;
                    case 1: stream.ReadFixed64(); break;
                    case 2: stream.ReadLength(); break;
                    case 5: stream.ReadFixed32(); break;
                    default: return false;
                }
            }
            return hasField1 && hasField2 && seen.Count >= 4;
        }
        catch (InvalidProtocolBufferException)
        {
            return false;
        }
    }
}

using Proto;

using static YaeAchievement.Parsers.PropType;

namespace YaeAchievement.Parsers;

public enum PropType {
    None = 0,
    Exp = 1001,
    BreakLevel = 1002,
    SatiationVal = 1003,
    SatiationPenaltyTime = 1004,
    GearStartVal = 2001,
    GearStopVal = 2002,
    Level = 4001,
    LastChangeAvatarTime = 10001,
    MaxSpringVolume = 10002,
    CurSpringVolume = 10003,
    IsSpringAutoUse = 10004,
    SpringAutoUsePercent = 10005,
    IsFlyable = 10006,
    IsWeatherLocked = 10007,
    IsGameTimeLocked = 10008,
    IsTransferable = 10009,
    MaxStamina = 10010,
    CurPersistStamina = 10011,
    CurTemporaryStamina = 10012,
    PlayerLevel = 10013,
    PlayerExp = 10014,
    PlayerHCoin = 10015,
    PlayerSCoin = 10016,
    PlayerMpSettingType = 10017,
    IsMpModeAvailable = 10018,
    PlayerWorldLevel = 10019,
    PlayerResin = 10020,
    PlayerWaitSubHCoin = 10022,
    PlayerWaitSubSCoin = 10023,
    IsOnlyMpWithPsPlayer = 10024,
    PlayerMCoin = 10025,
    PlayerWaitSubMCoin = 10026,
    PlayerLegendaryKey = 10027,
    IsHasFirstShare = 10028,
    PlayerForgePoint = 10029,
    CurClimateMeter = 10035,
    CurClimateType = 10036,
    CurClimateAreaId = 10037,
    CurClimateAreaClimateType = 10038,
    PlayerWorldLevelLimit = 10039,
    PlayerWorldLevelAdjustCd = 10040,
    PlayerLegendaryDailyTaskNum = 10041,
    PlayerHomeCoin = 10042,
    PlayerWaitSubHomeCoin = 10043,
    IsAutoUnlockSpecificEquip = 10044,
    PlayerGCGCoin = 10045,
    PlayerWaitSubGCGCoin = 10046,
    PlayerOnlineTime = 10047,
    IsDiveable = 10048,
    MaxDiveStamina = 10049,
    CurPersistDiveStamina = 10050,
    IsCanPutFiveStarReliquary = 10051,
    IsAutoLockFiveStarReliquary = 10052,
    PlayerRoleCombatCoin = 10053,
    CurPhlogiston = 10054,
    ReliquaryTemporaryExp = 10055,
    IsMpCrossPlatformEnabled = 10056,
    IsOnlyMpWithPlatformPlayer = 10057,
    PlayerMusicGameBookCoin = 10058,
    IsNotShowReliquaryRecommendProp = 10059,
}

public static class PlayerPropNotify {

    internal static readonly Dictionary<PropType, double> Props = new () {
        {PlayerHCoin, 0},
        {PlayerSCoin, 0},
        {PlayerMCoin, 0},
        {PlayerHomeCoin, 0},
        {PlayerWaitSubHCoin, 0},
        {PlayerWaitSubSCoin, 0},
        {PlayerWaitSubMCoin, 0},
        {PlayerRoleCombatCoin, 0},
        {PlayerWaitSubHomeCoin, 0},
        {PlayerMusicGameBookCoin, 0},
    };

    public static bool OnReceive(BinaryReader reader) {
        var propType = reader.ReadInt32();
        var propValue = reader.ReadDouble();
        Props[(PropType) propType] = propValue;
        return true;
    }

    public static void OnFinish() {
        PlayerStoreNotify.Instance.ItemList.AddRange([
            CreateVirtualItem(201, Props[PlayerHCoin] - Props[PlayerWaitSubHCoin]),
            CreateVirtualItem(202, Props[PlayerSCoin] - Props[PlayerWaitSubSCoin]),
            CreateVirtualItem(203, Props[PlayerMCoin] - Props[PlayerWaitSubMCoin]),
            CreateVirtualItem(204, Props[PlayerHomeCoin] - Props[PlayerWaitSubHomeCoin]),
            CreateVirtualItem(206, Props[PlayerRoleCombatCoin]),
            CreateVirtualItem(207, Props[PlayerMusicGameBookCoin]),
        ]);
    }

    private static Item CreateVirtualItem(uint id, double count) {
        return new Item {
            ItemId = id,
            VirtualItem = new VirtualItem {
                Count = (long) count
            }
        };
    }

}

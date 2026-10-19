using SubmarineTracker.Resources;

namespace SubmarineTracker.Data;

public enum LevelingObjective
{
    // 航線照「最大時長」設定挑、配置照每趟經驗挑（加這個選項之前的算法）。
    Default = 0,
    ExpPerMinute = 1,
    ExpPerVoyage = 2,
}

/// <summary>
/// 建造器「可用部件」：設定裡 40 格開關，索引＝SubmarinePart 列號 - 1。
/// 列號＝級別 × 4 ＋ 部位（1 船首、2 艦橋、3 船體、4 船尾），級別 0～4 是一般、5～9 是改級。
/// </summary>
public static class PartPool
{
    public const int Count = 40;
    public const int Sets = 10;

    public const int Bow = 1;
    public const int Bridge = 2;
    public const int Hull = 3;
    public const int Stern = 4;

    public static int PartId(int set, int slot) => (set * 4) + slot;

    /// <summary>
    /// 長度不對就整份重設為全開；某個部位一格都沒開時那個部位全開（求解器至少要有一種組合）。
    /// 回傳的就是設定裡那份陣列。
    /// </summary>
    public static bool[] Sanitize(Configuration config)
    {
        if (config.AvailableShipParts is not { Length: Count })
            config.AvailableShipParts = Enumerable.Repeat(true, Count).ToArray();

        var parts = config.AvailableShipParts;
        foreach (var slot in (int[]) [Bow, Bridge, Hull, Stern])
        {
            if (CountInSlot(parts, slot) != 0)
                continue;

            for (var set = 0; set < Sets; set++)
                parts[PartId(set, slot) - 1] = true;
        }

        return parts;
    }

    public static int CountInSlot(bool[] parts, int slot)
    {
        var count = 0;
        for (var set = 0; set < Sets; set++)
            if (parts[PartId(set, slot) - 1])
                count++;

        return count;
    }

    /// <summary>null＝不限制。</summary>
    public static bool Allows(bool[]? parts, int hull, int stern, int bow, int bridge)
        => parts == null || (parts[hull - 1] && parts[stern - 1] && parts[bow - 1] && parts[bridge - 1]);

    public static string GetName(this LevelingObjective objective)
    {
        return objective switch
        {
            LevelingObjective.Default => Language.BuilderLevelingObjectiveDefault,
            LevelingObjective.ExpPerMinute => Language.BuilderLevelingObjectiveExpPerMinute,
            LevelingObjective.ExpPerVoyage => Language.BuilderLevelingObjectiveExpPerVoyage,
            _ => Language.TermUnknown
        };
    }
}

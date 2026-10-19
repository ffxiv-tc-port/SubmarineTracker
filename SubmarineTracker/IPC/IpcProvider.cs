using Dalamud.Plugin.Ipc;
using SubmarineTracker.Data;

namespace SubmarineTracker.IPC;

/// <summary>
/// 對外公開「物品 ↔ 航點」對照表，資料來源是隨外掛出貨的 ItemDetailed.msgpack。
/// 🔴 端點跑在呼叫端的執行緒上：這裡只讀 Importer 建好之後就不再變動的內容，
/// 不碰 ImGui，也不碰 Plugin.Configuration 這類可變狀態。
/// </summary>
/// <remarks>
/// 回傳的 List 每次都是新的複本，呼叫端改它不會動到本地快取。
/// 查無資料回空 List，不回 null 也不回可空型別 —— 消費端只要判 Count。
/// </remarks>
public class IpcProvider : IDisposable
{
    /// <summary>端點契約版本。回傳型別或語意改變時才 +1。</summary>
    public const int ApiVersion = 1;

    private const string ApiVersionTag = "SubmarineTracker.ApiVersion";
    private const string SectorsForItemTag = "SubmarineTracker.GetSectorsForItem";
    private const string ItemsForSectorTag = "SubmarineTracker.GetItemsForSector";

    // Lazy 預設是 ExecutionAndPublication：多個呼叫端同時打第一發也只會建一次索引。
    private static readonly Lazy<LookupIndex> Index = new(BuildIndex);

    private readonly ICallGateProvider<int> ApiVersionGate;
    private readonly ICallGateProvider<uint, List<uint>> SectorsForItemGate;
    private readonly ICallGateProvider<uint, List<uint>> ItemsForSectorGate;

    public IpcProvider()
    {
        // 在載入執行緒上先把 Importer 的型別初始式跑掉（它會讀兩個 msgpack 檔），
        // 免得第一個 IPC 呼叫在別人的執行緒上付這筆檔案 I/O。
        // Plugin 建構子尾端那行讀的是 const，編譯期就被內聯，碰不到型別初始式。
        _ = Importer.ItemDetailed;

        ApiVersionGate = Plugin.PluginInterface.GetIpcProvider<int>(ApiVersionTag);
        SectorsForItemGate = Plugin.PluginInterface.GetIpcProvider<uint, List<uint>>(SectorsForItemTag);
        ItemsForSectorGate = Plugin.PluginInterface.GetIpcProvider<uint, List<uint>>(ItemsForSectorTag);

        ApiVersionGate.RegisterFunc(() => ApiVersion);
        SectorsForItemGate.RegisterFunc(GetSectorsForItem);
        ItemsForSectorGate.RegisterFunc(GetItemsForSector);
    }

    public void Dispose()
    {
        ApiVersionGate.UnregisterFunc();
        SectorsForItemGate.UnregisterFunc();
        ItemsForSectorGate.UnregisterFunc();
    }

    /// <summary>這件物品會在哪幾個航點撈到，依 Tier 高到低排序。</summary>
    private static List<uint> GetSectorsForItem(uint itemId) =>
        Index.Value.SectorsByItem.TryGetValue(itemId, out var sectors) ? [..sectors] : [];

    /// <summary>這個航點會撈到哪些物品。順序是資料檔裡的原始順序。</summary>
    private static List<uint> GetItemsForSector(uint sector) =>
        Index.Value.ItemsBySector.TryGetValue(sector, out var items) ? [..items] : [];

    private sealed record LookupIndex(
        Dictionary<uint, uint[]> SectorsByItem,
        Dictionary<uint, uint[]> ItemsBySector);

    private static LookupIndex BuildIndex()
    {
        var sectorsByItem = new Dictionary<uint, uint[]>();
        var itemsBySector = new Dictionary<uint, List<uint>>();

        foreach (var (itemId, details) in Importer.ItemDetailed.Items)
        {
            // Tier 高→低；同 Tier 再依航點 id 遞增，讓同一份資料每次都給出同樣的順序。
            sectorsByItem[itemId] = details
                                    .OrderByDescending(detail => TierRank(detail.Tier))
                                    .ThenBy(detail => detail.Sector)
                                    .Select(detail => detail.Sector)
                                    .Distinct()
                                    .ToArray();

            foreach (var detail in details)
            {
                if (!itemsBySector.TryGetValue(detail.Sector, out var items))
                    itemsBySector[detail.Sector] = items = [];

                if (!items.Contains(itemId))
                    items.Add(itemId);
            }
        }

        return new LookupIndex(sectorsByItem, itemsBySector.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray()));
    }

    // Tier 是資料檔裡的字串（見 Importer.ToTier）。認不出來的排最後而不是擲例外：
    // 那份資料是外部產生的，多一個新等級不該讓整個端點壞掉。
    private static int TierRank(string tier) => tier switch
    {
        "T3" => 3,
        "T2" => 2,
        "T1" => 1,
        _ => 0
    };
}

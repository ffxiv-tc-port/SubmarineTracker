using SubmarineTracker.Resources;
using static SubmarineTracker.Data.Loot;

namespace SubmarineTracker.Windows.Loot;

public partial class LootWindow
{
    private static readonly int MaxLength = "Craftsman's Command Mat".Length;

    private uint SelectedSubmarine;
    private int SelectedVoyage;

    private void VoyageTab()
    {
        using var tabItem = ImRaii.TabItem($"{Language.LootTabHistory}##VoyageHistory");
        if (!tabItem.Success)
            return;

        Dictionary<uint, (string Title, ulong LocalId)> existingSubs = new();
        foreach (var (id, knownFC) in Plugin.DatabaseCache.GetFreeCompanies())
            foreach (var s in Plugin.DatabaseCache.GetSubmarines(id))
                existingSubs.Add(s.Register, ($"{Plugin.NameConverter.GetName(knownFC)}{s.Name} ({s.Build.FullIdentifier()})", id));

        if (existingSubs.Count == 0)
        {
            Helper.NoData();
            return;
        }

        var selectedSubmarine = SelectedSubmarine;
        if (!existingSubs.TryGetValue(SelectedSubmarine, out var preview))
            (selectedSubmarine, preview) = existingSubs.First();


        var selectedFC = preview.LocalId;
        using (var combo = ImRaii.Combo("##existingSubs", preview.Title))
        {
            if (combo.Success)
            {
                foreach (var (key, value) in existingSubs)
                    if (ImGui.Selectable($"{value.Title}##{key}"))
                        selectedSubmarine = key;
            }
        }
        Helper.DrawArrowsDictionary(ref selectedSubmarine, existingSubs.Keys.ToArray(), 1);

        if (SelectedSubmarine != selectedSubmarine)
        {
            SelectedSubmarine = selectedSubmarine;
            SelectedVoyage = 0;

            selectedFC = existingSubs[selectedSubmarine].LocalId;
        }

        var fc = Plugin.DatabaseCache.GetFreeCompanies()[selectedFC];
        var sub = Plugin.DatabaseCache.GetSubmarines().First(sub => sub.Register == SelectedSubmarine);

        var submarineLoot = Plugin.DatabaseCache.GetLoot().Where(l => l.FreeCompanyId == fc.FreeCompanyId).Where(l => l.Register == sub.Register).ToArray();
        if (submarineLoot.Length == 0)
        {
            Helper.TextColored(ImGuiColors.ParsedOrange, Language.LootTabHistoryWrong);
            return;
        }

        var dict = new Dictionary<uint, List<SubmarineTracker.Loot>>();
        foreach (var l in submarineLoot.Where(loot => !Plugin.Configuration.ExcludeLegacy || loot.Valid))
            if (!dict.TryAdd(l.Return, [l]))
                dict[l.Return].Add(l);

        var lootHistory = dict.OrderByDescending(pair => pair.Key).Select(pair => pair.Value).ToArray();
        if (lootHistory.Length == 0)
        {
            Helper.TextColored(ImGuiColors.ParsedOrange, Language.LootTabHistoryNotTracked);
            return;
        }

        Helper.ClippedCombo("##voyageSelection", ref SelectedVoyage, lootHistory, entry => $"{entry[0].Date}");
        Helper.DrawArrows(ref SelectedVoyage, lootHistory.Length, 2);

        ImGuiHelpers.ScaledDummy(5.0f);

        var loot = lootHistory[SelectedVoyage];
        var stats = loot[0];
        if (stats.Valid)
            Helper.TextColored(ImGuiColors.TankBlue, $"{Language.TermsRank}: {stats.Rank} SRF: {stats.Surv}, {stats.Ret}, {stats.Fav}");
        else
            Helper.TextColored(ImGuiColors.ParsedOrange, Language.LootTabHistoryLegacyData);

        ImGuiHelpers.ScaledDummy(5.0f);

        foreach (var detailedLoot in loot)
        {
            // Primary / Additional 與底下的 Sector 是同一份 SQLite 歷史紀錄、同一個問題：
            // 物品 id 也是持久化值，裸 GetRow 查不到就擲例外，整個視窗會消失。
            // 這裡刻意不動 Sheets.GetItem 的簽名（全 repo 大量使用，改了會擴散），
            // 改在呼叫點驗，退路與 Sector 一致：顯示「未知 (id)」。
            var primaryItem = Sheets.ItemSheet.GetRowOrDefault(detailedLoot.Primary);
            var additionalItem = Sheets.ItemSheet.GetRowOrDefault(detailedLoot.Additional);

            // Sector 是從 SQLite 歷史紀錄回讀的持久化值，不保證存在於本地資料表。
            // 這裡在 Draw 路徑上，裸 GetRow 查不到就擲例外，整個視窗會消失。
            var sectorRow = Sheets.ExplorationSheet.GetRowOrDefault(detailedLoot.Sector);
            Helper.TextColored(ImGuiColors.HealerGreen, sectorRow == null ? $"{Language.TermsUnknown} ({detailedLoot.Sector})" : sectorRow.Value.ToName());
            using var indent = ImRaii.PushIndent(10.0f);
            if (stats.Valid)
                ImGui.TextUnformatted($"DD: {ProcToText(detailedLoot.FavProc)} --- Ret: {ProcToText(detailedLoot.PrimaryRetProc)}");

            using var table = ImRaii.Table("##VoyageLootTable", 4);
            if (!table.Success)
                return;

            ImGui.TableSetupColumn("##icon", ImGuiTableColumnFlags.WidthStretch, 0.2f);
            ImGui.TableSetupColumn("##item");
            ImGui.TableSetupColumn("##amount", ImGuiTableColumnFlags.WidthStretch, 0.2f);
            ImGui.TableSetupColumn("##survProc", ImGuiTableColumnFlags.WidthStretch, 0.4f);

            ImGui.TableNextColumn();
            if (primaryItem != null)
                Helper.DrawScaledIcon(primaryItem.Value.Icon, IconSize);

            var name = primaryItem != null ? primaryItem.Value.Name.ExtractText() : $"{Language.TermsUnknown} ({detailedLoot.Primary})";
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(name.Truncate(MaxLength));
            if (ImGui.IsItemHovered())
                Helper.Tooltip(name);

            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"x{detailedLoot.PrimaryCount}");

            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{(stats.Valid ? ProcToText(detailedLoot.PrimarySurvProc) : "")}");

            ImGui.TableNextRow();

            if (detailedLoot.ValidAdditional)
            {
                ImGui.TableNextColumn();
                if (additionalItem != null)
                    Helper.DrawScaledIcon(additionalItem.Value.Icon, IconSize);

                name = additionalItem != null ? additionalItem.Value.Name.ExtractText() : $"{Language.TermsUnknown} ({detailedLoot.Additional})";
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(name.Truncate(MaxLength));
                if (ImGui.IsItemHovered())
                    Helper.Tooltip(name);

                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"x{detailedLoot.AdditionalCount}");

                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"{(stats.Valid ? ProcToText(detailedLoot.AdditionalSurvProc) : "")}");

                ImGui.TableNextRow();
            }
        }
    }
}

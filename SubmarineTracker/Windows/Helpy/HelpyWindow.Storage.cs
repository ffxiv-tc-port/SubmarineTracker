using SubmarineTracker.Data;
using SubmarineTracker.Resources;

namespace SubmarineTracker.Windows.Helpy;

public partial class HelpyWindow
{
    private static readonly Vector2 IconSize = new(32, 32);

    private void StorageTab()
    {
        using var tabItem = ImRaii.TabItem($"{Language.HelpyTabStorage}##Storage");
        if (!tabItem.Success)
            return;

        ImGuiHelpers.ScaledDummy(5.0f);
        if (!Plugin.AllaganToolsConsumer.IsAvailable)
        {
            Helper.TextColored(ImGuiColors.ParsedOrange, Language.HelpyTabWarningAllaganTools);
            return;
        }

        // build cache if needed
        Storage.BuildStorageCache();

        foreach (var (key, fc) in Plugin.DatabaseCache.GetFreeCompanies())
        {
            // 快取只在第一次用到與手動同步時重建；之後才記錄到的部隊不在裡面，同步前先略過。
            if (!Storage.StorageCache.TryGetValue(key, out var cachedItems))
                continue;

            Helper.TextColored(ImGuiColors.DalamudViolet, $"{Plugin.NameConverter.GetName(fc)}:");

            using var indent = ImRaii.PushIndent(10.0f);
            using var table = ImRaii.Table($"##SubmarineOverview{key}", 3);
            if (!table.Success)
                continue;

            ImGui.TableSetupColumn("##icon", ImGuiTableColumnFlags.WidthStretch, 0.1f);
            ImGui.TableSetupColumn("##count", ImGuiTableColumnFlags.WidthStretch, 0.15f);
            ImGui.TableSetupColumn("##item");

            foreach (var cached in cachedItems.Values)
            {
                ImGui.TableNextColumn();
                Helper.DrawScaledIcon(cached.Item.Icon, IconSize);

                var count = $"{cached.Count}x";
                ImGui.TableNextColumn();
                ImGui.SameLine(ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize(count).X);
                ImGui.TextUnformatted(count);

                ImGui.TableNextColumn();
                ImGui.TextUnformatted(cached.Item.Name.ExtractText());

                ImGui.TableNextRow();
            }

            ImGuiHelpers.ScaledDummy(10.0f);
        }
    }
}

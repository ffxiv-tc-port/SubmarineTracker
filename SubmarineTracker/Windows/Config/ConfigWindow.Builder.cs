using Dalamud.Interface;
using Dalamud.Interface.Components;
using SubmarineTracker.Resources;

namespace SubmarineTracker.Windows.Config;

public partial class ConfigWindow
{
    private void Builder()
    {
        using var tabItem = ImRaii.TabItem($"{Language.BuilderTabBuilder}##Builder");
        if (!tabItem.Success)
            return;

        ImGuiHelpers.ScaledDummy(5.0f);
        var changed = false;

        Helper.TextColored(ImGuiColors.DalamudViolet, Language.ConfigTabEntryOptions);
        using (ImRaii.PushIndent(10.0f))
        {
            changed |= ImGui.Checkbox(Language.ConfigTabCheckboxCurrentFC, ref Plugin.Configuration.ShowOnlyCurrentFC);
            changed |= ImGui.Checkbox(Language.ConfigTabCheckboxAutoSelection, ref Plugin.Configuration.AutoSelectCurrent);
            ImGuiComponents.HelpMarker(Language.ConfigTabTooltipAutoSelection);
            if (Plugin.Configuration.AutoSelectCurrent)
            {
                using var indent = ImRaii.PushIndent(10.0f);
                changed |= ImGui.Checkbox(Language.ConfigTabCheckboxNextOverlay, ref Plugin.Configuration.ShowNextOverlay);
                ImGuiComponents.HelpMarker(Language.ConfigTabTooltipNextOverlay);
                changed |= ImGui.Checkbox(Language.ConfigTabCheckboxUnlockOverlay, ref Plugin.Configuration.ShowUnlockOverlay);
                ImGuiComponents.HelpMarker(Language.ConfigTabTooltipUnlockOverlay);
                changed |= ImGui.Checkbox(Language.ConfigTabCheckboxRouteOverlay, ref Plugin.Configuration.ShowRouteOverlay);
                ImGuiComponents.HelpMarker(Language.ConfigTabTooltipRouteOverlay);
                // 這裡的 changed 最後會通往 Configuration.Save()。滑桿在拖曳中每畫格都可能
                // 回傳 true，直接 |= 進去會以幀率同步寫磁碟；改成只在放開滑鼠時計一次。
                // HighestLevel 本身仍是即時更新，畫面不受影響。
                ImGui.SliderInt(Language.ConfigTabSliderRank, ref Plugin.Configuration.HighestLevel, 1, (int)Sheets.LastRank, $"{Language.TermsRank} %d");
                changed |= ImGui.IsItemDeactivatedAfterEdit();
                ImGuiComponents.HelpMarker(Language.ConfigTabTooltipRank);
                changed |= ImGui.Checkbox(Language.ConfigTabCheckboxIncludeMain, ref Plugin.Configuration.MainRouteAutoInclude);
                ImGuiComponents.HelpMarker(Language.ConfigTabTooltipIncludeMain);
            }
        }

        ImGuiHelpers.ScaledDummy(5.0f);

        Helper.TextColored(ImGuiColors.DalamudViolet, Language.ConfigTabEntrySavedBuilds);
        using var table = ImRaii.Table("##DeleteBuildsTable", 2);
        if (table.Success)
        {
            ImGui.TableSetupColumn(Language.TermsBuild);
            ImGui.TableSetupColumn("##Del", ImGuiTableColumnFlags.WidthStretch, 0.1f);

            ImGui.TableHeadersRow();
            var deletion = string.Empty;
            foreach (var (key, build) in Plugin.Configuration.SavedBuilds)
            {
                ImGui.TableNextColumn();
                var text = Utils.FormattedRouteBuild(key, build).Split("\n");
                Helper.TextWrapped(text.First());
                Helper.TextColored(ImGuiColors.ParsedOrange, text.Last());

                ImGui.TableNextColumn();
                if (ImGuiComponents.IconButton(key, FontAwesomeIcon.Trash))
                    deletion = key;

                ImGui.TableNextRow();
            }

            if (deletion != string.Empty)
            {
                Plugin.Configuration.SavedBuilds.Remove(deletion);
                Plugin.Configuration.Save();
            }
        }

        if (changed)
        {
            Plugin.BuilderWindow.VoyageInterfaceSelection = 0;
            Plugin.Configuration.Save();
        }
    }
}

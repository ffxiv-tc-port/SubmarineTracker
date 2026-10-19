using SubmarineTracker.Data;
using SubmarineTracker.Resources;

namespace SubmarineTracker.Windows.Builder;

public partial class BuilderWindow
{
    private static readonly int[] PartPoolColumns = [PartPool.Hull, PartPool.Stern, PartPool.Bow, PartPool.Bridge];
    private static string[]? PartPoolNames;

    private bool AvailablePartsTab()
    {
        using var tabItem = ImRaii.TabItem($"{Language.BuilderTabAvailableParts}##AvailableParts");
        if (!tabItem.Success)
            return false;

        var config = Plugin.Configuration;
        var parts = PartPool.Sanitize(config);
        var changed = false;

        Helper.TextWrapped(Language.BuilderAvailablePartsInfo);
        ImGuiHelpers.ScaledDummy(5.0f);

        if (ImGui.Button(Language.BuilderAvailablePartsButtonAll))
        {
            Array.Fill(parts, true);
            changed = true;
        }

        ImGui.SameLine();
        if (ImGui.Button(Language.BuilderAvailablePartsButtonNone))
        {
            Array.Fill(parts, false);
            foreach (var slot in PartPoolColumns)
                parts[PartPool.PartId(0, slot) - 1] = true;
            changed = true;
        }

        ImGui.SameLine();
        if (ImGui.Button(Language.BuilderAvailablePartsButtonNoModified))
        {
            foreach (var slot in PartPoolColumns)
            {
                // 這個部位只勾了改級時不動它，免得整個部位變成一格都沒有。
                var anyUnmodified = false;
                for (var set = 0; set < 5; set++)
                    anyUnmodified |= parts[PartPool.PartId(set, slot) - 1];
                if (!anyUnmodified)
                    continue;

                for (var set = 5; set < PartPool.Sets; set++)
                    parts[PartPool.PartId(set, slot) - 1] = false;
            }
            changed = true;
        }

        ImGuiHelpers.ScaledDummy(5.0f);
        using (var table = ImRaii.Table("##availablePartsTable", PartPoolColumns.Length, ImGuiTableFlags.Borders | ImGuiTableFlags.SizingStretchSame))
        {
            if (table.Success)
            {
                foreach (var slot in PartPoolColumns)
                    ImGui.TableSetupColumn(SlotName(slot));
                ImGui.TableHeadersRow();

                var names = GetPartPoolNames();
                for (var set = 0; set < PartPool.Sets; set++)
                {
                    ImGui.TableNextRow();
                    foreach (var slot in PartPoolColumns)
                    {
                        ImGui.TableNextColumn();
                        var id = PartPool.PartId(set, slot);
                        var enabled = parts[id - 1];
                        var lastInSlot = enabled && PartPool.CountInSlot(parts, slot) == 1;
                        using (ImRaii.Disabled(lastInSlot))
                        {
                            if (ImGui.Checkbox($"{names[id - 1]}##availablePart{id}", ref enabled))
                            {
                                parts[id - 1] = enabled;
                                changed = true;
                            }
                        }

                        if (lastInSlot && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                            Helper.Tooltip(Language.BuilderAvailablePartsTooltipLastPart);
                    }
                }
            }
        }

        if (changed)
        {
            config.Save();
            if (config.RestrictShipSolverParts)
                ApplyShipPartPool();
        }

        return true;
    }

    private static string SlotName(int slot) => slot switch
    {
        PartPool.Hull => Language.TermsHull,
        PartPool.Stern => Language.TermsStern,
        PartPool.Bow => Language.TermsBow,
        _ => Language.TermsBridge
    };

    private static string[] GetPartPoolNames()
    {
        if (PartPoolNames != null)
            return PartPoolNames;

        var names = new string[PartPool.Count];
        for (var id = 1; id <= PartPool.Count; id++)
            names[id - 1] = Submarines.PartIdToItemId.TryGetValue((ushort) id, out var itemId)
                                ? Sheets.GetItem(itemId).Name.ExtractText()
                                : $"#{id}";

        return PartPoolNames = names;
    }
}

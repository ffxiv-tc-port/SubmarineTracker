using Dalamud.Interface;
using Lumina.Excel.Sheets;
using SubmarineTracker.Data;
using SubmarineTracker.Resources;
using static SubmarineTracker.Utils;

namespace SubmarineTracker.Windows.Builder;

public partial class BuilderWindow
{
    private uint CurrentSearchSelection;
    public ExcelSheetSelector<Item>.ExcelSheetPopupOptions? SearchPopupOptions;

    private bool LootTab()
    {
        using var tabItem = ImRaii.TabItem($"{Language.LootTabLoot}##Loot");
        if (!tabItem.Success)
            return false;

        ImGuiHelpers.ScaledDummy(5.0f);

        var width = ImGui.GetContentRegionAvail().X / 3;
        ImGui.AlignTextToFramePadding();
        Helper.TextColored(ImGuiColors.DalamudViolet, Language.TermSearch);
        ImGui.SameLine();
        using (ImRaii.PushFont(UiBuilder.IconFont))
            ImGui.Button(FontAwesomeIcon.Search.ToIconString(), new Vector2(width, 0));

        SearchPopupOptions ??= new ExcelSheetSelector<Item>.ExcelSheetPopupOptions
        {
            CloseOnSelection = true,
            FormatRow = a => a.RowId switch { _ => $"[#{a.RowId}] {a.Name.ExtractText()}" },
            FilteredSheet = Sheets.ItemSheet.Where(i => Importer.ItemDetailed.Items.ContainsKey(i.RowId)),
        };

        if (ExcelSheetSelector<Item>.ExcelSheetPopup("BuilderSearchAddPopup", out var row, SearchPopupOptions))
            CurrentSearchSelection = row;

        ImGuiHelpers.ScaledDummy(10.0f);

        if (CurrentSearchSelection == 0)
        {
            ImGui.TextUnformatted(Language.TabLootNoSearch);
            return true;
        }

        var item = Sheets.GetItem(CurrentSearchSelection);
        Helper.IconHeader(item.Icon, new Vector2(32, 32), item.Name.ExtractText(), ImGuiColors.ParsedOrange);

        using var table = ImRaii.Table("##searchColumn", 5, ImGuiTableFlags.BordersInner | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp);
        if (table.Success)
        {
            ImGui.TableSetupColumn(Language.TermsSector);
            ImGui.TableSetupColumn(Language.TermsTier);
            ImGui.TableSetupColumn(Language.RetTermPoor);
            ImGui.TableSetupColumn(Language.RetTermNormal);
            ImGui.TableSetupColumn(Language.RetTermOptimal);

            ImGui.TableHeadersRow();
            foreach (var itemDetail in Importer.ItemDetailed.Items[item.RowId])
            {
                // Sector 來自捆綁的外部資料檔（msgpack），id 不保證存在於本地資料表。
                // 這裡在 Draw 路徑上，裸 GetRow 查不到就擲例外，整個視窗會消失。
                var subRow = Sheets.ExplorationSheet.GetRowOrDefault(itemDetail.Sector);

                ImGui.TableNextColumn();
                if (subRow == null)
                    ImGui.TextUnformatted($"{Language.TermsUnknown} ({itemDetail.Sector})");
                else
                    ImGui.TextUnformatted($"{UpperCaseStr(subRow.Value.Destination)} ({NumToLetter(subRow.Value.RowId, true)} - {MapToThreeLetter(subRow.Value.RowId, true)})");

                ImGui.TableNextColumn();
                Helper.CenterText($"{itemDetail.Tier}");

                ImGui.TableNextColumn();
                Helper.CenterText($"{itemDetail.Poor}");

                ImGui.TableNextColumn();
                Helper.CenterText($"{itemDetail.Normal}");

                ImGui.TableNextColumn();
                Helper.CenterText($"{itemDetail.Optimal}");
            }
        }

        return true;
    }
}

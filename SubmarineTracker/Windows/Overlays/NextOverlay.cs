using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SubmarineTracker.Data;
using SubmarineTracker.Resources;
using static SubmarineTracker.Utils;

namespace SubmarineTracker.Windows.Overlays;

public class NextOverlay : Window, IDisposable
{
    private readonly Plugin Plugin;

    private readonly List<(uint, Unlocks.UnlockedFrom)> UnlockPath;
    private (uint Sector, Unlocks.UnlockedFrom UnlockedFrom)? NextSector;

    // 只在數值變動時記一次，避免在繪製路徑上每幀洗版。
    private uint LastReportedBadSector = uint.MaxValue;

    private ImRaii.Color PushedColor = null!;

    public NextOverlay(Plugin plugin) : base($"{Language.WindowTitleNextOverlay}##SubmarineTracker")
    {
        Size = new Vector2(300, 60);

        Flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove;
        RespectCloseHotkey = false;
        DisableWindowSounds = true;
        ForceMainWindow = true;

        Plugin = plugin;

        UnlockPath = Unlocks.FindUnlockPath(Unlocks.SectorToUnlock.Last(s => s.Value.Sector != SectorType.UnknownUnlock).Key);
        UnlockPath.Reverse();
    }

    public void Dispose() { }

    public override unsafe void PreOpenCheck()
    {
        IsOpen = false;
        if (!Plugin.Configuration.AutoSelectCurrent || !Plugin.Configuration.ShowNextOverlay)
            return;

        // Always refresh submarine if we have interface selection
        Plugin.BuilderWindow.RefreshCache();
        try
        {
            var agent = AgentSubmersibleExploration.Instance();
            if (agent == null || agent->MapId == 0)
                return;

            var addonPtr = Plugin.GameGui.GetAddonByName("AirShipExploration");
            if (addonPtr == nint.Zero)
                return;

            Position = new Vector2(addonPtr.X + 5, addonPtr.Y - (Size!.Value.Y * ImGuiHelpers.GlobalScale));
            PositionCondition = ImGuiCond.Always;

            NextSector = null;
            var fcSub = Plugin.DatabaseCache.GetFreeCompanies()[Plugin.GetFCId];
            foreach (var (sector, unlockedFrom) in UnlockPath)
            {
                fcSub.UnlockedSectors.TryGetValue(sector, out var hasUnlocked);
                if (hasUnlocked)
                    continue;

                NextSector = (sector, unlockedFrom);
                break;
            }

            if (!NextSector.HasValue || Voyage.FindMapFromSector((uint)NextSector.Value.UnlockedFrom.Sector) != agent->MapId)
                return;

            IsOpen = true;
        }
        catch
        {
            // Something went wrong, we don't draw
        }
    }

    public override void PreDraw()
    {
        PushedColor = ImRaii.PushColor(ImGuiCol.WindowBg, Helper.TransparentBackground);
    }

    public override void Draw()
    {
        if (!NextSector.HasValue)
            return;

        var nextSector = NextSector.Value;

        // UnlockedFrom.Sector 可能是哨兵值（Begin 9000 / UnknownUnlock 9876 / Map 9999）而不是真正的
        // SubmarineExploration 列 —— UnlockPath 反轉後的第一筆就是 (海域 2, Begin)。PreOpenCheck 的
        // Voyage.FindMapFromSector() 擋不住它：FindVoyageStart() 對任何超出範圍的值都會退回「最大的
        // 起始點」，所以會靜默回傳一張合法的地圖而不是拋例外。
        // GetRow() 在查無此列時擲 ArgumentOutOfRangeException，而這裡位於 ImGui 繪製路徑上 ——
        // 擲一次就會讓 UiBuilder 把 Draw/OpenConfigUi 設為 null，整個外掛的介面到重開遊戲前都不會回來。
        var fromSector = (uint)nextSector.UnlockedFrom.Sector;
        if (!Sheets.ExplorationSheet.TryGetRow(nextSector.Sector, out var nextUnlock) ||
            !Sheets.ExplorationSheet.TryGetRow(fromSector, out var unlockedFrom))
        {
            if (LastReportedBadSector != fromSector)
            {
                LastReportedBadSector = fromSector;
                Plugin.Log.Information($"NextOverlay: 查無海域列（目標 {nextSector.Sector}／解鎖來源 {fromSector}），本次不繪製。解鎖來源 >= 9000 代表它是哨兵值而非真實海域。");
            }

            return;
        }

        LastReportedBadSector = uint.MaxValue;
        if (unlockedFrom.RankReq > Plugin.BuilderWindow.CurrentBuild.Rank)
        {
            if (ImGui.IsWindowHovered())
                Helper.Tooltip(Language.NextOverlayTooltipLowRank);

            return;
        }

        var isMap = false;
        if (Unlocks.SectorToUnlock.TryGetValue((uint)nextSector.UnlockedFrom.Sector, out var previousSector))
            isMap = previousSector.Map;

        var unlockText = $"{Language.NextOverlayTextNextSector} {NumToLetter(nextUnlock.RowId, true)}. {UpperCaseStr(nextUnlock.Destination)}";
        var visitText = $"{Language.NextOverlayTextVisit} {NumToLetter(unlockedFrom.RowId, true)}. {UpperCaseStr(unlockedFrom.Destination)}";
        if (isMap)
        {
            unlockText = $"{Language.NextOverlayTextNextMap} {MapToShort(nextUnlock.RowId, true)}";
            visitText = $"{Language.NextOverlayTextVisit} {NumToLetter(unlockedFrom.RowId, true)}. {UpperCaseStr(unlockedFrom.Destination)}";
        }

        var avail = ImGui.GetWindowSize().X;
        var textWidth1 = ImGui.CalcTextSize(unlockText).X;
        var textWidth2 = ImGui.CalcTextSize(visitText).X;

        ImGui.SetCursorPosX((avail - textWidth1) * 0.5f);
        Helper.TextColored(ImGuiColors.DalamudOrange, unlockText);

        ImGui.SetCursorPosX((avail - textWidth2) * 0.5f);
        Helper.TextColored(ImGuiColors.HealerGreen, visitText);

        if (Plugin.Configuration.MainRouteAutoInclude && Plugin.RouteOverlay.MustInclude.Add(unlockedFrom))
            Plugin.RouteOverlay.Calculate = true;
    }

    public override void PostDraw()
    {
        PushedColor.Dispose();
    }
}

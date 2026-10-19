using System.IO;
using Dalamud.Interface.Windowing;
using SubmarineTracker.Resources;

namespace SubmarineTracker.Windows.Overlays;

// Inspired by Accountant from Ottermandias
public class ReturnOverlay : Window, IDisposable
{
    private readonly Plugin Plugin;

    private long LastRefresh;
    private Submarine NextSub = new();
    private (int OnRoute, int Done) VoyageStats = (0, 0);

    // 自動展開／摺疊：每個公會上次看到的狀態（true 全部返回、false 全部出航、null 部分返回），只在狀態改變時動標頭。
    private readonly Dictionary<ulong, bool?> FcReturnStates = new();

    private ImRaii.Color PushedColor = null!;

    public ReturnOverlay(Plugin plugin) : base("Submarines: 0|0###submarineOverlay")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(300, 140),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        RespectCloseHotkey = false;
        DisableWindowSounds = true;

        Plugin = plugin;
    }

    public void Dispose() { }

    public override void Update()
    {
        Flags = (Plugin.Configuration.OverlayLockLocation ? ImGuiWindowFlags.NoMove : 0) | (Plugin.Configuration.OverlayLockSize ? ImGuiWindowFlags.NoResize : 0);
    }

    public override void PreOpenCheck()
    {
        if (Plugin.Configuration.OverlayHoldClosed && !Plugin.DatabaseCache.GetSubmarines().Any(sub => sub.IsDone()))
            IsOpen = false;

        // Values are shared between this window and server bar, so always calculate it even with a closed window
        // Only calculate it once a second
        if (Environment.TickCount64 < LastRefresh)
            return;

        LastRefresh = Environment.TickCount64 + 1000; // 1s

        VoyageStats = (0, 0);
        NextSub = new Submarine(Plugin.Configuration.OverlayFirstReturn ? uint.MaxValue : 0);
        foreach (var sub in Plugin.DatabaseCache.GetSubmarines())
        {
            if (Plugin.Configuration.OverlayNoHidden && Plugin.IsFCHidden(sub.FreeCompanyId))
                continue;

            if (sub.IsDone())
            {
                VoyageStats.Done += 1;
            }
            else
            {
                VoyageStats.OnRoute += 1;
                if (!Plugin.Configuration.OverlayTitleTime)
                    continue;

                if (Plugin.Configuration.OverlayFirstReturn)
                {
                    if (NextSub.Return > sub.Return)
                        NextSub = sub;
                }
                else
                {
                    if (NextSub.Return < sub.Return)
                        NextSub = sub;
                }
            }
        }
    }

    public override void PreDraw()
    {
        base.PreDraw();

        var returnText = "";
        if (Plugin.Configuration.OverlayTitleTime)
        {
            if (Plugin.Configuration.OverlayFirstReturn)
            {
                if (NextSub.Return < uint.MaxValue)
                    returnText = $"   -   {(NextSub.IsDone() ? Language.TermsDone : Utils.ToTime(NextSub.LeftoverTime()))}";
            }
            else
            {
                if (NextSub.Return > 0)
                    returnText = $"   -   {Utils.ToTime(NextSub.LeftoverTime())}";
            }
        }

        WindowName = $"{Language.TermsSubmarines}: {OverlayNumbers()}{returnText}###submarineOverlay";

        PushedColor = ImRaii.PushColor(ImGuiCol.WindowBg, Helper.TransparentBackground);
    }

    public override void Draw()
    {
        if (!Plugin.Configuration.OverlayAutoExpandCollapseFCs)
            FcReturnStates.Clear();

        var showLast = !Plugin.Configuration.OverlayFirstReturn;

        Submarine? timerSub = null;
        var anySubs = false;
        foreach (var fc in Plugin.DatabaseCache.GetFreeCompanies().Keys)
        {
            var subs = Plugin.DatabaseCache.GetSubmarines(fc);
            var timer = showLast ? subs.MaxBy(s => s.Return) : subs.MinBy(s => s.Return);
            if (timer == null)
                continue;

            // 「全部」標頭彙總的是下面那份不含隱藏公會的清單，時間也不算它們；全部隱藏時照畫標頭、不畫時間。
            anySubs = true;
            if (Plugin.IsFCHidden(fc))
                continue;

            if (timerSub == null || (showLast ? timer.ReturnTime > timerSub.ReturnTime : timer.ReturnTime < timerSub.ReturnTime))
                timerSub = timer;
        }

        if (!anySubs)
            return;

        var y = ImGui.GetCursorPosY();
        var windowWidth = ImGui.GetContentRegionAvail().X;

        var color = VoyageStats is { Done: > 0, OnRoute: > 0 }
                        ? Plugin.Configuration.OverlayPartlyDone : VoyageStats.OnRoute == 0
                            ? Plugin.Configuration.OverlayAllDone : Plugin.Configuration.OverlayNoneDone;
        bool mainHeader;
        using (ImRaii.PushColor(ImGuiCol.Header, color))
            mainHeader = ImGui.CollapsingHeader("All###overlayAll", ImGuiTreeNodeFlags.DefaultOpen);

        if (timerSub != null)
            SetHeaderText(timerSub, windowWidth, y);

        if (!mainHeader)
            return;

        Plugin.EnsureFCOrderSafety();
        var fcList = Plugin.GetFCOrderWithoutHidden().Select(id => (Plugin.DatabaseCache.GetFreeCompanies()[id], Plugin.DatabaseCache.GetSubmarines(id))).Where(tuple => tuple.Item2.Length != 0);
        if (Plugin.Configuration.OverlaySortReverse)
            fcList = fcList.OrderByDescending(tuple => tuple.Item2.Min(s => s.Return));
        else if (Plugin.Configuration.OverlaySort)
            fcList = fcList.OrderBy(tuple => tuple.Item2.Min(s => s.Return));

        if (Plugin.Configuration.OverlayOnlyReturned)
            fcList = fcList.Where(tuple => tuple.Item2.Any(s => s.IsDone()));


        var sortedFcList = fcList.ToArray();
        if (sortedFcList.Length == 0)
        {
            using var indent = ImRaii.PushIndent(10.0f);
            Helper.TextColored(ImGuiColors.DalamudOrange, Language.ReturnOverlayInfoNoReturn);
            return;
        }

        using var outerIndent = ImRaii.PushIndent(10.0f);
        foreach (var (fc, subs) in sortedFcList)
        {
            y = ImGui.GetCursorPosY();
            var anySubDone = subs.Any(s => s.IsDone());
            var longestSub = showLast ? subs.MaxBy(s => s.Return) : subs.MinBy(s => s.Return);

            if (longestSub == null)
                continue;

            SetFcHeaderState(fc.FreeCompanyId, subs);

            bool header;
            using (ImRaii.PushColor(ImGuiCol.Header, longestSub.IsDone() ? Plugin.Configuration.OverlayAllDone : anySubDone ? Plugin.Configuration.OverlayPartlyDone : Plugin.Configuration.OverlayNoneDone))
                header = ImGui.CollapsingHeader($"{Plugin.NameConverter.GetName(fc)}###overlayFC{fc.FreeCompanyId}");

            SetHeaderText(longestSub, windowWidth, y);

            if (!header)
                continue;

            using var innerIndent = ImRaii.PushIndent(10.0f);
            foreach (var sub in subs)
            {
                var needsRepair = sub.PredictDurability() <= 0;
                var subText = $"{(Plugin.Configuration.OverlayShowRank ? $"{Language.TermsRank} {sub.Rank}. " : "")}{Plugin.NameConverter.GetJustSub(sub)}{(Plugin.Configuration.OverlayShowBuild ? $" ({sub.Build.FullIdentifier()})" : "")}";
                Helper.TextColored(!needsRepair ? ImGuiColors.TankBlue : ImGuiColors.DalamudYellow, subText);

                if (needsRepair && ImGui.IsItemHovered())
                    Helper.Tooltip(Language.ReturnOverlayTooltipRepairNeeded);

                var timeText = Helper.GenerateVoyageText(sub, !Plugin.Configuration.OverlayShowDate);
                var timeWidth = ImGui.CalcTextSize(timeText).X;
                ImGui.SameLine(windowWidth - timeWidth);
                ImGui.TextUnformatted(timeText);

            }
            ImGui.Columns(1);
        }
    }

    private void SetFcHeaderState(ulong fcId, Submarine[] subs)
    {
        if (!Plugin.Configuration.OverlayAutoExpandCollapseFCs)
            return;

        var done = 0;
        foreach (var sub in subs)
            if (sub.IsDone())
                done++;

        bool? state = done == subs.Length ? true : done == 0 ? false : null;
        if (FcReturnStates.TryGetValue(fcId, out var previous) && previous == state)
            return;

        FcReturnStates[fcId] = state;
        if (state.HasValue)
            ImGui.SetNextItemOpen(state.Value, ImGuiCond.Always);
    }

    public override void OnOpen()
    {
        try
        {
            Plugin.Configuration.OverlayOpen = true;
            Plugin.Configuration.Save();
        }
        catch (IOException)
        {
            // We just ignore
        }
    }

    public override void OnClose()
    {
        try
        {
            Plugin.Configuration.OverlayOpen = false;
            Plugin.Configuration.Save();
        }
        catch (IOException)
        {
            // We just ignore
        }
    }

    public override void PostDraw()
    {
        PushedColor.Dispose();

        base.PostDraw();
    }

    public static void SetHeaderText(Submarine sub, float windowWidth, float lastY)
    {
        var cursorPos = ImGui.GetCursorPos();
        var longestText = Helper.GenerateVoyageText(sub, !Plugin.Configuration.OverlayShowDate);
        var longestWidth = ImGui.CalcTextSize(longestText).X;
        ImGui.SetCursorPos(new Vector2(windowWidth - longestWidth, lastY));
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(longestText);
        ImGui.SetCursorPos(cursorPos);
    }

    // 資訊列拿來判斷數字有沒有變，免得每幀先組出字串才比。
    internal (int OnRoute, int Done) CurrentVoyageStats => VoyageStats;

    public string OverlayNumbers()
    {
        // DTR 空間很擠：用 1/27 取代 1 | 27，語意相同但省掉兩個空白與分隔線。
        return $"{VoyageStats.Done}/{VoyageStats.OnRoute}";
    }
}

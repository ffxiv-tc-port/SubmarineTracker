using System.Text;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Plugin.Services;
using SubmarineTracker.Data;
using SubmarineTracker.Resources;

namespace SubmarineTracker;

public class ServerBar
{
    private readonly Plugin Plugin;
    private readonly IDtrBarEntry? DtrEntry;

    public ServerBar(Plugin plugin)
    {
        Plugin = plugin;

        if (Plugin.DtrBar.Get("SubmarineTracker") is not { } entry)
            return;

        DtrEntry = entry;

        DtrEntry.Text = "Submarines are cool...";
        DtrEntry.Shown = false;
        DtrEntry.OnClick += OnClick;

        Plugin.Framework.Update += UpdateDtrBar;
    }

    public void Dispose()
    {
        if (DtrEntry == null)
            return;

        Plugin.Framework.Update -= UpdateDtrBar;
        DtrEntry.OnClick -= OnClick;
        DtrEntry.Remove();
    }

    public void UpdateDtrBar(IFramework framework)
    {
        if (!Plugin.Configuration.ShowDtrEntry && !Plugin.Configuration.DtrShowOverlayNumbers)
        {
            UpdateVisibility(false);
            return;
        }

        UpdateVisibility(true);
        UpdateBarString();
    }

    // ShowDtrEntry 關著時（預設）文字只由這組值決定：沒變就不再重建與設定（每幀重建要組數段字串、StringBuilder 與 SeString，
    // Dalamud 端也得每幀重新編碼比對）。ShowDtrEntry 開著時含倒數計時，每幀重建，並讓下一次關著時一定重建。
    private (bool Numbers, int OnRoute, int Done, bool Slots, int SlotsFree)? LastBarKey;

    private void UpdateBarString()
    {
        (bool Numbers, int OnRoute, int Done, bool Slots, int SlotsFree)? key = null;
        if (!Plugin.Configuration.ShowDtrEntry)
        {
            var showNumbers = Plugin.Configuration.DtrShowOverlayNumbers;
            var stats = showNumbers ? Plugin.ReturnOverlay.CurrentVoyageStats : default;
            var showSlots = Plugin.Configuration.DtrShowInventorySlots && Storage.InventorySlotsFree > -1;
            key = (showNumbers, stats.OnRoute, stats.Done, showSlots, showSlots ? Storage.InventorySlotsFree : 0);
            if (LastBarKey == key)
                return;
        }

        var returning = "";
        if (Plugin.Configuration.ShowDtrEntry)
        {
            LastBarKey = null;
            // 隱藏的公會不參與挑選（同主視窗）。手寫迴圈：每幀跑，不配置列舉器；同值時取先遇到的那艘，同 MaxBy／MinBy。
            var showLast = !Plugin.Configuration.OverlayFirstReturn;
            Submarine? sub = null;
            foreach (var s in Plugin.DatabaseCache.GetSubmarines())
            {
                if (Plugin.IsFCHidden(s.FreeCompanyId))
                    continue;

                if (sub == null || (showLast ? s.Return > sub.Return : s.Return < sub.Return))
                    sub = s;
            }

            if (sub is not { FreeCompanyId: > 0 })
                return;

            if (!Plugin.DatabaseCache.GetFreeCompanies().TryGetValue(sub.FreeCompanyId, out var fc))
                return;

            var name = Plugin.NameConverter.GetSub(sub, fc, Plugin.Configuration.DtrShowSubmarineName);
            var time = Language.TermsNoVoyage;
            if (sub.IsOnVoyage())
            {
                time = Language.TermsDone;

                var returnTime = sub.LeftoverTime();
                if (returnTime.TotalSeconds > 0)
                    time = Utils.ToTime(returnTime);
            }

            returning = $"{name} ({time})";
        }

        var separator = Plugin.Configuration.DtrShowOverlayNumbers && Plugin.Configuration.ShowDtrEntry ? " - " : string.Empty;

        // DTR 空間很擠：拿掉方括號與多餘空白（[1 | 27] → 1/27、[95 / 140] → 95/140）。
        var numbers = "";
        if (Plugin.Configuration.DtrShowOverlayNumbers)
            numbers = Plugin.ReturnOverlay.OverlayNumbers();

        var slots = "";
        if (Plugin.Configuration.DtrShowInventorySlots && Storage.InventorySlotsFree > -1)
            slots = $" {140 - Storage.InventorySlotsFree}/140";

        DtrEntry!.Text = $"{returning}{separator}{numbers}{slots}";

        // 圖示化／縮寫之後看不出各個數字是什麼意思，所以這一格要有提示。
        var tip = new StringBuilder("SubmarineTracker — 潛水艇");
        if (Plugin.Configuration.ShowDtrEntry && returning.Length > 0)
            tip.Append($"\n下一艘返回：{returning}");
        if (Plugin.Configuration.DtrShowOverlayNumbers)
            tip.Append($"\n已返回／航行中：{numbers}");
        if (Plugin.Configuration.DtrShowInventorySlots && Storage.InventorySlotsFree > -1)
            tip.Append($"\n背包已用：{140 - Storage.InventorySlotsFree}/140");
        tip.Append("\n\n左鍵：開啟／關閉主視窗\n右鍵：開啟／關閉設定視窗");
        DtrEntry.Tooltip = tip.ToString();
        LastBarKey = key;
    }

    private void UpdateVisibility(bool shown) => DtrEntry!.Shown = shown;

    private void OnClick(DtrInteractionEvent ev)
    {
        if (ev.ClickType == MouseClickType.Left)
            Plugin.OpenTracker();
        else
            Plugin.OpenConfig();
    }
}

using Newtonsoft.Json;
using SubmarineTracker.Resources;

namespace SubmarineTracker.Data;

/// <summary>
/// 把一條航線複製成 AutoRetainer 的潛水艇點位計畫 JSON，放進剪貼簿。
/// </summary>
/// <remarks>
/// 契約：只寫剪貼簿。不開 IPC、不碰 AutoRetainer 的設定檔、不掛任何自動事件。
/// JSON 只給 Name 與 Points，不給 GUID —— 貼上端只讀這兩個欄位，
/// 不給 GUID 它才會保留自己那份計畫的 GUID，已指派的潛水艇不會斷開。
/// Points 照呼叫端給的順序原樣寫出：貼上端裁點時以排列順序決勝。
/// </remarks>
public static class PointPlanExport
{
    public static void CopyToClipboard(IReadOnlyList<uint> sectors)
    {
        if (sectors.Count == 0)
            return;

        var letters = Utils.SectorsToPath(string.Empty, sectors);
        var name = $"{Voyage.SectorToMapName(sectors[0])}-{letters}";
        ImGui.SetClipboardText(JsonConvert.SerializeObject(new { Name = name, Points = sectors }));

        Plugin.ChatGui.Print(Utils.SuccessMessage(Language.BuilderRouteSuccessCopyPointPlan));
    }
}

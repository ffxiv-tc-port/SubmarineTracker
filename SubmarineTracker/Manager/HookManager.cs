using System.Threading.Tasks;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.Network;
using SubmarineTracker.Data;

namespace SubmarineTracker.Manager;

public unsafe class HookManager
{
    private Hook<PacketDispatcher.Delegates.HandleEventYieldPacket> PacketHandlerHook { get; init; }

    public HookManager()
    {
        PacketHandlerHook = Plugin.Hook.HookFromAddress<PacketDispatcher.Delegates.HandleEventYieldPacket>(PacketDispatcher.MemberFunctionPointers.HandleEventYieldPacket, PacketReceiver);
        PacketHandlerHook.Enable();
    }

    public void Dispose()
    {
        PacketHandlerHook.Dispose();
    }

    private void PacketReceiver(EventId id, short scene, byte responseId, int* intParams, byte argCount)
    {
        PacketHandlerHook.OriginalDisposeSafe(id, scene, responseId, intParams, argCount);

        // We only care about voyage results
        if (id != 721343)
            return;

        try
        {
            var instance = HousingManager.Instance();
            if (instance == null || instance->WorkshopTerritory == null)
                return;

            var current = instance->WorkshopTerritory->Submersible.DataPointers[4];
            if (current.Value == null)
                return;

            var sub = current.Value;

            var fcId = Plugin.GetFCId;
            // 這是唯一會把 fcId 寫進資料庫的路徑(Loot.FreeCompanyId)。GetFCId 取不到部隊時回 0,
            // 讓 0 寫下去會在使用者存檔裡留下歸屬不明、之後也對不回任何部隊的戰利品列。
            // 語意與 Plugin.cs 的 `if (fcId == 0) return;` 一致:寧可少記一次,不要寫髒資料。
            if (fcId == 0)
                return;

            var register = sub->RegisterTime;
            var returnTime = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds(); // sub->ReturnTime is 0 at this point

            var data = sub->GatheredData;
            if (data[0].ItemIdPrimary == 0)
                return;

            var validSectors = data.Filter(val => val.Point > 0);
            var expGathered = (uint) validSectors.Sum(val => val.ExpGained);
            var buildRank = Sectors.CalculateOriginalRank(sub->RankId, sub->CurrentExp, expGathered);
            var build = new Build.SubmarineBuild(buildRank, sub->HullId, sub->SternId, sub->BowId, sub->BridgeId);

            var lootList = new List<Loot>();
            foreach (var val in validSectors)
                lootList.Add(new Loot(build, val) {FreeCompanyId = fcId, Register = register, Return = returnTime});

            Task.Run(() =>
            {
                try
                {
                    foreach (var loot in lootList)
                        Plugin.DatabaseCache.Database.InsertLootEntry(loot);
                }
                catch (Exception ex)
                {
                    Plugin.Log.Error(ex, "Error while upsert of loot entry");
                }
            });
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Error in packet receiver");
        }
    }
}

using System.Runtime.CompilerServices;

namespace SubmarineTracker.Manager;

public class HookManager
{
    // null＝航行結果的 hook 沒建立；型別刻意是 IDisposable，本類別不直接引用 VoyageLootHook 碰到的 CS 成員。
    private readonly IDisposable? LootHook;

    public HookManager()
    {
        // 換了 CS 世代、我方補丁的成員不見時，JIT 在進 VoyageLootHook 的方法之前就擲，只有呼叫端攔得到。
        try
        {
            LootHook = CreateLootHook();
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning($"SubmarineTracker：航行結果的 hook 建立失敗（{ex.GetType().Name}: {ex.Message}），" +
                               "「航行結果的戰利品記錄」已停用；潛水艇追蹤、返航通知與其餘視窗不受影響。");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static IDisposable? CreateLootHook()
        => VoyageLootHook.TryCreate();

    public void Dispose()
    {
        LootHook?.Dispose();
    }
}

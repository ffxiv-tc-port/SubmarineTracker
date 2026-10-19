using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.Gui.Toast;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using SubmarineTracker.Data;
using SubmarineTracker.Resources;
using SubmarineTracker.Windows.Config;

namespace SubmarineTracker;

public class Notify
{
    private readonly Plugin Plugin;

    private bool IsInitialized;
    private readonly HashSet<string> FinishedNotifications = [];

    // Tuples whose "Notify…"/"Repair…" key has already been passed to FinishedNotifications.Add, so
    // NotifyLoop can skip those subs without building the key string every frame. Keys are never
    // removed, so a hit here means Add would have returned false.
    private readonly HashSet<(string Name, ulong FcId, uint Return)> SeenReturns = [];
    private readonly HashSet<(string Name, uint Register, ulong ContentId, DateTime Day)> SeenRepairs = [];

    public Notify(Plugin plugin)
    {
        Plugin = plugin;
    }

    private void Init()
    {
        // We call this in the first framework update to ensure all subs have loaded
        IsInitialized = true;
        foreach (var sub in Plugin.DatabaseCache.GetSubmarines())
            FinishedNotifications.Add($"Dispatch{sub.Register}{sub.Return}");
    }

    public void NotifyLoop(IFramework _)
    {
        var subs = Plugin.DatabaseCache.GetSubmarines();
        var fcs = Plugin.DatabaseCache.GetFreeCompanies();
        if (subs.Length == 0 || fcs.Count == 0)
            return;

        if (!IsInitialized)
            Init();

        // Same order as GetFCOrderWithoutHidden(), walked directly: this runs every frame.
        foreach (var (id, hidden) in Plugin.Configuration.ManagedFCs)
        {
            if (hidden)
                continue;

            // 清單裡的部隊在資料庫讀不到時略過：這裡每幀都跑，不能讓它擲例外。
            if (!fcs.TryGetValue(id, out var fc))
                continue;

            foreach (var sub in subs)
            {
                if (sub.FreeCompanyId != id)
                    continue;

                // this state happens after the rewards got picked up
                if (sub.Return == 0 || sub.ReturnTime > DateTime.Now.ToUniversalTime())
                    continue;

                if (SeenReturns.Contains((sub.Name, id, sub.Return)) || !NotifyEnabledFor(sub, id))
                    continue;

                SeenReturns.Add((sub.Name, id, sub.Return));
                if (FinishedNotifications.Add($"Notify{sub.Name}{id}{sub.Return}"))
                {
                    if (Plugin.Configuration.NotifyForReturns)
                        SendReturn(sub, fc);

                    if (Plugin.Configuration.WebhookReturn)
                        QueueReturnWebhook(sub, fc);
                }
            }
        }

        var fcId = Plugin.GetFCId;
        if (!Plugin.Configuration.NotifyForRepairs || !fcs.TryGetValue(fcId, out var currentFC))
            return;

        foreach (var sub in subs)
        {
            if (sub.FreeCompanyId != fcId)
                continue;

            // We want this state, as it signals a returned submarine
            if (sub.Return != 0 || sub.NoRepairNeeded)
                continue;

            // using just date here because subs can't come back the same day and be broken again
            var repair = (sub.Name, sub.Register, Plugin.PlayerState.ContentId, DateTime.Now.Date);
            if (!SeenRepairs.Add(repair))
                continue;

            if (FinishedNotifications.Add($"Repair{repair.Name}{repair.Register}{repair.ContentId}{repair.Date}"))
                SendRepair(sub, currentFC);
        }
    }

    private bool NotifyEnabledFor(Submarine sub, ulong fcId)
    {
        var config = Plugin.Configuration;
        if (config.NotifyForAll)
            return true;

        // Same "{Name}{fcId}" key as the settings window writes, looked up without allocating it.
        Span<char> key = stackalloc char[96];
        if (key.TryWrite($"{sub.Name}{fcId}", out var length) && config.NotifyFCSpecific.TryGetAlternateLookup<ReadOnlySpan<char>>(out var lookup))
            return lookup.TryGetValue(key[..length], out var on) && on;

        return config.NotifyFCSpecific.TryGetValue($"{sub.Name}{fcId}", out var enabled) && enabled;
    }

    // A method of its own so the closure is only allocated when a webhook is actually sent.
    private void QueueReturnWebhook(Submarine sub, FreeCompany fc) => Task.Run(() => SendReturnWebhook(sub, fc));

    public void CheckForDispatch(uint key, uint returnTime)
    {
        var fcId = Plugin.GetFCId;
        if (!Plugin.DatabaseCache.GetFreeCompanies().TryGetValue(fcId, out var currentFC))
            return;

        var subs = Plugin.DatabaseCache.GetSubmarines(fcId);
        if (subs.Length == 0)
            return;

        var sub = subs.FirstOrDefault(s => s.Register == key);
        if (sub == null || !sub.IsValid())
            return;

        if (!FinishedNotifications.Add($"Dispatch{key}{returnTime}"))
            return;

        var found = Plugin.Configuration.NotifyFCSpecific.TryGetValue($"{sub.Name}{fcId}", out var ok);
        if (!Plugin.Configuration.NotifyForAll && !(found && ok))
            return;

        if (Plugin.Configuration.WebhookDispatch)
            SendDispatchWebhook(sub, currentFC, returnTime);

        if (Plugin.Configuration.WebhookOfflineMode)
            SendOfflineModeData(sub, currentFC, returnTime);
    }

    public void SendDispatchWebhook(Submarine sub, FreeCompany fc, uint returnTime)
    {
        if (!Plugin.Configuration.WebhookUrl.StartsWith("https://"))
            return;

        var content = new Webhook.WebhookContent();
        content.Embeds.Add(new
        {
            title = Plugin.NameConverter.GetSub(sub, fc),
            description = Language.WebhookOnDispatch.Format(returnTime),
            color = 15124255
        });

        Webhook.PostMessage(content);
    }

    public void SendReturnWebhook(Submarine sub, FreeCompany fc)
    {
        // No need to send messages if the user isn't logged in (also prevents sending on startup)
        if (!Plugin.ClientState.IsLoggedIn)
            return;

        if (Plugin.Configuration.WebhookOfflineMode)
            return;

        if (!Plugin.Configuration.WebhookUrl.StartsWith("https://"))
            return;

        // Prevent that multibox user send multiple webhook triggers
        using var mutex = new Mutex(false, "Global\\SubmarineTrackerMutex");
        if (!mutex.WaitOne(0, false))
            return;

        var content = new Webhook.WebhookContent();
        content.Embeds.Add(new
        {
            title = Plugin.NameConverter.GetSub(sub, fc),
            description = Language.WebhookOnReturn.Format(sub.Return),
            color = 8447519
        });

        Webhook.PostMessage(content);

        // Ensure that the other process had time to catch up
        Thread.Sleep(500);
        mutex.ReleaseMutex();
    }

    public void SendOfflineModeData(Submarine sub, FreeCompany fc, uint returnTime)
    {
        if (!ConfigWindow.WebhookRegex().IsMatch(Plugin.Configuration.WebhookUrl))
            return;

        Plugin.UploadNotify(new Export.SubNotify(
                                Plugin.Configuration.WebhookUrl,
                                Language.WebhookOnReturn.Format(returnTime),
                                Plugin.NameConverter.GetSub(sub, fc),
                                Plugin.Configuration.WebhookMention,
                                Plugin.Configuration.WebhookRoleMention,
                                returnTime));
    }

    public void SendReturn(Submarine sub, FreeCompany fc)
    {
        var name = Plugin.NameConverter.GetSub(sub, fc);
        Plugin.ChatGui.Print(GenerateMessage(name));

        // Same sentence the chat line just printed, but handed to Windows so it is visible
        // while the player is looking at another window. TrayNotify decides for itself
        // whether to act: it is off by default and stays quiet while the game has focus.
        Plugin.TrayNotify.Notify(Language.NotificationChatReturn.Format(name));

        if (Plugin.Configuration.OverlayAlwaysOpen)
            Plugin.ReturnOverlay.IsOpen = true;
    }

    public void SendRepair(Submarine sub, FreeCompany fc)
    {
        Plugin.ChatGui.Print(RepairMessage(Plugin.NameConverter.GetSub(sub, fc)));

        if (Plugin.Configuration.ShowRepairToast)
            Plugin.ToastGui.ShowQuest(ShortRepairMessage(), new QuestToastOptions {IconId = 60858, PlaySound = true});
    }

    private static SeString GenerateMessage(string name)
    {
        return new SeStringBuilder()
               .AddUiForeground("[Submarine Tracker] ", 540)
               .AddUiForeground(Language.NotificationChatReturn.Format(name), 566)
               .BuiltString;
    }

    private static SeString RepairMessage(string name)
    {
        return new SeStringBuilder()
               .AddUiForeground("[Submarine Tracker] ", 540)
               .AddUiForeground(Language.NotificationChatRepair.Format(name), 43)
               .BuiltString;
    }

    private static SeString ShortRepairMessage()
    {
        return new SeStringBuilder()
               .AddUiForeground(Language.NotificationToastRepair, 43)
               .BuiltString;
    }
}

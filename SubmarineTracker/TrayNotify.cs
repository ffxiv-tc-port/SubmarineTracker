using Dalamud.Utility;
using NotificationMasterAPI;

namespace SubmarineTracker;

/// <summary>
/// Windows-side notifications for the events a player misses because they alt-tabbed away.
///
/// Everything here is opt-in and display only:
/// - <see cref="Configuration.TrayNotification"/> defaults to false, so nothing changes for
///   existing users until they tick the box themselves.
/// - The tray balloon comes from the NotificationMaster plugin over IPC. When that plugin is
///   not installed the API call simply reports failure and we still flash the taskbar icon,
///   which Dalamud can do on its own without any third party plugin.
/// - Nothing in here touches the game or triggers an in-game action. It only asks Windows to
///   draw attention to a window that is already running.
/// </summary>
public class TrayNotify
{
    private readonly NotificationMasterApi Api;

    // The "NotificationMaster is not answering" notice is worth exactly one line per session.
    // This runs off the submarine notify loop, so an unthrottled log line would repeat for
    // every returning submarine of every free company.
    private bool LoggedUnavailable;

    public TrayNotify()
    {
        Api = new NotificationMasterApi(Plugin.PluginInterface);
    }

    /// <summary>
    /// Raises a Windows notification, but only while the game window is in the background.
    /// The balloon title is left to the API, which fills in this plugin's manifest name.
    /// </summary>
    /// <param name="text">Body of the tray balloon. Should say the same thing as the chat message.</param>
    public void Notify(string text)
    {
        if (!Plugin.Configuration.TrayNotification)
            return;

        // Util.ApplicationIsActivated() asks Windows which window is in the foreground; it reads
        // nothing out of the game. If the game already has focus then the player has just seen the
        // chat message, and a tray balloon on top of that is only noise.
        //
        // NotificationMaster exposes an equivalent IsGameWindowActivated over IPC, but that member
        // is private in NotificationMasterAPI - in the 1.0.0.1 package the rest of the fleet uses
        // as well as in current source - so it cannot be called from here. The Dalamud helper is
        // public, does the same foreground-window comparison, and keeps working when
        // NotificationMaster is not installed at all.
        if (Util.ApplicationIsActivated())
            return;

        try
        {
            var delivered = Api.DisplayTrayNotification(text);
            if (!delivered && !LoggedUnavailable)
            {
                LoggedUnavailable = true;
                Plugin.Log.Information("Tray notification was requested but NotificationMaster did not accept it - the plugin is probably not installed or not enabled. Falling back to flashing the taskbar icon only.");
            }

            // Always flash, whether or not the balloon went out. FlashWindow is built into Dalamud,
            // so this half keeps working without NotificationMaster. Its default flashIfOpen=false
            // makes it re-check the foreground window, so a game that regained focus in between the
            // two calls is left alone.
            Util.FlashWindow();
        }
        catch (Exception ex)
        {
            // DisplayTrayNotification reaches into another plugin over IPC. NotificationMasterApi
            // swallows IpcNotReadyError itself but nothing else, and this runs on the framework
            // thread inside the submarine notify loop - an escaping exception there would take out
            // the notification pass for every remaining submarine.
            Plugin.Log.Information(ex, "Failed to raise a Windows notification, carrying on without one.");
        }
    }
}

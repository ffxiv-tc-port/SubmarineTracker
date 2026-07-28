using Dalamud.Game.Command;
using System.Reflection;
using Dalamud.Plugin.Services;
using SubmarineTracker.Resources;

namespace SubmarineTracker.Attributes
{
    public class PluginCommandManager<THost> : IDisposable
    {
        private readonly ICommandManager CommandManager;
        private readonly (string, CommandInfo)[] PluginCommands;
        private readonly THost Host;

        public PluginCommandManager(THost host, ICommandManager commandManager)
        {
            this.CommandManager = commandManager;
            this.Host = host;

            this.PluginCommands = host!.GetType().GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
                .Where(method => method.GetCustomAttribute<CommandAttribute>() != null)
                .SelectMany(GetCommandInfoTuple)
                .ToArray();

            AddCommandHandlers();
        }

        private void AddCommandHandlers()
        {
            foreach (var (command, commandInfo) in this.PluginCommands)
            {
                this.CommandManager.AddHandler(command, commandInfo);
            }
        }

        private void RemoveCommandHandlers()
        {
            foreach (var (command, _) in this.PluginCommands)
            {
                this.CommandManager.RemoveHandler(command);
            }
        }

        private IEnumerable<(string, CommandInfo)> GetCommandInfoTuple(MethodInfo method)
        {
            var handlerDelegate = (IReadOnlyCommandInfo.HandlerDelegate)Delegate.CreateDelegate(typeof(IReadOnlyCommandInfo.HandlerDelegate), this.Host, method);

            var command = handlerDelegate.Method.GetCustomAttribute<CommandAttribute>();
            var aliases = handlerDelegate.Method.GetCustomAttribute<AliasesAttribute>();
            var helpMessage = handlerDelegate.Method.GetCustomAttribute<HelpMessageAttribute>();
            var doNotShowInHelp = handlerDelegate.Method.GetCustomAttribute<DoNotShowInHelpAttribute>();

            var commandInfo = new CommandInfo(handlerDelegate)
            {
                HelpMessage = LocalizeHelpMessage(command!.Command, helpMessage?.HelpMessage),
                ShowInHelp = doNotShowInHelp == null,
            };

            // Create list of tuples that will be filled with one tuple per alias, in addition to the base command tuple.
            var commandInfoTuples = new List<(string, CommandInfo)> { (command!.Command, commandInfo) };
            if (aliases != null)
            {
                foreach (var alias in aliases.Aliases)
                {
                    commandInfoTuples.Add((alias, commandInfo));
                }
            }

            return commandInfoTuples;
        }

        /// <summary>
        /// HelpMessage 會顯示在 /xlhelp 和 Dalamud 插件安裝器的指令清單裡,原本直接沿用
        /// [HelpMessage] 屬性上的英文。屬性參數必須是編譯期常數,沒辦法在屬性上做在地化,
        /// 所以改在註冊當下依指令名去 resx 找 CommandHelp_&lt;指令&gt;;找不到就沿用屬性上的英文原文。
        /// </summary>
        private static string LocalizeHelpMessage(string command, string? fallback)
        {
            if (string.IsNullOrEmpty(fallback))
                return string.Empty;

            try
            {
                return Language.ResourceManager.GetString($"CommandHelp_{command.TrimStart('/')}", Language.Culture) ?? fallback;
            }
            catch (Exception e)
            {
                Plugin.Log.Warning(e, "Unable to localize help message for {Command}", command);
                return fallback;
            }
        }

        public void Dispose()
        {
            RemoveCommandHandlers();
            GC.SuppressFinalize(this);
        }
    }
}

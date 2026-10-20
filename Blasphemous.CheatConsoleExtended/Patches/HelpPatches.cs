using Blasphemous.NewbieEltonLibs.Extensions.GameLibs;
using Framework.Managers;
using Gameplay.UI.Console;
using Gameplay.UI.Widgets;
using HarmonyLib;
using System.Collections.Generic;
using Tools.DataContainer;

namespace Blasphemous.CheatConsoleExtended;

[HarmonyPatch(typeof(Help), "Execute")]
internal static class Help_RenderCommandCatalog_Patch
{
    private const string ModCommandSystemTypeName = "Blasphemous.CheatConsole.ModCommandSystem";

    [HarmonyPrefix]
    private static bool Prefix(Help __instance, string[] parameters)
    {
        if (parameters != null && parameters.Length > 0)
        {
            return true;
        }

        ConsoleWidget console = TraverseUtils.GetValue<ConsoleWidget>(
            __instance,
            "Console",
            TraverseUtils.TraverseAccessType.Property);
        if (console == null)
        {
            return true;
        }

        WriteCatalog(console);
        return false;
    }

    private static void WriteCatalog(ConsoleWidget console)
    {
        List<CommandCatalogEntry> vanillaCommands = new List<CommandCatalogEntry>();
        List<string> sharedCommandIds = new List<string>();
        List<CommandCatalogEntry> modCommands = new List<CommandCatalogEntry>();

        List<ConsoleCommand> commands = TraverseUtils.GetValue<List<ConsoleCommand>>(
            console,
            "commands",
            TraverseUtils.TraverseAccessType.Field);
        if (commands != null)
        {
            foreach (ConsoleCommand command in commands)
            {
                if (command == null)
                {
                    continue;
                }

                AddCommandNames(IsModCommand(command) ? modCommands : vanillaCommands, command);
            }
        }

        if (Core.SharedCommands != null)
        {
            List<SharedCommand> sharedCommands = Core.SharedCommands.GetAllCommands();
            if (sharedCommands != null)
            {
                foreach (SharedCommand command in sharedCommands)
                {
                    if (command != null && !string.IsNullOrEmpty(command.Id))
                    {
                        sharedCommandIds.Add(command.Id);
                    }
                }
            }
        }

        foreach (string line in HelpCommandCatalogRenderer.Render(vanillaCommands, sharedCommandIds, modCommands))
        {
            console.Write(line);
        }
    }

    private static void AddCommandNames(List<CommandCatalogEntry> entries, ConsoleCommand command)
    {
        List<string> names = command.GetNames();
        if (names == null)
        {
            return;
        }

        // Current multi-entry vanilla commands dispatch differently by input name.
        foreach (string name in names)
        {
            if (!string.IsNullOrEmpty(name))
            {
                entries.Add(new CommandCatalogEntry(name));
            }
        }
    }

    private static bool IsModCommand(ConsoleCommand command)
    {
        return command.GetType().FullName == ModCommandSystemTypeName;
    }
}
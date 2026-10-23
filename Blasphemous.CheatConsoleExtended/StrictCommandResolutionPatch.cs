using Blasphemous.CheatConsole;
using Blasphemous.NewbieEltonLibs.Extensions.GameLibs;
using Framework.FrameworkCore;
using Framework.Managers;
using Gameplay.UI.Console;
using Gameplay.UI.Widgets;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using Tools.DataContainer;

namespace Blasphemous.CheatConsoleExtended;

/// <summary>Coordinates strict command resolution and dispatch for the vanilla console.</summary>
internal static class StrictCommandRuntime
{
    private static readonly Dictionary<string, string[]> VanillaSubcommands = CreateVanillaSubcommands();

    /// <summary>Processes an input line and determines whether the original console handler should continue.</summary>
    /// <returns><see langword="true"/> when the original handler should run; otherwise, <see langword="false"/>.</returns>
    internal static bool Process(ConsoleWidget console, string rawText, out ConsoleCommand resolvedCommand)
    {
        resolvedCommand = null;
        string[] tokens = StrictCommandResolver.Tokenize(rawText);
        if (tokens.Length == 0)
            return false;

        List<CommandDefinition> catalog = CreateCatalog(console);
        if (StrictCommandResolver.ShouldProcessInternalCommand(rawText, tokens, catalog))
        {
            bool handled = Traverse.Create(console)
                .Method("ProcessInternalCommand")
                .GetValue<bool>(rawText);
            if (!handled)
                console.Write("Command not found. Use Help for more information.");
            return false;
        }

        CommandDefinition command = StrictCommandResolver.FindExact(tokens[0], catalog);
        if (StrictCommandResolver.ShouldRunOriginal(tokens, catalog))
        {
            resolvedCommand = command == null ? null : command.Value as ConsoleCommand;
            return true;
        }

        console.Write("Command not found. Use Help for more information.");
        if (command == null)
        {
            List<string> suggestions = tokens.Length > 1
                ? StrictCommandResolver.GetHierarchicalSuggestions(tokens[0], tokens[1], catalog, GetSuggestionDistance())
                : StrictCommandResolver.GetSuggestions(tokens[0], catalog, GetSuggestionDistance());
            WriteSuggestion(console, suggestions);
        }
        else if (command.Value is ConsoleCommand && tokens.Length > 1 && command.Subcommands.Count > 0)
            WriteSuggestion(console, StrictCommandResolver.GetSubcommandSuggestions(command, tokens[1], GetSuggestionDistance()));

        return false;
    }

    /// <summary>Finds a shared command by case-insensitive exact ID match.</summary>
    /// <returns>The matching command, or <see langword="null"/> when none exists.</returns>
    internal static SharedCommand FindSharedCommand(SharedCommands sharedCommands, string id)
    {
        if (sharedCommands == null || string.IsNullOrEmpty(id))
            return null;

        foreach (SharedCommand command in sharedCommands.GetAllCommands())
        {
            if (command != null && string.Equals(command.Id, id, StringComparison.OrdinalIgnoreCase))
                return command;
        }

        return null;
    }

    /// <summary>Builds the command catalog from console commands, built-in commands, and shared commands.</summary>
    private static List<CommandDefinition> CreateCatalog(ConsoleWidget console)
    {
        List<CommandDefinition> catalog = [];
        List<ConsoleCommand> commands = TraverseUtils.GetValue<List<ConsoleCommand>>(console, "commands");
        if (commands != null)
        {
            foreach (ConsoleCommand command in commands)
            {
                if (command == null)
                    continue;

                List<string> subcommands = GetSubcommands(command);
                foreach (string name in command.GetNames())
                {
                    if (string.IsNullOrEmpty(name))
                        continue;

                    catalog.Add(new CommandDefinition(name, subcommands, command));
                }
            }
        }

        catalog.Add(new CommandDefinition("clear"));
        catalog.Add(new CommandDefinition("cls"));
        catalog.Add(new CommandDefinition("mirrorlog"));

        if (Core.SharedCommands != null)
        {
            foreach (SharedCommand command in Core.SharedCommands.GetAllCommands())
            {
                if (command != null && !string.IsNullOrEmpty(command.Id))
                    catalog.Add(new CommandDefinition(command.Id, null, command));
            }
        }

        return catalog;
    }

    /// <summary>Collects known subcommands for a console command, including applicable mod and shared commands.</summary>
    private static List<string> GetSubcommands(ConsoleCommand command)
    {
        List<string> subcommands = [];
        bool isSharedCommands = false;
        foreach (string name in command.GetNames())
        {
            if (VanillaSubcommands.TryGetValue(name, out string[] knownSubcommands))
                AddUnique(subcommands, knownSubcommands);
            if (string.Equals(name, "command", StringComparison.OrdinalIgnoreCase))
                isSharedCommands = true;
        }

        AddUnique(subcommands, GetModSubcommands(command));
        if (isSharedCommands && Core.SharedCommands != null)
        {
            foreach (SharedCommand sharedCommand in Core.SharedCommands.GetAllCommands())
            {
                if (sharedCommand != null)
                    AddUnique(subcommands, sharedCommand.Id);
            }
        }

        return subcommands;
    }

    /// <summary>Gets subcommand names exposed by the mod-command adapter, or an empty list when unavailable.</summary>
    private static IEnumerable<string> GetModSubcommands(ConsoleCommand command)
    {
        if (!string.Equals(command.GetType().FullName, "Blasphemous.CheatConsole.ModCommandSystem", StringComparison.Ordinal))
            return [];

        try
        {
            ModCommand modCommand = TraverseUtils.GetValue<ModCommand>(command, "command");
            if (modCommand == null)
                return [];

            IDictionary availableCommands = TraverseUtils.GetValue<IDictionary>(modCommand, "availableCommands");
            if (availableCommands == null)
            {
                availableCommands = Traverse.Create(modCommand)
                    .Method("AddSubCommands")
                    .GetValue<IDictionary>([]);
                if (availableCommands != null)
                    TraverseUtils.SetValue(ref modCommand, "availableCommands", availableCommands);
            }

            List<string> names = [];
            if (availableCommands != null)
            {
                foreach (DictionaryEntry item in availableCommands)
                    names.Add(item.Key.ToString());
            }
            return names;
        }
        catch (Exception)
        {
            return [];
        }
    }

    /// <summary>Gets the configured suggestion distance normalized to nonnegative, defaulting to two when configuration is unavailable.</summary>
    private static int GetSuggestionDistance()
    {
        if (Main.CheatConsoleExtended == null || Main.CheatConsoleExtended.Config == null)
            return 2;
        return StrictCommandResolver.NormalizeDistance(Main.CheatConsoleExtended.Config.CommandSuggestionDistance);
    }

    /// <summary>Writes a formatted suggestion line when suggestions are available.</summary>
    private static void WriteSuggestion(ConsoleWidget console, IList<string> suggestions)
    {
        string line = StrictCommandResolver.FormatSuggestionLine(suggestions);
        if (line != null)
            console.Write(line);
    }

    /// <summary>Checks whether a list contains a value using a case-insensitive comparison.</summary>
    private static bool ContainsIgnoreCase(IList<string> values, string value)
    {
        foreach (string item in values)
        {
            if (string.Equals(item, value, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>Adds nonempty values that are not already present, ignoring case.</summary>
    private static void AddUnique(List<string> target, IEnumerable<string> values)
    {
        if (values == null)
            return;

        foreach (string value in values)
        {
            if (!string.IsNullOrEmpty(value) && !ContainsIgnoreCase(target, value))
                target.Add(value);
        }
    }

    /// <summary>Adds a nonempty value when it is not already present, ignoring case.</summary>
    private static void AddUnique(List<string> target, string value)
    {
        if (!string.IsNullOrEmpty(value) && !ContainsIgnoreCase(target, value))
            target.Add(value);
    }

    /// <summary>Creates the known subcommand map for vanilla console commands.</summary>
    private static Dictionary<string, string[]> CreateVanillaSubcommands()
    {
        return new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["achievement"] = ["help", "addprogress", "check", "checkprogress", "clear", "clearall", "clearsteam", "clearsteamall", "disablepopup", "enablepopup", "grant"],
            ["alms"] = ["help", "current", "list", "consume", "set"],
            ["audio"] = ["help", "list", "master", "music", "sfx", "voiceover"],
            ["bonus"] = ["help", "list"],
            ["bossrush"] = ["help", "end", "golast", "hub", "next", "printscore", "start", "unlock"],
            ["camera"] = ["all", "game", "scene", "ui", "virtual"],
            ["completion"] = ["base", "get", "show", "ng+"],
            ["debug"] = ["help", "list", "off", "on"],
            ["show_debug_ui"] = ["help", "current", "off", "on"],
            ["demake"] = ["help", "enter"],
            ["dialog"] = ["help", "list", "start"],
            ["flag"] = ["help", "set", "clear", "test"],
            ["gamemode"] = ["help", "list", "current", "set"],
            ["guilt"] = ["help", "get", "reset", "add"],
            ["language"] = ["help", "list", "current", "set"],
            ["map"] = ["help", "list", "set", "secrets", "secret", "unrevealed", "reveal"],
            ["miriam"] = ["help", "status", "start", "end", "activateportal", "deactivateportal", "gotogoal"],
            ["penitence"] = ["help", "current", "activate", "deactivate", "abandon", "complete", "listall", "listabandoned", "listcompleted"],
            ["savegame"] = ["help", "load", "save", "enablenewgameplus"],
            ["showui"] = ["help", "current", "off", "on"],
            ["skill"] = ["help", "list", "lock", "lockall", "showui", "unlock", "unlockall"],
            ["skin"] = ["help", "get", "list", "listunlocked", "lock", "set", "unlock"],
            ["health"] = ["help", "current", "set", "reset", "upgrade", "upgradeto", "fill", "setmax"],
            ["flask"] = ["help", "current", "set", "reset", "upgrade", "upgradeto", "fill", "setmax"],
            ["fervour"] = ["help", "current", "set", "reset", "upgrade", "upgradeto", "fill", "setmax"],
            ["purge"] = ["help", "current", "set", "reset", "upgrade", "upgradeto"],
            ["meaculpa"] = ["help", "current", "set", "reset", "upgrade", "upgradeto"],
            ["strength"] = ["help", "current", "set", "reset", "upgrade", "upgradeto"],
            ["flaskhealth"] = ["help", "current", "set", "reset", "upgrade", "upgradeto", "fill", "setmax"],
            ["teleport"] = ["help", "list", "go", "showui", "unlock"],
            ["testplan"] = ["help", "1"],
            ["tutorial"] = ["list", "show"],
            ["relic"] = ["help", "list", "listowned", "add", "remove", "equiped", "equip", "unequip"],
            ["questitem"] = ["help", "list", "listowned", "add", "remove"],
            ["collectible"] = ["help", "list", "listowned", "add", "remove"],
            ["bead"] = ["help", "list", "listowned", "setslots", "add", "remove", "equiped", "equip", "unequip"],
            ["prayer"] = ["help", "list", "listowned", "add", "remove", "equiped", "equip", "unequip", "decipher"],
            ["sword"] = ["help", "list", "listowned", "add", "remove", "equiped", "equip", "unequip"],
            ["key"] = ["help", "list", "add", "remove"],
            ["command"] = ["help", "list", "refresh"]
        };
    }
}

/// <summary>Routes console input through strict command resolution before the vanilla handler runs.</summary>
[HarmonyPatch(typeof(ConsoleWidget), nameof(ConsoleWidget.ProcessCommand))]
internal static class ConsoleWidget_ProcessCommand_Patch
{
    /// <summary>Handles the input through the strict resolver and controls whether the original method runs.</summary>
    /// <returns><see langword="true"/> to run the original method; otherwise, <see langword="false"/>.</returns>
    private static bool Prefix(
        ConsoleWidget __instance,
        string rawText,
        out VanillaConsoleOutputLocalizer.ProcessCommandScope __state)
    {
        __state = VanillaConsoleOutputLocalizer.BeginProcessCommand();
        bool runOriginal = StrictCommandRuntime.Process(__instance, rawText, out ConsoleCommand command);
        if (runOriginal)
            VanillaConsoleOutputLocalizer.SelectCommand(command == null ? null : command.GetType());
        return runOriginal;
    }

    /// <summary>Restores output-localization state after nested and top-level command processing.</summary>
    private static Exception Finalizer(
        Exception __exception,
        VanillaConsoleOutputLocalizer.ProcessCommandScope __state)
    {
        VanillaConsoleOutputLocalizer.EndProcessCommand(__state);
        return __exception;
    }
}

/// <summary>Replaces shared-command prefix lookup with case-insensitive exact matching.</summary>
[HarmonyPatch(typeof(SharedCommands), "GetCommandFromName")]
internal static class SharedCommands_GetCommandFromName_Patch
{
    /// <summary>Sets the exact lookup result and suppresses the original shared-command lookup.</summary>
    /// <param name="__result">Receives the matching command, or <see langword="null"/> if no exact match exists.</param>
    /// <returns>Always <see langword="false"/> to skip the original method.</returns>
    private static bool Prefix(SharedCommands __instance, string id, ref SharedCommand __result)
    {
        __result = StrictCommandRuntime.FindSharedCommand(__instance, id);
        return false;
    }
}
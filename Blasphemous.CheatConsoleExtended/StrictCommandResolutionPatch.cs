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

internal static class StrictCommandRuntime
{
    private static readonly Dictionary<string, string[]> VanillaSubcommands = CreateVanillaSubcommands();

    internal static void Process(ConsoleWidget console, string rawText)
    {
        string[] tokens = StrictCommandResolver.Tokenize(rawText);
        if (tokens.Length == 0)
            return;

        List<CommandDefinition> catalog = CreateCatalog(console);
        CommandDefinition command = StrictCommandResolver.FindExact(tokens[0], catalog);
        if (command != null)
        {
            if (command.Value is ConsoleCommand consoleCommand)
            {
                bool invalidSubcommand = tokens.Length > 1
                    && command.Subcommands.Count > 0
                    && StrictCommandResolver.FindExactSubcommand(command, tokens[1]) == null;

                Execute(consoleCommand, command.Name, Slice(tokens, 1));
                if (invalidSubcommand)
                {
                    WriteSuggestion(console, StrictCommandResolver.GetSubcommandSuggestions(
                        command,
                        tokens[1],
                        GetSuggestionDistance()));
                }
                return;
            }

            if (command.Value is SharedCommand)
            {
                Core.SharedCommands.ExecuteCommand(command.Name);
                return;
            }

            if (tokens.Length == 1)
                ExecuteInternalCommand(console, command.Name);
            else
                console.Write("Command not found. Use Help for more information.");
            return;
        }

        console.Write("Command not found. Use Help for more information.");
        List<string> suggestions = tokens.Length > 1
            ? StrictCommandResolver.GetHierarchicalSuggestions(tokens[0], tokens[1], catalog, GetSuggestionDistance())
            : StrictCommandResolver.GetSuggestions(tokens[0], catalog, GetSuggestionDistance());
        WriteSuggestion(console, suggestions);
    }

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

    private static IEnumerable<string> GetModSubcommands(ConsoleCommand command)
    {
        if (!string.Equals(command.GetType().FullName, "Blasphemous.CheatConsole.ModCommandSystem", StringComparison.Ordinal))
            return new string[0];

        try
        {
            ModCommand modCommand = TraverseUtils.GetValue<ModCommand>(command, "command");
            if (modCommand == null)
                return new string[0];

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
            return new string[0];
        }
    }

    private static void ExecuteInternalCommand(ConsoleWidget console, string commandName)
    {
        Traverse.Create(console)
            .Method("ProcessInternalCommand")
            .GetValue<bool>([commandName]);
    }

    private static void Execute(ConsoleCommand command, string name, string[] parameters)
    {
        string[] lowerParameters = new string[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
            lowerParameters[i] = parameters[i].ToLowerInvariant();

        if (command.HasLowerParameters() || command.ToLowerAll())
            command.Execute(name, lowerParameters);
        else
            command.Execute(name, parameters);
    }

    private static int GetSuggestionDistance()
    {
        if (Main.CheatConsoleExtended == null || Main.CheatConsoleExtended.Config == null)
            return 2;
        return StrictCommandResolver.NormalizeDistance(Main.CheatConsoleExtended.Config.CommandSuggestionDistance);
    }

    private static void WriteSuggestion(ConsoleWidget console, IList<string> suggestions)
    {
        string line = StrictCommandResolver.FormatSuggestionLine(suggestions);
        if (line != null)
            console.Write(line);
    }

    private static bool ContainsIgnoreCase(IList<string> values, string value)
    {
        foreach (string item in values)
        {
            if (string.Equals(item, value, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static string[] Slice(string[] values, int start)
    {
        string[] result = new string[values.Length - start];
        Array.Copy(values, start, result, 0, result.Length);
        return result;
    }

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

    private static void AddUnique(List<string> target, string value)
    {
        if (!string.IsNullOrEmpty(value) && !ContainsIgnoreCase(target, value))
            target.Add(value);
    }

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

[HarmonyPatch(typeof(ConsoleWidget), nameof(ConsoleWidget.ProcessCommand))]
internal static class ConsoleWidget_ProcessCommand_Patch
{
    private static bool Prefix(ConsoleWidget __instance, string rawText)
    {
        StrictCommandRuntime.Process(__instance, rawText);
        return false;
    }
}

[HarmonyPatch(typeof(SharedCommands), "GetCommandFromName")]
internal static class SharedCommands_GetCommandFromName_Patch
{
    private static bool Prefix(SharedCommands __instance, string id, ref SharedCommand __result)
    {
        __result = StrictCommandRuntime.FindSharedCommand(__instance, id);
        return false;
    }
}
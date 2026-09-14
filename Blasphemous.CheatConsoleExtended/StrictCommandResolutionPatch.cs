using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Blasphemous.CheatConsole;
using Framework.FrameworkCore;
using Framework.Managers;
using Gameplay.UI.Console;
using Gameplay.UI.Widgets;
using HarmonyLib;
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
                    && !ContainsIgnoreCase(command.Subcommands, tokens[1]);

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

            console.ProcessInternalCommand(command.Name);
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
        List<CommandDefinition> catalog = new List<CommandDefinition>();
        foreach (ConsoleCommand command in console.commands)
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
        List<string> subcommands = new List<string>();
        bool isSharedCommands = false;
        foreach (string name in command.GetNames())
        {
            string[] knownSubcommands;
            if (VanillaSubcommands.TryGetValue(name, out knownSubcommands))
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
            FieldInfo commandField = command.GetType().GetField("command", BindingFlags.Instance | BindingFlags.NonPublic);
            object modCommand = commandField == null ? null : commandField.GetValue(command);
            if (modCommand == null)
                return new string[0];

            FieldInfo availableCommandsField = typeof(ModCommand).GetField(
                "availableCommands",
                BindingFlags.Instance | BindingFlags.NonPublic);
            IDictionary availableCommands = availableCommandsField == null
                ? null
                : availableCommandsField.GetValue(modCommand) as IDictionary;
            if (availableCommands == null)
            {
                MethodInfo addSubCommands = typeof(ModCommand).GetMethod(
                    "AddSubCommands",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                availableCommands = addSubCommands == null
                    ? null
                    : addSubCommands.Invoke(modCommand, null) as IDictionary;
                if (availableCommands != null && availableCommandsField != null)
                    availableCommandsField.SetValue(modCommand, availableCommands);
            }

            List<string> names = new List<string>();
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
            ["achievement"] = new[] { "help", "addprogress", "check", "checkprogress", "clear", "clearall", "clearsteam", "clearsteamall", "disablepopup", "enablepopup", "grant" },
            ["alms"] = new[] { "help", "current", "list", "consume", "set" },
            ["audio"] = new[] { "help", "list", "master", "music", "sfx", "voiceover" },
            ["bonus"] = new[] { "help", "list" },
            ["bossrush"] = new[] { "help", "end", "golast", "hub", "next", "printscore", "start", "unlock" },
            ["camera"] = new[] { "all", "game", "scene", "ui", "virtual" },
            ["completion"] = new[] { "base", "get", "show", "ng+" },
            ["debug"] = new[] { "help", "list", "off", "on" },
            ["show_debug_ui"] = new[] { "help", "current", "off", "on" },
            ["demake"] = new[] { "help", "enter" },
            ["dialog"] = new[] { "help", "list", "start" },
            ["flag"] = new[] { "help", "set", "clear", "test" },
            ["gamemode"] = new[] { "help", "list", "current", "set" },
            ["guilt"] = new[] { "help", "get", "reset", "add" },
            ["language"] = new[] { "help", "list", "current", "set" },
            ["map"] = new[] { "help", "list", "set", "secrets", "secret", "unrevealed", "reveal" },
            ["miriam"] = new[] { "help", "status", "start", "end", "activateportal", "deactivateportal", "gotogoal" },
            ["penitence"] = new[] { "help", "current", "activate", "deactivate", "abandon", "complete", "listall", "listabandoned", "listcompleted" },
            ["savegame"] = new[] { "help", "load", "save", "enablenewgameplus" },
            ["showui"] = new[] { "help", "current", "off", "on" },
            ["skill"] = new[] { "help", "list", "lock", "lockall", "showui", "unlock", "unlockall" },
            ["skin"] = new[] { "help", "get", "list", "listunlocked", "lock", "set", "unlock" },
            ["health"] = new[] { "help", "current", "set", "reset", "upgrade", "upgradeto", "fill", "setmax" },
            ["flask"] = new[] { "help", "current", "set", "reset", "upgrade", "upgradeto", "fill", "setmax" },
            ["fervour"] = new[] { "help", "current", "set", "reset", "upgrade", "upgradeto", "fill", "setmax" },
            ["purge"] = new[] { "help", "current", "set", "reset", "upgrade", "upgradeto" },
            ["meaculpa"] = new[] { "help", "current", "set", "reset", "upgrade", "upgradeto" },
            ["strength"] = new[] { "help", "current", "set", "reset", "upgrade", "upgradeto" },
            ["flaskhealth"] = new[] { "help", "current", "set", "reset", "upgrade", "upgradeto", "fill", "setmax" },
            ["teleport"] = new[] { "help", "list", "go", "showui", "unlock" },
            ["testplan"] = new[] { "help", "1" },
            ["tutorial"] = new[] { "list", "show" },
            ["relic"] = new[] { "help", "list", "listowned", "add", "remove", "equiped", "equip", "unequip" },
            ["questitem"] = new[] { "help", "list", "listowned", "add", "remove" },
            ["collectible"] = new[] { "help", "list", "listowned", "add", "remove" },
            ["bead"] = new[] { "help", "list", "listowned", "setslots", "add", "remove", "equiped", "equip", "unequip" },
            ["prayer"] = new[] { "help", "list", "listowned", "add", "remove", "equiped", "equip", "unequip", "decipher" },
            ["sword"] = new[] { "help", "list", "listowned", "add", "remove", "equiped", "equip", "unequip" },
            ["key"] = new[] { "help", "list", "add", "remove" },
            ["command"] = new[] { "help", "list", "refresh" }
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

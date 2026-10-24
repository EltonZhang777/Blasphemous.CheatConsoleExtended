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
    private static readonly Dictionary<string, Dictionary<string, ArgumentCountRange>> VanillaArgumentCounts = CreateVanillaArgumentCounts();
    private static readonly Func<string[], bool> VanillaArgumentValidator =
        CreateVanillaArgumentValidator(
            () => Core.InventoryManager.GetRosaryBeadSlots(),
            id => Core.AchievementsManager.Achievements.ContainsKey(id.ToUpperInvariant()),
            id => Core.LevelManager.DEBUG_GetAllLevelsName().Exists(levelName =>
                string.Equals(levelName, id, StringComparison.OrdinalIgnoreCase)),
            (district, zone) =>
            {
                var currentMap = Core.NewMapManager?.DEBUG_GetCurrentMap();
                if (currentMap == null)
                    return false;

                foreach (var cell in currentMap.Cells)
                {
                    if (cell.ZoneId.District == district
                        && (zone == null || cell.ZoneId.Zone == zone))
                    {
                        return true;
                    }
                }

                return false;
            },
            IsInventoryItemRegistered);

    /// <summary>Processes an input line and determines whether the original console handler should continue.</summary>
    /// <param name="console">The console receiving the command.</param>
    /// <param name="rawText">The unmodified command input.</param>
    /// <param name="resolvedCommand">The matching vanilla command, even when strict validation rejects its arguments.</param>
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
        resolvedCommand = command == null ? null : command.Value as ConsoleCommand;
        if (StrictCommandResolver.ShouldRunOriginal(rawText, catalog))
            return true;

        string invalidInventoryItemMessage = GetInvalidInventoryItemMessage(tokens, IsInventoryItemRegistered);
        if (invalidInventoryItemMessage != null)
        {
            console.Write(invalidInventoryItemMessage);
            return false;
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

    /// <summary>Runs only a registered vanilla command's supported, informational Help subcommand.</summary>
    internal static bool TryShowCommandHelp(ConsoleWidget console, string targetName)
    {
        if (console == null)
            return false;

        CommandDefinition target = StrictCommandResolver.FindSafeHelpCommand(targetName, CreateCatalog(console));
        if (target == null || target.Value is not ConsoleCommand command)
            return false;

        command.Execute(target.Name, ["help"]);
        return true;
    }

    /// <summary>Creates pre-dispatch checks that mirror the vanilla handlers' explicit value validation.</summary>
    internal static Func<string[], bool> CreateVanillaArgumentValidator(
        Func<int> getRosaryBeadSlots,
        Func<string, bool> achievementExists,
        Func<string, bool> levelExists,
        Func<string, string, bool> mapRevealTargetExists,
        Func<string, string, bool> inventoryItemExists)
    {
        if (getRosaryBeadSlots == null)
            throw new ArgumentNullException(nameof(getRosaryBeadSlots));
        if (achievementExists == null)
            throw new ArgumentNullException(nameof(achievementExists));
        if (levelExists == null)
            throw new ArgumentNullException(nameof(levelExists));
        if (mapRevealTargetExists == null)
            throw new ArgumentNullException(nameof(mapRevealTargetExists));
        if (inventoryItemExists == null)
            throw new ArgumentNullException(nameof(inventoryItemExists));

        return tokens =>
        {
            if (tokens == null || tokens.Length == 0)
                return false;

            if (GetInvalidInventoryItemMessage(tokens, inventoryItemExists) != null)
                return false;

            string command = tokens[0];
            string subcommand = tokens.Length > 1 ? tokens[1] : string.Empty;

            if (string.Equals(command, "command", StringComparison.OrdinalIgnoreCase) && tokens.Length > 2)
                return false;

            if ((string.Equals(command, "execution", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(command, "showcursor", StringComparison.OrdinalIgnoreCase))
                && tokens.Length == 2)
            {
                return string.Equals(tokens[1], "y", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(tokens[1], "n", StringComparison.OrdinalIgnoreCase);
            }

            if (string.Equals(command, "bossrush", StringComparison.OrdinalIgnoreCase)
                && string.Equals(subcommand, "start", StringComparison.OrdinalIgnoreCase)
                && tokens.Length == 4)
            {
                return CanParseEnum(typeof(BossRushManager.BossRushCourseId), tokens[2])
                    && CanParseEnum(typeof(BossRushManager.BossRushCourseMode), tokens[3]);
            }

            if (string.Equals(command, "gamemode", StringComparison.OrdinalIgnoreCase)
                && string.Equals(subcommand, "set", StringComparison.OrdinalIgnoreCase)
                && tokens.Length == 3)
            {
                string requestedName = tokens[2].ToUpperInvariant();
                foreach (string gameModeName in Enum.GetNames(typeof(GameModeManager.GAME_MODES)))
                {
                    if (gameModeName.Equals(requestedName))
                        return true;
                }
                return false;
            }

            if (string.Equals(command, "timescale", StringComparison.OrdinalIgnoreCase) && tokens.Length == 2)
            {
                return float.TryParse(tokens[1], out float timeScale)
                    && !float.IsNaN(timeScale)
                    && !float.IsInfinity(timeScale);
            }

            if (string.Equals(command, "achievement", StringComparison.OrdinalIgnoreCase)
                && tokens.Length >= 3
                && (string.Equals(subcommand, "check", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(subcommand, "grant", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(subcommand, "clear", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(subcommand, "clearsteam", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(subcommand, "addprogress", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(subcommand, "checkprogress", StringComparison.OrdinalIgnoreCase)))
            {
                return achievementExists(tokens[2])
                    && (!string.Equals(subcommand, "addprogress", StringComparison.OrdinalIgnoreCase)
                        || (tokens.Length == 4 && IsWithinRange(tokens[3], 0f, 100f)));
            }

            if ((string.Equals(command, "load", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(command, "loadnoui", StringComparison.OrdinalIgnoreCase))
                && tokens.Length == 2)
            {
                return tokens[1].Trim().Length > 0 && levelExists(tokens[1]);
            }

            if (string.Equals(command, "map", StringComparison.OrdinalIgnoreCase)
                && string.Equals(subcommand, "reveal", StringComparison.OrdinalIgnoreCase))
            {
                if (tokens.Length == 3)
                {
                    if (string.Equals(tokens[2], "all", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(tokens[2], "ng", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }

                    return mapRevealTargetExists(tokens[2], null);
                }

                return tokens.Length == 4 && mapRevealTargetExists(tokens[2], tokens[3]);
            }

            if (string.Equals(command, "audio", StringComparison.OrdinalIgnoreCase)
                && (string.Equals(subcommand, "master", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(subcommand, "music", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(subcommand, "sfx", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(subcommand, "voiceover", StringComparison.OrdinalIgnoreCase))
                && tokens.Length == 3)
            {
                return IsWithinRange(tokens[2], 0f, 1f);
            }

            if (string.Equals(command, "alms", StringComparison.OrdinalIgnoreCase)
                && (string.Equals(subcommand, "consume", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(subcommand, "set", StringComparison.OrdinalIgnoreCase))
                && tokens.Length == 3)
            {
                return IsWithinRange(tokens[2], 0, 99999);
            }

            if (string.Equals(command, "savegame", StringComparison.OrdinalIgnoreCase)
                && (string.Equals(subcommand, "load", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(subcommand, "save", StringComparison.OrdinalIgnoreCase))
                && tokens.Length == 3)
            {
                return IsWithinRange(tokens[2], 0, 10);
            }

            if (IsStatCommand(command) && tokens.Length == 3)
            {
                if (string.Equals(subcommand, "set", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(subcommand, "setmax", StringComparison.OrdinalIgnoreCase))
                {
                    return IsWithinRange(tokens[2], 0f, 99999f);
                }
                if (string.Equals(subcommand, "upgradeto", StringComparison.OrdinalIgnoreCase))
                    return IsWithinRange(tokens[2], 0, 9999);
            }

            if (string.Equals(command, "relic", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(subcommand, "equip", StringComparison.OrdinalIgnoreCase) && tokens.Length == 4)
                    return IsWithinRange(tokens[3], 0, 2);
                if (string.Equals(subcommand, "unequip", StringComparison.OrdinalIgnoreCase) && tokens.Length == 3)
                    return IsWithinRange(tokens[2], 0, 2);
            }

            if (string.Equals(command, "bead", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(subcommand, "setslots", StringComparison.OrdinalIgnoreCase) && tokens.Length == 3)
                    return IsWithinRange(tokens[2], 2, 7);
                if (string.Equals(subcommand, "equip", StringComparison.OrdinalIgnoreCase) && tokens.Length == 4)
                    return IsWithinRange(tokens[3], 0, getRosaryBeadSlots() - 1);
                if (string.Equals(subcommand, "unequip", StringComparison.OrdinalIgnoreCase) && tokens.Length == 3)
                    return IsWithinRange(tokens[2], 0, getRosaryBeadSlots() - 1);
            }

            if ((string.Equals(command, "sword", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(command, "prayer", StringComparison.OrdinalIgnoreCase))
                && string.Equals(subcommand, "equip", StringComparison.OrdinalIgnoreCase)
                && tokens.Length == 4)
            {
                return IsWithinRange(tokens[3], 0, 0);
            }

            if ((string.Equals(command, "sword", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(command, "prayer", StringComparison.OrdinalIgnoreCase))
                && string.Equals(subcommand, "unequip", StringComparison.OrdinalIgnoreCase)
                && tokens.Length == 3)
            {
                return IsWithinRange(tokens[2], 0, 0);
            }

            if (string.Equals(command, "prayer", StringComparison.OrdinalIgnoreCase)
                && string.Equals(subcommand, "decipher", StringComparison.OrdinalIgnoreCase)
                && tokens.Length == 4)
            {
                return IsWithinRange(tokens[3], 1, 20);
            }

            return true;
        };
    }

    /// <summary>Returns vanilla's invalid-ID message for a supported item route, or null when the route is valid or unrelated.</summary>
    internal static string GetInvalidInventoryItemMessage(
        string[] tokens,
        Func<string, string, bool> inventoryItemExists)
    {
        if (inventoryItemExists == null)
            throw new ArgumentNullException(nameof(inventoryItemExists));
        if (tokens == null || tokens.Length < 3)
            return null;

        string inventoryCategory;
        string displayCategory;
        bool supportsEquipment;
        bool supportsDecipher = false;
        switch (tokens[0].ToLowerInvariant())
        {
            case "relic":
                inventoryCategory = "relic";
                displayCategory = "relic";
                supportsEquipment = true;
                break;
            case "questitem":
                inventoryCategory = "questitem";
                displayCategory = "quest item";
                supportsEquipment = false;
                break;
            case "collectible":
                inventoryCategory = "collectible";
                displayCategory = "collectible item";
                supportsEquipment = false;
                break;
            case "bead":
                inventoryCategory = "bead";
                displayCategory = "rosary bead";
                supportsEquipment = true;
                break;
            case "prayer":
                inventoryCategory = "prayer";
                displayCategory = "prayer";
                supportsEquipment = true;
                supportsDecipher = true;
                break;
            case "sword":
                inventoryCategory = "sword";
                displayCategory = "sword";
                supportsEquipment = true;
                break;
            default:
                return null;
        }

        string subcommand = tokens[1];
        bool isAddOrRemove = string.Equals(subcommand, "add", StringComparison.OrdinalIgnoreCase)
            || string.Equals(subcommand, "remove", StringComparison.OrdinalIgnoreCase);
        bool isEquipmentRoute = supportsEquipment
            && string.Equals(subcommand, "equip", StringComparison.OrdinalIgnoreCase);
        bool isDecipherRoute = supportsDecipher
            && string.Equals(subcommand, "decipher", StringComparison.OrdinalIgnoreCase);

        if (isAddOrRemove)
        {
            if (tokens.Length != 3)
                return null;
            if (string.Equals(tokens[2], "all", StringComparison.OrdinalIgnoreCase))
                return null;
        }
        else if ((isEquipmentRoute || isDecipherRoute) && tokens.Length == 4)
        {
            // The inventory object ID is the first argument; the final argument is a slot or decipher value.
        }
        else
        {
            return null;
        }

        return inventoryItemExists(inventoryCategory, tokens[2])
            ? null
            : tokens[2] + " is not a valid " + displayCategory + " ID, please use list subcommand";
    }

    /// <summary>Looks up an item in the matching live inventory registry.</summary>
    private static bool IsInventoryItemRegistered(string category, string id)
    {
        switch (category)
        {
            case "relic":
                return Core.InventoryManager.GetRelic(id) != null;
            case "questitem":
                return Core.InventoryManager.GetQuestItem(id) != null;
            case "collectible":
                return Core.InventoryManager.GetCollectibleItem(id) != null;
            case "bead":
                return Core.InventoryManager.GetRosaryBead(id) != null;
            case "prayer":
                return Core.InventoryManager.GetPrayer(id) != null;
            case "sword":
                return Core.InventoryManager.GetSword(id) != null;
            default:
                return false;
        }
    }

    private static bool CanParseEnum(Type enumType, string value)
    {
        if (value.IndexOf(',') >= 0)
            return false;

        try
        {
            object parsedValue = Enum.Parse(enumType, value, true);
            return Enum.IsDefined(enumType, parsedValue);
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    private static bool IsWithinRange(string value, int minimum, int maximum)
    {
        return int.TryParse(value, out int parsed) && parsed >= minimum && parsed <= maximum;
    }

    private static bool IsWithinRange(string value, float minimum, float maximum)
    {
        return float.TryParse(value, out float parsed) && parsed >= minimum && parsed <= maximum;
    }

    private static bool IsStatCommand(string command)
    {
        return string.Equals(command, "health", StringComparison.OrdinalIgnoreCase)
            || string.Equals(command, "flask", StringComparison.OrdinalIgnoreCase)
            || string.Equals(command, "fervour", StringComparison.OrdinalIgnoreCase)
            || string.Equals(command, "flaskhealth", StringComparison.OrdinalIgnoreCase)
            || string.Equals(command, "purge", StringComparison.OrdinalIgnoreCase)
            || string.Equals(command, "meaculpa", StringComparison.OrdinalIgnoreCase)
            || string.Equals(command, "strength", StringComparison.OrdinalIgnoreCase);
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

                    VanillaArgumentCounts.TryGetValue(name, out Dictionary<string, ArgumentCountRange> argumentCounts);
                    catalog.Add(new CommandDefinition(
                        name,
                        subcommands,
                        command,
                        argumentCounts,
                        string.Equals(name, "command", StringComparison.OrdinalIgnoreCase),
                        argumentCounts == null ? null : VanillaArgumentValidator));
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
                {
                    catalog.Add(new CommandDefinition(command.Id, null, command));
                }
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
            ["camera"] = ["help", "all", "game", "scene", "ui", "virtual"],
            ["completion"] = ["help", "base", "get", "show", "ng+"],
            ["graybox"] = ["show", "hide"],
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
            ["tutorial"] = ["help", "list", "show"],
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

    /// <summary>Creates the vanilla command argument counts from each handler's accepted input shapes.</summary>
    private static Dictionary<string, Dictionary<string, ArgumentCountRange>> CreateVanillaArgumentCounts()
    {
        var counts = new Dictionary<string, Dictionary<string, ArgumentCountRange>>(StringComparer.OrdinalIgnoreCase);

        AddArgumentCounts(counts, ["invincible", "fervourrefill", "restart", "maxfervour", "exit"], Exact("", 0));
        AddArgumentCounts(counts, ["load"], Exact("", 1));
        AddArgumentCounts(counts, ["loadmenu"], Exact("", 0));
        AddArgumentCounts(counts, ["loadnoui"], Exact("", 1));
        AddArgumentCounts(counts, ["execution", "timescale", "showcursor"], Exact("", 1));
        AddArgumentCounts(counts, ["help"], Range("", 0, 1));
        AddArgumentCounts(counts, ["sendevent"], Range("", 0, 1));
        AddArgumentCounts(counts, ["kill"], Range("", 0, 2));

        AddArgumentCounts(counts, ["achievement"],
            Exact("help", 0), Exact("enablepopup", 0), Exact("disablepopup", 0), Exact("check", 1),
            Exact("grant", 1), Exact("clear", 1), Exact("clearsteam", 1), Exact("clearall", 0),
            Exact("clearsteamall", 0), Exact("addprogress", 2), Exact("checkprogress", 1));
        AddArgumentCounts(counts, ["alms"],
            Exact("help", 0), Exact("current", 0), Exact("list", 0), Exact("consume", 1), Exact("set", 1));
        AddArgumentCounts(counts, ["audio"],
            Exact("help", 0), Exact("list", 0), Exact("master", 1), Exact("music", 1), Exact("sfx", 1), Exact("voiceover", 1));
        AddArgumentCounts(counts, ["bonus"], Exact("help", 0), Exact("list", 0));
        AddArgumentCounts(counts, ["bossrush"],
            Exact("help", 0), Exact("start", 2), Exact("hub", 0), Exact("next", 0), Exact("golast", 0),
            Exact("end", 0), Exact("printscore", 0), Exact("unlock", 1));
        AddArgumentCounts(counts, ["camera"],
            Exact("help", 0), Exact("all", 0), Exact("game", 0), Exact("scene", 0), Exact("ui", 0), Exact("virtual", 0));
        AddArgumentCounts(counts, ["completion"],
            Exact("help", 0), Exact("base", 0), Exact("get", 0), Exact("show", 0), Exact("ng+", 0));
        AddArgumentCounts(counts, ["debug"], Exact("help", 0), Exact("list", 0), Exact("off", 1), Exact("on", 1));
        AddArgumentCounts(counts, ["show_debug_ui", "showui"],
            Exact("help", 0), Exact("current", 0), Exact("off", 0), Exact("on", 0));
        AddArgumentCounts(counts, ["demake"], Exact("help", 0), Exact("enter", 0));
        AddArgumentCounts(counts, ["dialog"], Exact("help", 0), Exact("list", 0), Exact("start", 1));
        AddArgumentCounts(counts, ["flag"], Exact("help", 0), Exact("set", 1), Exact("clear", 1), Exact("test", 1));
        AddArgumentCounts(counts, ["gamemode"], Exact("help", 0), Exact("list", 0), Exact("current", 0), Exact("set", 1));
        AddArgumentCounts(counts, ["guilt"], Exact("help", 0), Exact("get", 0), Exact("reset", 0), Exact("add", 0));
        AddArgumentCounts(counts, ["graybox"], Exact("help", 0), Exact("show", 0), Exact("hide", 0));
        AddArgumentCounts(counts, ["language"], Exact("help", 0), Exact("list", 0), Exact("current", 0), Exact("set", 1));
        AddArgumentCounts(counts, ["map"],
            Exact("help", 0), Exact("list", 0), Exact("set", 1), Exact("secrets", 0), Exact("secret", 2),
            Exact("unrevealed", 0), Range("reveal", 1, 2));
        AddArgumentCounts(counts, ["miriam"],
            Exact("help", 0), Exact("status", 0), Exact("start", 0), Exact("end", 0),
            Exact("activateportal", 0), Exact("deactivateportal", 0), Exact("gotogoal", 0));
        AddArgumentCounts(counts, ["penitence"],
            Exact("help", 0), Exact("current", 0), Exact("activate", 1), Exact("deactivate", 0),
            Exact("abandon", 0), Exact("complete", 0), Exact("listall", 0), Exact("listabandoned", 0), Exact("listcompleted", 0));
        AddArgumentCounts(counts, ["savegame"],
            Exact("help", 0), Exact("load", 1), Exact("save", 1), Exact("enablenewgameplus", 0));
        AddArgumentCounts(counts, ["skill"],
            Exact("help", 0), Exact("list", 0), Exact("unlock", 1), Exact("unlockall", 0),
            Exact("lock", 1), Exact("lockall", 0), Exact("showui", 0));
        AddArgumentCounts(counts, ["skin"],
            Exact("help", 0), Exact("list", 0), Exact("listunlocked", 0), Exact("get", 0),
            Exact("set", 1), Exact("unlock", 1), Exact("lock", 1));

        string[] variableStats = ["health", "flask", "fervour", "flaskhealth"];
        AddArgumentCounts(counts, variableStats,
            Exact("help", 0), Exact("current", 0), Exact("set", 1), Exact("reset", 0),
            Exact("upgrade", 0), Exact("upgradeto", 1), Exact("fill", 0), Exact("setmax", 1));
        AddArgumentCounts(counts, ["purge", "meaculpa", "strength"],
            Exact("help", 0), Exact("current", 0), Exact("set", 1), Exact("reset", 0),
            Exact("upgrade", 0), Exact("upgradeto", 1));
        AddArgumentCounts(counts, ["teleport"],
            Exact("help", 0), Exact("list", 0), Exact("go", 1), Exact("showui", 0), Exact("unlock", 1));
        AddArgumentCounts(counts, ["testplan"], Exact("help", 0), Exact("1", 0));
        AddArgumentCounts(counts, ["tutorial"], Exact("help", 0), Exact("list", 0), Exact("show", 1));

        AddArgumentCounts(counts, ["relic"], InventoryRules(true, true, false, false));
        AddArgumentCounts(counts, ["questitem", "collectible"], InventoryRules(false, false, false, false));
        AddArgumentCounts(counts, ["bead"], InventoryRules(true, true, true, false));
        AddArgumentCounts(counts, ["prayer"], InventoryRules(true, true, false, true));
        AddArgumentCounts(counts, ["sword"], InventoryRules(true, true, false, false));
        AddArgumentCounts(counts, ["key"], Exact("help", 0), Exact("list", 0), Exact("add", 1), Exact("remove", 1));
        AddArgumentCounts(counts, ["invtest"], Exact("", 0));
        AddArgumentCounts(counts, ["command"], Exact("help", 0), Exact("list", 0), Exact("refresh", 0));

        return counts;
    }

    /// <summary>Builds the common inventory argument routes, adding alias-specific options where needed.</summary>
    private static ArgumentCountRule[] InventoryRules(bool supportsEquipment, bool hasEquippedList, bool setSlots, bool decipher)
    {
        List<ArgumentCountRule> rules =
        [
            Exact("help", 0), Exact("list", 0), Exact("listowned", 0), Exact("add", 1), Exact("remove", 1)
        ];
        if (supportsEquipment)
        {
            rules.Add(Exact("equip", 2));
            rules.Add(Exact("unequip", 1));
        }
        if (hasEquippedList)
            rules.Add(Exact("equiped", 0));
        if (setSlots)
            rules.Add(Exact("setslots", 1));
        if (decipher)
            rules.Add(Exact("decipher", 2));
        return rules.ToArray();
    }

    /// <summary>Adds the supplied argument routes for one or more vanilla command aliases.</summary>
    private static void AddArgumentCounts(
        Dictionary<string, Dictionary<string, ArgumentCountRange>> target,
        string[] commandNames,
        params ArgumentCountRule[] rules)
    {
        foreach (string commandName in commandNames)
        {
            var counts = new Dictionary<string, ArgumentCountRange>(StringComparer.OrdinalIgnoreCase);
            foreach (ArgumentCountRule rule in rules)
                counts.Add(rule.Route, new ArgumentCountRange(rule.Minimum, rule.Maximum));
            target.Add(commandName, counts);
        }
    }

    private static ArgumentCountRule Exact(string route, int count) => new(route, count, count);

    private static ArgumentCountRule Range(string route, int minimum, int maximum) => new(route, minimum, maximum);

    private sealed class ArgumentCountRule
    {
        public ArgumentCountRule(string route, int minimum, int maximum)
        {
            Route = route;
            Minimum = minimum;
            Maximum = maximum;
        }

        public string Route { get; }

        public int Minimum { get; }

        public int Maximum { get; }
    }
}

/// <summary>Routes console input through strict command resolution before the vanilla handler runs.</summary>
[HarmonyPatch(typeof(ConsoleWidget), nameof(ConsoleWidget.ProcessCommand))]
internal static class ConsoleWidget_ProcessCommand_Patch
{
    /// <summary>Handles the input through the strict resolver and controls whether the original method runs.</summary>
    /// <returns><see langword="true"/> to run the original method; otherwise, <see langword="false"/>.</returns>
    private static bool Prefix(ConsoleWidget __instance, string rawText)
    {
        return StrictCommandRuntime.Process(__instance, rawText, out _);
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

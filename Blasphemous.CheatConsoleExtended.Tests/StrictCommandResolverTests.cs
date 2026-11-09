using Framework.Managers;
using System;
using System.Collections.Generic;
using System.Globalization;
using Xunit;

namespace Blasphemous.CheatConsoleExtended.Tests;

public sealed class StrictCommandResolverTests
{
    [Fact]
    public void MatchesTopLevelAndSubcommandNamesExactly()
    {
        List<CommandDefinition> commands = CreateSuggestionCatalog();

        Assert.Equal("exit", StrictCommandResolver.FindExact("EXIT", commands).Name);
        Assert.Null(StrictCommandResolver.FindExact("in", commands));
        Assert.Equal("master", StrictCommandResolver.FindExactSubcommand(commands[2], "MASTER"));
        Assert.Null(StrictCommandResolver.FindExactSubcommand(commands[2], "mas"));

        List<CommandDefinition> sharedCommands = [new CommandDefinition("grant_relic")];
        CommandDefinition commandWithSharedIds = new("command", ["help", "list", "refresh", "grant_relic"]);
        Assert.Equal("grant_relic", StrictCommandResolver.FindExact("GRANT_RELIC", sharedCommands).Name);
        Assert.Null(StrictCommandResolver.FindExact("grant", sharedCommands));
        Assert.Equal("grant_relic", StrictCommandResolver.FindExactSubcommand(commandWithSharedIds, "GRANT_RELIC"));
        Assert.Null(StrictCommandResolver.FindExactSubcommand(commandWithSharedIds, "grant"));

        sharedCommands[0] = new CommandDefinition("new_shared_id");
        Assert.Equal("new_shared_id", StrictCommandResolver.FindExact("NEW_SHARED_ID", sharedCommands).Name);
        Assert.Null(StrictCommandResolver.FindExact("grant_relic", sharedCommands));
    }

    [Fact]
    public void SuggestsClosestNamesWithinConfiguredDistance()
    {
        List<CommandDefinition> commands = CreateSuggestionCatalog();

        Assert.Equal(new[] { "cat", "cut" }, StrictCommandResolver.GetSuggestions("cot", commands, 1));
        Assert.Equal("Did you mean: cat, cut?", StrictCommandResolver.FormatSuggestionLine(StrictCommandResolver.GetSuggestions("cot", commands, 1)));
        Assert.Empty(StrictCommandResolver.GetSuggestions("zzzz", commands, 2));
        Assert.Null(StrictCommandResolver.FormatSuggestionLine(StrictCommandResolver.GetSuggestions("zzzz", commands, 2)));
        Assert.Empty(StrictCommandResolver.GetSuggestions("cot", commands, 0));
        Assert.Equal(new[] { "ab" }, StrictCommandResolver.GetSuggestions("ba", commands, 1));
        Assert.Equal(0, StrictCommandResolver.NormalizeDistance(-1));

        Assert.Equal(new[] { "audio master" }, StrictCommandResolver.GetSubcommandSuggestions(commands[2], "mastr", 1));
        Assert.Equal(new[] { "audio", "audio master" }, StrictCommandResolver.GetHierarchicalSuggestions("audo", "mastr", commands, 2));
    }

    [Fact]
    public void TokenizesLikeVanillaParserAndRejectsPrefixMatches()
    {
        Assert.Equal(["", "", "AUDIO\t", "master", "", ""], StrictCommandResolver.Tokenize("  AUDIO\t master  "));
        Assert.Equal(["clear", ""], StrictCommandResolver.Tokenize("clear "));
        Assert.Equal(["", "invincible"], StrictCommandResolver.Tokenize(" invincible"));
        Assert.Empty(StrictCommandResolver.Tokenize(" \t\r "));

        List<CommandDefinition> sourceCatalog =
        [
            new CommandDefinition("exit"),
            new CommandDefinition("load"),
            new CommandDefinition("loadmenu"),
            new CommandDefinition("health"),
            new CommandDefinition("invincible"),
            new CommandDefinition("invtest"),
            new CommandDefinition("audio", ["help", "master", "music"]),
            new CommandDefinition("demo_extension", ["help", "ping"]),
            new CommandDefinition("shared_sequence"),
            new CommandDefinition("clear"),
            new CommandDefinition("cls"),
            new CommandDefinition("mirrorlog")
        ];

        foreach (CommandDefinition sourceCommand in sourceCatalog)
        {
            Assert.Same(sourceCommand, StrictCommandResolver.FindExact(sourceCommand.Name.ToUpperInvariant(), sourceCatalog));
        }

        Assert.Null(StrictCommandResolver.FindExact("e", sourceCatalog));
        Assert.Null(StrictCommandResolver.FindExact("in", sourceCatalog));
        Assert.Null(StrictCommandResolver.FindExact("a", sourceCatalog));
        Assert.Null(StrictCommandResolver.FindExact("invte", sourceCatalog));

        string[] commandWithParameter = StrictCommandResolver.Tokenize("AUDIO MASTER 1");
        CommandDefinition audio = StrictCommandResolver.FindExact(commandWithParameter[0], sourceCatalog);
        Assert.NotNull(audio);
        Assert.Equal("master", StrictCommandResolver.FindExactSubcommand(audio, commandWithParameter[1]));
    }

    [Fact]
    public void UsesDefaultSuggestionDistanceAndNormalizesInvalidConfig()
    {
        MasterConfig constructorDefaults = new();
        MasterConfig defaults = MasterConfig.CreateDefault();

        Assert.Equal(2, constructorDefaults.CommandSuggestionDistance);
        Assert.Equal(2, defaults.CommandSuggestionDistance);
        Assert.Equal(MasterConfig.DefaultConsoleFont, defaults.ConsoleFont);
        Assert.Equal(MasterConfig.DefaultTopLevelCommandColor, defaults.TopLevelCommandColor);
        Assert.Equal(MasterConfig.DefaultSubcommandColor, defaults.SubcommandColor);
        Assert.Equal(MasterConfig.DefaultParameterColor, defaults.ParameterColor);
        Assert.Equal(MasterConfig.DefaultOutputColor, defaults.OutputColor);

        MasterConfig config = new()
        {
            CommandSuggestionDistance = -5,
            TopLevelCommandColor = "#a1b2c3",
            SubcommandColor = "not-a-color",
            ParameterColor = 42,
            OutputColor = null
        };
        config.Normalize();

        Assert.Equal(0, config.CommandSuggestionDistance);
        Assert.Equal("#A1B2C3", config.TopLevelCommandColor);
        Assert.Equal(MasterConfig.DefaultSubcommandColor, config.SubcommandColor);
        Assert.Equal(MasterConfig.DefaultParameterColor, config.ParameterColor);
        Assert.Equal(MasterConfig.DefaultOutputColor, config.OutputColor);
    }

    [Fact]
    public void RunsOnlyExactValidVanillaCommands()
    {
        List<CommandDefinition> boundaryCatalog = CreateBoundaryCatalog();

        Assert.True(StrictCommandResolver.ShouldRunOriginal(StrictCommandResolver.Tokenize("AUDIO MASTER MiXeD"), boundaryCatalog));
        Assert.False(StrictCommandResolver.ShouldRunOriginal(StrictCommandResolver.Tokenize("aud"), boundaryCatalog));
        Assert.False(StrictCommandResolver.ShouldRunOriginal(StrictCommandResolver.Tokenize("AUDIO mas"), boundaryCatalog));
        Assert.False(StrictCommandResolver.ShouldRunOriginal(StrictCommandResolver.Tokenize("clear"), boundaryCatalog));
        Assert.False(StrictCommandResolver.ShouldRunOriginal(StrictCommandResolver.Tokenize("clear now"), boundaryCatalog));
    }

    [Fact]
    public void DispatchBoundaryInvokesAllowedHandlerOnceAndLeavesRejectedInputUntouched()
    {
        int handlerCalls = 0;
        int sideEffects = 0;
        Action invincibleHandler = () =>
        {
            handlerCalls++;
            sideEffects++;
        };
        List<CommandDefinition> commands =
        [
            new CommandDefinition("invincible", null, invincibleHandler, Counts(("", 0, 0))),
            new CommandDefinition("invtest", null, new Action(() => { }), Counts(("", 0, 0)))
        ];

        void Dispatch(string rawText)
        {
            if (!StrictCommandResolver.ShouldRunOriginal(rawText, commands))
                return;

            string[] tokens = StrictCommandResolver.Tokenize(rawText);
            Action handler = (Action)StrictCommandResolver.FindExact(tokens[0], commands).Value;
            handler();
        }

        Dispatch("invincible");
        Assert.Equal(1, handlerCalls);
        Assert.Equal(1, sideEffects);

        foreach (string rejectedInput in new[] { "inv", " invincible", "invincible ", "invincible extra" })
        {
            Dispatch(rejectedInput);
            Assert.Equal(1, handlerCalls);
            Assert.Equal(1, sideEffects);
        }
    }

    [Fact]
    public void ValidatesTopLevelAndSubcommandArgumentCounts()
    {
        List<CommandDefinition> commands =
        [
            new CommandDefinition("invtest", null, new object(), Counts(("", 0, 0))),
            new CommandDefinition("health", ["help", "current", "set"], new object(), Counts(("help", 0, 0), ("current", 0, 0), ("set", 1, 1)))
        ];

        Assert.True(StrictCommandResolver.ShouldRunOriginal(StrictCommandResolver.Tokenize("INVTEST"), commands));
        Assert.False(StrictCommandResolver.ShouldRunOriginal(StrictCommandResolver.Tokenize("invtest abc def"), commands));
        Assert.True(StrictCommandResolver.ShouldRunOriginal(StrictCommandResolver.Tokenize("HEALTH SET 10"), commands));
        Assert.False(StrictCommandResolver.ShouldRunOriginal(StrictCommandResolver.Tokenize("health set"), commands));
        Assert.False(StrictCommandResolver.ShouldRunOriginal(StrictCommandResolver.Tokenize("health set 10 extra"), commands));
    }

    [Fact]
    public void RejectsInvalidMapSecretSwitchBeforeDispatch()
    {
        List<CommandDefinition> commands =
        [
            new CommandDefinition("map", ["help", "secret"], new object(), Counts(("help", 0, 0), ("secret", 2, 2)))
        ];

        foreach (string acceptedSwitch in new[] { "ON", "OFF", "TRUE", "FALSE" })
            Assert.True(StrictCommandResolver.ShouldRunOriginal("map secret SecretId " + acceptedSwitch, commands));

        Assert.False(StrictCommandResolver.ShouldRunOriginal("map secret SecretId invalid", commands));
    }

    [Fact]
    public void ValidatesFiniteTokensEnumsAndGameModeNames()
    {
        Assert.True(ShouldRunVanillaRoute("execution y", "execution", null, ("", 1, 1)));
        Assert.True(ShouldRunVanillaRoute("execution N", "execution", null, ("", 1, 1)));
        Assert.False(ShouldRunVanillaRoute("execution yes", "execution", null, ("", 1, 1)));
        Assert.True(ShouldRunVanillaRoute("showcursor Y", "showcursor", null, ("", 1, 1)));
        Assert.True(ShouldRunVanillaRoute("showcursor n", "showcursor", null, ("", 1, 1)));
        Assert.False(ShouldRunVanillaRoute("showcursor true", "showcursor", null, ("", 1, 1)));

        Type courseType = typeof(BossRushManager.BossRushCourseId);
        Type modeType = typeof(BossRushManager.BossRushCourseMode);
        string[] courseNames = Enum.GetNames(courseType);
        string[] modeNames = Enum.GetNames(modeType);
        Assert.NotEmpty(courseNames);
        Assert.NotEmpty(modeNames);

        foreach (string courseName in courseNames)
        {
            foreach (string modeName in modeNames)
            {
                Assert.True(ShouldRunVanillaRoute(
                    "bossrush start " + courseName.ToUpperInvariant() + " " + modeName.ToLowerInvariant(),
                    "bossrush", ["help", "start"], ("help", 0, 0), ("start", 2, 2)));
            }
        }

        foreach (object courseValue in Enum.GetValues(courseType))
        {
            string courseNumber = Convert.ToInt64(courseValue, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
            foreach (object modeValue in Enum.GetValues(modeType))
            {
                string modeNumber = Convert.ToInt64(modeValue, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
                Assert.True(ShouldRunVanillaRoute("bossrush start " + courseNumber + " " + modeNumber,
                    "bossrush", ["help", "start"], ("help", 0, 0), ("start", 2, 2)));
            }
        }

        Assert.False(ShouldRunVanillaRoute("bossrush start COURSE_A_1,COURSE_A_2 NORMAL",
            "bossrush", ["help", "start"], ("help", 0, 0), ("start", 2, 2)));
        Assert.False(ShouldRunVanillaRoute("bossrush start 2147483647 0",
            "bossrush", ["help", "start"], ("help", 0, 0), ("start", 2, 2)));
        Assert.False(ShouldRunVanillaRoute("bossrush start COURSE_A_1 2147483647",
            "bossrush", ["help", "start"], ("help", 0, 0), ("start", 2, 2)));
        Assert.False(ShouldRunVanillaRoute("bossrush start COURSE_A_2,COURSE_A_3 NORMAL",
            "bossrush", ["help", "start"], ("help", 0, 0), ("start", 2, 2)));
        Assert.False(ShouldRunVanillaRoute("bossrush start unknown " + modeNames[0],
            "bossrush", ["help", "start"], ("help", 0, 0), ("start", 2, 2)));
        Assert.False(ShouldRunVanillaRoute("bossrush start 2147483648 0",
            "bossrush", ["help", "start"], ("help", 0, 0), ("start", 2, 2)));

        string gameMode = Enum.GetNames(typeof(GameModeManager.GAME_MODES))[0];
        Assert.True(ShouldRunVanillaRoute("gamemode set " + gameMode.ToLowerInvariant(),
            "gamemode", ["help", "set"], ("help", 0, 0), ("set", 1, 1)));
        Assert.False(ShouldRunVanillaRoute("gamemode set 0",
            "gamemode", ["help", "set"], ("help", 0, 0), ("set", 1, 1)));
        Assert.False(ShouldRunVanillaRoute("gamemode set not_a_game_mode",
            "gamemode", ["help", "set"], ("help", 0, 0), ("set", 1, 1)));
    }

    [Fact]
    public void ValidatesNumericParsingAndOriginalNumericBoundaries()
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");

            Assert.True(ShouldRunVanillaRoute("achievement addprogress arbitrary-id 0",
                "achievement", ["addprogress"], ("addprogress", 2, 2)));
            Assert.True(ShouldRunVanillaRoute("achievement addprogress arbitrary-id 100",
                "achievement", ["addprogress"], ("addprogress", 2, 2)));
            Assert.True(ShouldRunVanillaRoute("achievement addprogress arbitrary-id 42,5",
                "achievement", ["addprogress"], ("addprogress", 2, 2)));
            Assert.False(ShouldRunVanillaRoute("achievement addprogress arbitrary-id -0,1",
                "achievement", ["addprogress"], ("addprogress", 2, 2)));
            Assert.False(ShouldRunVanillaRoute("achievement addprogress arbitrary-id 100,1",
                "achievement", ["addprogress"], ("addprogress", 2, 2)));
            Assert.False(ShouldRunVanillaRoute("achievement addprogress arbitrary-id NaN",
                "achievement", ["addprogress"], ("addprogress", 2, 2)));
            Assert.False(ShouldRunVanillaRoute("achievement addprogress arbitrary-id not-a-number",
                "achievement", ["addprogress"], ("addprogress", 2, 2)));

            Assert.True(ShouldRunVanillaRoute("timescale 2,5", "timescale", null, ("", 1, 1)));
            Assert.True(ShouldRunVanillaRoute("timescale 1e20", "timescale", null, ("", 1, 1)));
            Assert.False(ShouldRunVanillaRoute("timescale invalid", "timescale", null, ("", 1, 1)));
            Assert.False(ShouldRunVanillaRoute("timescale NaN", "timescale", null, ("", 1, 1)));
            Assert.False(ShouldRunVanillaRoute("timescale Infinity", "timescale", null, ("", 1, 1)));
            Assert.False(ShouldRunVanillaRoute("timescale -Infinity", "timescale", null, ("", 1, 1)));
            Assert.True(ShouldRunVanillaRoute("load scene_alpha", "load", null, ("", 1, 1)));
            Assert.False(ShouldRunVanillaRoute("load ", "load", null, ("", 1, 1)));
            Assert.True(ShouldRunVanillaRoute("loadnoui scene_alpha", "loadnoui", null, ("", 1, 1)));
            Assert.False(ShouldRunVanillaRoute("loadnoui ", "loadnoui", null, ("", 1, 1)));
            Assert.True(ShouldRunVanillaRoute("audio master 0,0", "audio", ["master"], ("master", 1, 1)));
            Assert.True(ShouldRunVanillaRoute("audio master 1,0", "audio", ["master"], ("master", 1, 1)));
            Assert.False(ShouldRunVanillaRoute("audio master 1,1", "audio", ["master"], ("master", 1, 1)));
            Assert.False(ShouldRunVanillaRoute("audio master invalid", "audio", ["master"], ("master", 1, 1)));
            Assert.False(ShouldRunVanillaRoute("audio master NaN", "audio", ["master"], ("master", 1, 1)));
            foreach (string audioSubcommand in new[] { "music", "sfx", "voiceover" })
            {
                Assert.True(ShouldRunVanillaRoute("audio " + audioSubcommand + " 0,5", "audio",
                    [audioSubcommand], (audioSubcommand, 1, 1)));
            }
            Assert.True(ShouldRunVanillaRoute("alms set 0", "alms", ["set"], ("set", 1, 1)));
            Assert.True(ShouldRunVanillaRoute("alms consume 99999", "alms", ["consume"], ("consume", 1, 1)));
            Assert.False(ShouldRunVanillaRoute("alms set -1", "alms", ["set"], ("set", 1, 1)));
            Assert.False(ShouldRunVanillaRoute("alms set 100000", "alms", ["set"], ("set", 1, 1)));
            Assert.False(ShouldRunVanillaRoute("alms set 1,5", "alms", ["set"], ("set", 1, 1)));

            Assert.True(ShouldRunVanillaRoute("savegame load 0", "savegame", ["load"], ("load", 1, 1)));
            Assert.True(ShouldRunVanillaRoute("savegame save 10", "savegame", ["save"], ("save", 1, 1)));
            Assert.False(ShouldRunVanillaRoute("savegame load -1", "savegame", ["load"], ("load", 1, 1)));
            Assert.False(ShouldRunVanillaRoute("savegame save 11", "savegame", ["save"], ("save", 1, 1)));

            foreach (string stat in new[] { "health", "flask", "fervour", "flaskhealth", "purge", "meaculpa", "strength" })
            {
                Assert.True(ShouldRunVanillaRoute(stat + " set 99999", stat, ["set", "setmax", "upgradeto"],
                    ("set", 1, 1), ("setmax", 1, 1), ("upgradeto", 1, 1)));
                Assert.True(ShouldRunVanillaRoute(stat + " setmax 0", stat, ["set", "setmax", "upgradeto"],
                    ("set", 1, 1), ("setmax", 1, 1), ("upgradeto", 1, 1)));
                Assert.False(ShouldRunVanillaRoute(stat + " set -0,1", stat, ["set", "setmax", "upgradeto"],
                    ("set", 1, 1), ("setmax", 1, 1), ("upgradeto", 1, 1)));
                Assert.False(ShouldRunVanillaRoute(stat + " set NaN", stat, ["set", "setmax", "upgradeto"],
                    ("set", 1, 1), ("setmax", 1, 1), ("upgradeto", 1, 1)));
                Assert.False(ShouldRunVanillaRoute(stat + " setmax 100000", stat, ["set", "setmax", "upgradeto"],
                    ("set", 1, 1), ("setmax", 1, 1), ("upgradeto", 1, 1)));
                Assert.True(ShouldRunVanillaRoute(stat + " upgradeto 9999", stat, ["set", "setmax", "upgradeto"],
                    ("set", 1, 1), ("setmax", 1, 1), ("upgradeto", 1, 1)));
                Assert.False(ShouldRunVanillaRoute(stat + " upgradeto 10000", stat, ["set", "setmax", "upgradeto"],
                    ("set", 1, 1), ("setmax", 1, 1), ("upgradeto", 1, 1)));
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void ValidatesInventorySlotsWhileAllowingRegisteredDynamicIds()
    {
        string[] inventoryRoutes = ["help", "equip", "unequip", "setslots", "decipher"];
        (string Subcommand, int Minimum, int Maximum)[] inventoryCounts =
        [
            ("help", 0, 0), ("equip", 2, 2), ("unequip", 1, 1), ("setslots", 1, 1), ("decipher", 2, 2)
        ];

        Assert.True(ShouldRunVanillaRoute("relic equip not-a-checked-id 0", "relic", inventoryRoutes, inventoryCounts));
        Assert.True(ShouldRunVanillaRoute("relic equip not-a-checked-id 2", "relic", inventoryRoutes, inventoryCounts));
        Assert.False(ShouldRunVanillaRoute("relic equip not-a-checked-id 3", "relic", inventoryRoutes, inventoryCounts));
        Assert.True(ShouldRunVanillaRoute("relic unequip 2", "relic", inventoryRoutes, inventoryCounts));
        Assert.False(ShouldRunVanillaRoute("relic unequip -1", "relic", inventoryRoutes, inventoryCounts));

        Assert.True(ShouldRunVanillaRoute("bead setslots 2", "bead", inventoryRoutes, inventoryCounts));
        Assert.True(ShouldRunVanillaRoute("bead setslots 7", "bead", inventoryRoutes, inventoryCounts));
        Assert.False(ShouldRunVanillaRoute("bead setslots 1", "bead", inventoryRoutes, inventoryCounts));
        Assert.False(ShouldRunVanillaRoute("bead setslots 8", "bead", inventoryRoutes, inventoryCounts));
        Assert.True(ShouldRunVanillaRoute("bead equip arbitrary-id 6", "bead", inventoryRoutes, inventoryCounts));
        Assert.False(ShouldRunVanillaRoute("bead equip arbitrary-id 7", "bead", inventoryRoutes, inventoryCounts));
        Assert.True(ShouldRunVanillaRoute("bead unequip 0", "bead", inventoryRoutes, inventoryCounts));
        Assert.False(ShouldRunVanillaRoute("bead unequip 7", "bead", inventoryRoutes, inventoryCounts));

        Assert.True(ShouldRunVanillaRoute("sword equip arbitrary-id 0", "sword", inventoryRoutes, inventoryCounts));
        Assert.False(ShouldRunVanillaRoute("sword equip arbitrary-id 1", "sword", inventoryRoutes, inventoryCounts));
        Assert.True(ShouldRunVanillaRoute("sword unequip 0", "sword", inventoryRoutes, inventoryCounts));
        Assert.False(ShouldRunVanillaRoute("sword unequip 1", "sword", inventoryRoutes, inventoryCounts));

        Assert.True(ShouldRunVanillaRoute("prayer equip arbitrary-id 0", "prayer", inventoryRoutes, inventoryCounts));
        Assert.False(ShouldRunVanillaRoute("prayer equip arbitrary-id 1", "prayer", inventoryRoutes, inventoryCounts));
        Assert.True(ShouldRunVanillaRoute("prayer unequip 0", "prayer", inventoryRoutes, inventoryCounts));
        Assert.False(ShouldRunVanillaRoute("prayer unequip 1", "prayer", inventoryRoutes, inventoryCounts));
        Assert.True(ShouldRunVanillaRoute("prayer decipher arbitrary-id 20", "prayer", inventoryRoutes, inventoryCounts));
        Assert.False(ShouldRunVanillaRoute("prayer decipher arbitrary-id 21", "prayer", inventoryRoutes, inventoryCounts));
        Assert.False(ShouldRunVanillaRoute("prayer decipher arbitrary-id 0", "prayer", inventoryRoutes, inventoryCounts));
    }

    [Fact]
    public void ValidatesInventoryIdsAgainstCategoryLookupsBeforeOriginalDispatch()
    {
        Dictionary<string, HashSet<string>> registeredItems = new(StringComparer.OrdinalIgnoreCase)
        {
            ["relic"] = new HashSet<string>(["RE01", "RE401"], StringComparer.OrdinalIgnoreCase),
            ["questitem"] = new HashSet<string>(["QI01"], StringComparer.OrdinalIgnoreCase),
            ["collectible"] = new HashSet<string>(["CO01"], StringComparer.OrdinalIgnoreCase),
            ["bead"] = new HashSet<string>(["RB01"], StringComparer.OrdinalIgnoreCase),
            ["prayer"] = new HashSet<string>(["PR01"], StringComparer.OrdinalIgnoreCase),
            ["sword"] = new HashSet<string>(["SW01"], StringComparer.OrdinalIgnoreCase)
        };
        bool inventoryItemExists(string category, string id) =>
            registeredItems.TryGetValue(category, out HashSet<string> ids) && ids.Contains(id);
        Func<string[], bool> validator = CreateTestVanillaArgumentValidator(inventoryItemExists);

        bool ShouldRun(string input, string commandName, string[] subcommands, params (string Subcommand, int Minimum, int Maximum)[] rules)
        {
            return ShouldRunVanillaRouteWithArgumentValidator(input, commandName, subcommands, validator, rules);
        }

        string[] relicRoutes = ["help", "list", "listowned", "add", "remove", "equiped", "equip", "unequip"];
        string[] questItemRoutes = ["help", "list", "listowned", "add", "remove"];
        string[] collectibleRoutes = ["help", "list", "listowned", "add", "remove"];
        string[] beadRoutes = ["help", "list", "listowned", "setslots", "add", "remove", "equiped", "equip", "unequip"];
        string[] prayerRoutes = ["help", "list", "listowned", "add", "remove", "equiped", "equip", "unequip", "decipher"];
        string[] swordRoutes = ["help", "list", "listowned", "add", "remove", "equiped", "equip", "unequip"];

        Assert.True(ShouldRun("relic add RE01", "relic", relicRoutes, ("add", 1, 1)));
        Assert.True(ShouldRun("relic remove RE401", "relic", relicRoutes, ("remove", 1, 1)));
        Assert.True(ShouldRun("relic equip RE401 0", "relic", relicRoutes, ("equip", 2, 2)));
        Assert.True(ShouldRun("questitem add QI01", "questitem", questItemRoutes, ("add", 1, 1)));
        Assert.True(ShouldRun("questitem remove QI01", "questitem", questItemRoutes, ("remove", 1, 1)));
        Assert.True(ShouldRun("collectible add CO01", "collectible", collectibleRoutes, ("add", 1, 1)));
        Assert.True(ShouldRun("collectible remove CO01", "collectible", collectibleRoutes, ("remove", 1, 1)));
        Assert.True(ShouldRun("bead add RB01", "bead", beadRoutes, ("add", 1, 1)));
        Assert.True(ShouldRun("bead remove RB01", "bead", beadRoutes, ("remove", 1, 1)));
        Assert.True(ShouldRun("bead equip RB01 6", "bead", beadRoutes, ("equip", 2, 2)));
        Assert.True(ShouldRun("sword add SW01", "sword", swordRoutes, ("add", 1, 1)));
        Assert.True(ShouldRun("sword remove SW01", "sword", swordRoutes, ("remove", 1, 1)));
        Assert.True(ShouldRun("sword equip SW01 0", "sword", swordRoutes, ("equip", 2, 2)));
        Assert.True(ShouldRun("prayer add PR01", "prayer", prayerRoutes, ("add", 1, 1)));
        Assert.True(ShouldRun("prayer remove PR01", "prayer", prayerRoutes, ("remove", 1, 1)));
        Assert.True(ShouldRun("prayer equip PR01 0", "prayer", prayerRoutes, ("equip", 2, 2)));
        Assert.True(ShouldRun("prayer decipher PR01 20", "prayer", prayerRoutes, ("decipher", 2, 2)));

        Assert.True(ShouldRun("relic add ALL", "relic", relicRoutes, ("add", 1, 1)));
        Assert.True(ShouldRun("questitem remove all", "questitem", questItemRoutes, ("remove", 1, 1)));
        Assert.True(ShouldRun("collectible add all", "collectible", collectibleRoutes, ("add", 1, 1)));
        Assert.True(ShouldRun("bead remove all", "bead", beadRoutes, ("remove", 1, 1)));
        Assert.True(ShouldRun("prayer add all", "prayer", prayerRoutes, ("add", 1, 1)));
        Assert.True(ShouldRun("sword remove all", "sword", swordRoutes, ("remove", 1, 1)));

        Assert.False(ShouldRun("relic add MISSING", "relic", relicRoutes, ("add", 1, 1)));
        Assert.False(ShouldRun("relic add RB01", "relic", relicRoutes, ("add", 1, 1)));
        Assert.False(ShouldRun("questitem remove RE401", "questitem", questItemRoutes, ("remove", 1, 1)));

        Assert.Equal(
            "MissingRelic is not a valid relic ID, please use list subcommand",
            StrictCommandRuntime.GetInvalidInventoryItemMessage(StrictCommandResolver.Tokenize("relic add MissingRelic"), inventoryItemExists));
        Assert.Equal(
            "MISSING is not a valid quest item ID, please use list subcommand",
            StrictCommandRuntime.GetInvalidInventoryItemMessage(StrictCommandResolver.Tokenize("questitem remove MISSING"), inventoryItemExists));
        Assert.Equal(
            "MISSING is not a valid collectible item ID, please use list subcommand",
            StrictCommandRuntime.GetInvalidInventoryItemMessage(StrictCommandResolver.Tokenize("collectible remove MISSING"), inventoryItemExists));
        Assert.Equal(
            "MISSING is not a valid rosary bead ID, please use list subcommand",
            StrictCommandRuntime.GetInvalidInventoryItemMessage(StrictCommandResolver.Tokenize("bead equip MISSING 0"), inventoryItemExists));
        Assert.Equal(
            "MISSING is not a valid prayer ID, please use list subcommand",
            StrictCommandRuntime.GetInvalidInventoryItemMessage(StrictCommandResolver.Tokenize("prayer decipher MISSING 1"), inventoryItemExists));
        Assert.Equal(
            "MISSING is not a valid sword ID, please use list subcommand",
            StrictCommandRuntime.GetInvalidInventoryItemMessage(StrictCommandResolver.Tokenize("sword equip MISSING 0"), inventoryItemExists));

        bool allLookupInvoked = false;
        bool missingLookup(string category, string id)
        {
            allLookupInvoked = true;
            return false;
        }
        Assert.Null(StrictCommandRuntime.GetInvalidInventoryItemMessage(
            StrictCommandResolver.Tokenize("relic add ALL"), missingLookup));
        Assert.False(allLookupInvoked);
        Assert.Null(StrictCommandRuntime.GetInvalidInventoryItemMessage(
            StrictCommandResolver.Tokenize("inventory add MISSING"), inventoryItemExists));
        Assert.Null(StrictCommandRuntime.GetInvalidInventoryItemMessage(
            StrictCommandResolver.Tokenize("relic add MISSING extra"), inventoryItemExists));

        List<CommandDefinition> dispatchCatalog =
        [
            new CommandDefinition("relic", ["add"], new object(), Counts(("add", 1, 1)), false, validator)
        ];
        int originalHandlerCalls = 0;
        void Dispatch(string input)
        {
            if (StrictCommandResolver.ShouldRunOriginal(input, dispatchCatalog))
                originalHandlerCalls++;
        }

        Dispatch("relic add MISSING");
        Assert.Equal(0, originalHandlerCalls);
        Dispatch("relic add RB01");
        Assert.Equal(0, originalHandlerCalls);
        Dispatch("relic add RE401");
        Assert.Equal(1, originalHandlerCalls);
    }

    [Fact]
    public void InvalidArgumentValuesDoNotReachTheOriginalHandlerAndValidValuesReachItOnce()
    {
        Func<string[], bool> validator = CreateTestVanillaArgumentValidator();
        List<CommandDefinition> commands =
        [
            new CommandDefinition("timescale", null, new object(), Counts(("", 1, 1)), false, validator),
            new CommandDefinition("achievement", ["addprogress"], new object(), Counts(("addprogress", 2, 2)), false, validator),
            new CommandDefinition("load", null, new object(), Counts(("", 1, 1)), false, validator),
            new CommandDefinition("loadnoui", null, new object(), Counts(("", 1, 1)), false, validator)
        ];

        int handlerCalls;
        void Dispatch(string input)
        {
            if (StrictCommandResolver.ShouldRunOriginal(input, commands))
                handlerCalls++;
        }

        foreach (string input in new[]
        {
            "timescale NaN",
            "timescale Infinity",
            "achievement addprogress arbitrary-id 100,1",
            "load ",
            "loadnoui "
        })
        {
            handlerCalls = 0;
            Dispatch(input);
            Assert.Equal(0, handlerCalls);
        }

        foreach (string input in new[]
        {
            "timescale 2",
            "achievement addprogress arbitrary-id 50",
            "load scene_alpha",
            "loadnoui scene_alpha"
        })
        {
            handlerCalls = 0;
            Dispatch(input);
            Assert.Equal(1, handlerCalls);
        }
    }

    [Fact]
    public void ValidatesDynamicAchievementLevelAndMapIdentifiers()
    {
        Func<string[], bool> validator = StrictCommandRuntime.CreateVanillaArgumentValidator(
            () => 7,
            id => string.Equals(id, "AC01", StringComparison.OrdinalIgnoreCase),
            level => string.Equals(level, "SCENE_ALPHA", StringComparison.OrdinalIgnoreCase),
            (district, zone) => string.Equals(district, "DISTRICT_A", StringComparison.Ordinal)
                && (zone == null || string.Equals(zone, "ZONE_A", StringComparison.Ordinal)),
            (_, _) => true);

        string[] achievementRoutes = ["check", "grant", "clear", "clearsteam", "addprogress", "checkprogress"];
        var achievementCounts = new[]
        {
            ("check", 1, 1), ("grant", 1, 1), ("clear", 1, 1), ("clearsteam", 1, 1),
            ("addprogress", 2, 2), ("checkprogress", 1, 1)
        };
        foreach (string route in achievementRoutes)
        {
            string arguments = route == "addprogress" ? " AC01 50" : " AC01";
            string invalidArguments = route == "addprogress" ? " UNKNOWN 50" : " UNKNOWN";
            string[] routes = ["help", "check", "grant", "clear", "clearsteam", "addprogress", "checkprogress"];
            Assert.True(ShouldRunVanillaRouteWithArgumentValidator(
                "achievement " + route + arguments, "achievement", routes, validator, achievementCounts));
            Assert.False(ShouldRunVanillaRouteWithArgumentValidator(
                "achievement " + route + invalidArguments, "achievement", routes, validator, achievementCounts));
        }

        Assert.True(ShouldRunVanillaRouteWithArgumentValidator(
            "load scene_alpha", "load", null, validator, ("", 1, 1)));
        Assert.True(ShouldRunVanillaRouteWithArgumentValidator(
            "loadnoui SCENE_ALPHA", "loadnoui", null, validator, ("", 1, 1)));
        Assert.False(ShouldRunVanillaRouteWithArgumentValidator(
            "load missing_scene", "load", null, validator, ("", 1, 1)));
        Assert.False(ShouldRunVanillaRouteWithArgumentValidator(
            "loadnoui missing_scene", "loadnoui", null, validator, ("", 1, 1)));

        string[] mapRoutes = ["help", "reveal"];
        (string Subcommand, int Minimum, int Maximum)[] mapCounts = [("help", 0, 0), ("reveal", 1, 2)];
        Assert.True(ShouldRunVanillaRouteWithArgumentValidator("map reveal all", "map", mapRoutes, validator, mapCounts));
        Assert.True(ShouldRunVanillaRouteWithArgumentValidator("map reveal ng", "map", mapRoutes, validator, mapCounts));
        Assert.True(ShouldRunVanillaRouteWithArgumentValidator("map reveal DISTRICT_A", "map", mapRoutes, validator, mapCounts));
        Assert.True(ShouldRunVanillaRouteWithArgumentValidator("map reveal DISTRICT_A ZONE_A", "map", mapRoutes, validator, mapCounts));
        Assert.False(ShouldRunVanillaRouteWithArgumentValidator("map reveal UNKNOWN", "map", mapRoutes, validator, mapCounts));
        Assert.False(ShouldRunVanillaRouteWithArgumentValidator("map reveal DISTRICT_A UNKNOWN", "map", mapRoutes, validator, mapCounts));
        Assert.False(ShouldRunVanillaRouteWithArgumentValidator("map reveal district_a", "map", mapRoutes, validator, mapCounts));
    }

    [Fact]
    public void FindsOnlyCommandsWithSafeHelpRoutes()
    {
        List<CommandDefinition> commands =
        [
            new CommandDefinition("health", ["help", "current", "set"], new object(), Counts(("help", 0, 0), ("current", 0, 0), ("set", 1, 1))),
            new CommandDefinition("completion", ["help", "base"], new object(), Counts(("help", 0, 0), ("base", 0, 0))),
            new CommandDefinition("invincible", null, new object(), Counts(("", 0, 0))),
            new CommandDefinition("graybox", ["show", "hide"], new object(), Counts(("help", 0, 0), ("show", 0, 0), ("hide", 0, 0))),
            new CommandDefinition("help", null, new object(), Counts(("", 0, 1)))
        ];

        Assert.Equal("health", StrictCommandResolver.FindSafeHelpCommand("HEALTH", commands).Name);
        Assert.Equal("completion", StrictCommandResolver.FindSafeHelpCommand("COMPLETION", commands).Name);
        Assert.Null(StrictCommandResolver.FindSafeHelpCommand("invincible", commands));
        Assert.Null(StrictCommandResolver.FindSafeHelpCommand("graybox", commands));
        Assert.Null(StrictCommandResolver.FindSafeHelpCommand("help", commands));
    }

    [Fact]
    public void DispatchesOnlySafeCommandHelpAndWritesFallbackForUnsafeTarget()
    {
        List<CommandDefinition> commands =
        [
            new CommandDefinition("health", ["help"], new object(), Counts(("help", 0, 0))),
            new CommandDefinition("invincible", null, new object(), Counts(("", 0, 0)))
        ];
        int healthHelpCalls = 0;
        int invincibleHandlerCalls = 0;
        List<string> output = [];
        Dictionary<string, Action> handlers = new(StringComparer.OrdinalIgnoreCase)
        {
            ["health"] = () =>
            {
                healthHelpCalls++;
                output.Add("Health command help");
            },
            ["invincible"] = () => invincibleHandlerCalls++
        };

        bool TryShowSafeHelp(string targetName)
        {
            CommandDefinition command = StrictCommandResolver.FindSafeHelpCommand(targetName, commands);
            if (command == null)
                return false;

            handlers[command.Name]();
            return true;
        }

        Assert.True(Help_RenderCommandCatalog_Patch.DispatchCommandHelp(
            ["health"], TryShowSafeHelp, output.Add));
        Assert.Equal(1, healthHelpCalls);
        Assert.Equal("Health command help", output[0]);

        Assert.False(Help_RenderCommandCatalog_Patch.DispatchCommandHelp(
            ["invincible"], TryShowSafeHelp, output.Add));
        Assert.Equal(0, invincibleHandlerCalls);
        Assert.Equal(
            "Command-specific help is unavailable. Use Help for the full command list.",
            output[1]);
    }

    [Fact]
    public void IdentifiesInternalCommandsBeforeVanillaHandling()
    {
        List<CommandDefinition> boundaryCatalog = CreateBoundaryCatalog();

        foreach (string name in new[] { "clear", "cls", "mirrorlog" })
        {
            string[] tokens = StrictCommandResolver.Tokenize(name);
            Assert.True(StrictCommandResolver.ShouldProcessInternalCommand(name, tokens, boundaryCatalog));
            Assert.False(StrictCommandResolver.ShouldRunOriginal(tokens, boundaryCatalog));
        }

        Assert.False(StrictCommandResolver.ShouldProcessInternalCommand("clear ", StrictCommandResolver.Tokenize("clear "), boundaryCatalog));
        Assert.False(StrictCommandResolver.ShouldProcessInternalCommand(" clear", StrictCommandResolver.Tokenize(" clear"), boundaryCatalog));

        List<CommandDefinition> collidingCatalog =
        [
            new CommandDefinition("clear", null, new object()),
            new CommandDefinition("clear")
        ];
        Assert.True(StrictCommandResolver.ShouldProcessInternalCommand("clear", StrictCommandResolver.Tokenize("clear"), collidingCatalog));
        Assert.False(StrictCommandResolver.ShouldRunOriginal(StrictCommandResolver.Tokenize("clear"), collidingCatalog));

        foreach (string invalidInput in new[] { "clear ", " clear", "clear now" })
        {
            string[] tokens = StrictCommandResolver.Tokenize(invalidInput);
            Assert.False(StrictCommandResolver.ShouldProcessInternalCommand(invalidInput, tokens, collidingCatalog));
            Assert.False(StrictCommandResolver.ShouldRunOriginal(tokens, collidingCatalog));
        }

    }

    [Fact]
    public void RequiresExactSharedCommandMatches()
    {
        List<CommandDefinition> boundaryCatalog = CreateBoundaryCatalog();
        Assert.False(StrictCommandResolver.ShouldRunOriginal(StrictCommandResolver.Tokenize("shared"), boundaryCatalog));
        Assert.True(StrictCommandResolver.ShouldRunOriginal(StrictCommandResolver.Tokenize("shared_sequence"), boundaryCatalog));
        Assert.True(StrictCommandResolver.ShouldRunOriginal(StrictCommandResolver.Tokenize("grant invalid-int"), boundaryCatalog));
    }

    [Fact]
    public void PreservesOriginalSharedCommandArgumentBehavior()
    {
        Func<string[], bool> validator = CreateTestVanillaArgumentValidator();
        CommandDefinition command = new(
            "command",
            ["help", "list", "refresh", "shared_sequence"],
            new object(),
            Counts(("help", 0, 0), ("list", 0, 0), ("refresh", 0, 0)),
            true,
            validator);
        CommandDefinition shared = new("shared_sequence", null, new object());
        List<CommandDefinition> catalog = [command, shared];

        Assert.True(StrictCommandResolver.ShouldRunOriginal("command shared_sequence", catalog));
        Assert.False(StrictCommandResolver.ShouldRunOriginal("command shared_sequence ignored", catalog));
        Assert.True(StrictCommandResolver.ShouldRunOriginal("shared_sequence", catalog));
        Assert.True(StrictCommandResolver.ShouldRunOriginal("shared_sequence ignored", catalog));
    }

    private static Dictionary<string, ArgumentCountRange> Counts(params (string Subcommand, int Minimum, int Maximum)[] rules)
    {
        Dictionary<string, ArgumentCountRange> counts = new(StringComparer.OrdinalIgnoreCase);
        foreach ((string subcommand, int minimum, int maximum) in rules)
        {
            counts.Add(subcommand, new ArgumentCountRange(minimum, maximum));
        }

        return counts;
    }

    private static bool ShouldRunVanillaRoute(
        string input,
        string commandName,
        string[] subcommands,
        params (string Subcommand, int Minimum, int Maximum)[] rules)
    {
        return ShouldRunVanillaRouteWithArgumentValidator(
            input,
            commandName,
            subcommands,
            CreateTestVanillaArgumentValidator(),
            rules);
    }

    private static bool ShouldRunVanillaRouteWithArgumentValidator(
        string input,
        string commandName,
        string[] subcommands,
        Func<string[], bool> validator,
        params (string Subcommand, int Minimum, int Maximum)[] rules)
    {
        CommandDefinition command = new(
            commandName,
            subcommands,
            new object(),
            Counts(rules),
            false,
            validator);
        return StrictCommandResolver.ShouldRunOriginal(input, [command]);
    }

    private static Func<string[], bool> CreateTestVanillaArgumentValidator(
        Func<string, string, bool> inventoryItemExists = null)
    {
        return StrictCommandRuntime.CreateVanillaArgumentValidator(
            () => 7,
            _ => true,
            _ => true,
            (_, _) => true,
            inventoryItemExists ?? ((_, _) => true));
    }

    private static List<CommandDefinition> CreateSuggestionCatalog()
    {
        return
        [
            new CommandDefinition("exit"),
            new CommandDefinition("invincible"),
            new CommandDefinition("audio", ["help", "master", "music"]),
            new CommandDefinition("cat"),
            new CommandDefinition("cut"),
            new CommandDefinition("ab"),
            new CommandDefinition("audit", ["list"])
        ];
    }

    private static List<CommandDefinition> CreateBoundaryCatalog()
    {
        return
        [
            new CommandDefinition("audio", ["help", "master"], new object()),
            new CommandDefinition("grant", null, new object()),
            new CommandDefinition("shared_sequence", null, new object()),
            new CommandDefinition("clear"),
            new CommandDefinition("cls"),
            new CommandDefinition("mirrorlog")
        ];
    }

}
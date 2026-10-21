using System.Collections.Generic;
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

        List<CommandDefinition> sharedCommands = new List<CommandDefinition> { new CommandDefinition("grant_relic") };
        CommandDefinition commandWithSharedIds = new CommandDefinition("command", new[] { "help", "list", "refresh", "grant_relic" });
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
    public void TokenizesInputAndRejectsPrefixMatches()
    {
        Assert.Equal(new[] { "AUDIO", "master" }, StrictCommandResolver.Tokenize("  AUDIO\t master  "));
        Assert.Equal(new[] { "clear" }, StrictCommandResolver.Tokenize("clear "));

        List<CommandDefinition> sourceCatalog = new List<CommandDefinition>
        {
            new CommandDefinition("exit"),
            new CommandDefinition("load"),
            new CommandDefinition("loadmenu"),
            new CommandDefinition("health"),
            new CommandDefinition("invincible"),
            new CommandDefinition("invtest"),
            new CommandDefinition("audio", new[] { "help", "master", "music" }),
            new CommandDefinition("demo_extension", new[] { "help", "ping" }),
            new CommandDefinition("shared_sequence"),
            new CommandDefinition("clear"),
            new CommandDefinition("cls"),
            new CommandDefinition("mirrorlog")
        };

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
        MasterConfig constructorDefaults = new MasterConfig();
        MasterConfig defaults = MasterConfig.CreateDefault();

        Assert.Equal(2, constructorDefaults.CommandSuggestionDistance);
        Assert.Equal(2, defaults.CommandSuggestionDistance);
        Assert.Equal(MasterConfig.DefaultConsoleFont, defaults.ConsoleFont);
        Assert.Equal(MasterConfig.DefaultTopLevelCommandColor, defaults.TopLevelCommandColor);
        Assert.Equal(MasterConfig.DefaultSubcommandColor, defaults.SubcommandColor);
        Assert.Equal(MasterConfig.DefaultParameterColor, defaults.ParameterColor);
        Assert.Equal(MasterConfig.DefaultOutputColor, defaults.OutputColor);

        MasterConfig config = new MasterConfig
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

        List<CommandDefinition> collidingCatalog = new List<CommandDefinition>
        {
            new CommandDefinition("clear", null, new object()),
            new CommandDefinition("clear")
        };
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

    private static List<CommandDefinition> CreateSuggestionCatalog()
    {
        return new List<CommandDefinition>
        {
            new CommandDefinition("exit"),
            new CommandDefinition("invincible"),
            new CommandDefinition("audio", new[] { "help", "master", "music" }),
            new CommandDefinition("cat"),
            new CommandDefinition("cut"),
            new CommandDefinition("ab"),
            new CommandDefinition("audit", new[] { "list" })
        };
    }

    private static List<CommandDefinition> CreateBoundaryCatalog()
    {
        return new List<CommandDefinition>
        {
            new CommandDefinition("audio", new[] { "help", "master" }, new object()),
            new CommandDefinition("grant", null, new object()),
            new CommandDefinition("shared_sequence", null, new object()),
            new CommandDefinition("clear"),
            new CommandDefinition("cls"),
            new CommandDefinition("mirrorlog")
        };
    }

}
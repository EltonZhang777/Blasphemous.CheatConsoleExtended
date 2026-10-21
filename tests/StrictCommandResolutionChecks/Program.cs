using System;
using System.Collections.Generic;

namespace Blasphemous.CheatConsoleExtended;

internal static class Program
{
    private static void Main()
    {
        var commands = new List<CommandDefinition>
        {
            new CommandDefinition("exit"),
            new CommandDefinition("invincible"),
            new CommandDefinition("audio", new[] { "help", "master", "music" }),
            new CommandDefinition("cat"),
            new CommandDefinition("cut"),
            new CommandDefinition("ab"),
            new CommandDefinition("audit", new[] { "list" })
        };

        Check(StrictCommandResolver.FindExact("EXIT", commands).Name == "exit");
        Check(StrictCommandResolver.FindExact("in", commands) == null);
        Check(string.Join(",", StrictCommandResolver.GetSuggestions("cot", commands, 1)) == "cat,cut");
        Check(StrictCommandResolver.FormatSuggestionLine(StrictCommandResolver.GetSuggestions("cot", commands, 1)) == "Did you mean: cat, cut?");
        Check(StrictCommandResolver.GetSuggestions("zzzz", commands, 2).Count == 0);
        Check(StrictCommandResolver.FormatSuggestionLine(StrictCommandResolver.GetSuggestions("zzzz", commands, 2)) == null);
        Check(StrictCommandResolver.GetSuggestions("cot", commands, 0).Count == 0);
        Check(string.Join(",", StrictCommandResolver.GetSuggestions("ba", commands, 1)) == "ab");
        Check(StrictCommandResolver.NormalizeDistance(-1) == 0);
        Check(new MasterConfig().CommandSuggestionDistance == 2);
        Check(string.Join("/", StrictCommandResolver.Tokenize("  AUDIO\t master  ")) == "AUDIO/master");
        Check(StrictCommandResolver.Tokenize("clear ").Length == 1);
        Check(StrictCommandResolver.FindExactSubcommand(commands[2], "MASTER") == "master");
        Check(StrictCommandResolver.FindExactSubcommand(commands[2], "mas") == null);
        Check(string.Join(",", StrictCommandResolver.GetSubcommandSuggestions(commands[2], "mastr", 1)) == "audio master");
        Check(string.Join(",", StrictCommandResolver.GetHierarchicalSuggestions("audo", "mastr", commands, 2)) == "audio,audio master");

        var sharedCommands = new List<CommandDefinition> { new CommandDefinition("grant_relic") };
        Check(StrictCommandResolver.FindExact("GRANT_RELIC", sharedCommands).Name == "grant_relic");
        Check(StrictCommandResolver.FindExact("grant", sharedCommands) == null);
        var commandWithSharedIds = new CommandDefinition("command", new[] { "help", "list", "refresh", "grant_relic" });
        Check(StrictCommandResolver.FindExactSubcommand(commandWithSharedIds, "GRANT_RELIC") == "grant_relic");
        Check(StrictCommandResolver.FindExactSubcommand(commandWithSharedIds, "grant") == null);
        sharedCommands[0] = new CommandDefinition("new_shared_id");
        Check(StrictCommandResolver.FindExact("NEW_SHARED_ID", sharedCommands).Name == "new_shared_id");
        Check(StrictCommandResolver.FindExact("grant_relic", sharedCommands) == null);

        var sourceCatalog = new List<CommandDefinition>
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
            Check(StrictCommandResolver.FindExact(sourceCommand.Name.ToUpperInvariant(), sourceCatalog) == sourceCommand);
        Check(StrictCommandResolver.FindExact("e", sourceCatalog) == null);
        Check(StrictCommandResolver.FindExact("in", sourceCatalog) == null);
        Check(StrictCommandResolver.FindExact("a", sourceCatalog) == null);
        Check(StrictCommandResolver.FindExact("invte", sourceCatalog) == null);
        string[] commandWithParameter = StrictCommandResolver.Tokenize("AUDIO MASTER 1");
        CommandDefinition audio = StrictCommandResolver.FindExact(commandWithParameter[0], sourceCatalog);
        Check(audio != null && StrictCommandResolver.FindExactSubcommand(audio, commandWithParameter[1]) == "master");
        var normalizedConfig = new MasterConfig { CommandSuggestionDistance = -5 };
        normalizedConfig.Normalize();
        Check(normalizedConfig.CommandSuggestionDistance == 0);

        var boundaryCatalog = new List<CommandDefinition>
        {
            new CommandDefinition("audio", new[] { "help", "master" }, new object()),
            new CommandDefinition("grant", null, new object()),
            new CommandDefinition("shared_sequence", null, new object()),
            new CommandDefinition("clear"),
            new CommandDefinition("cls"),
            new CommandDefinition("mirrorlog")
        };
        Check(StrictCommandResolver.ShouldRunOriginal(StrictCommandResolver.Tokenize("AUDIO MASTER MiXeD"), boundaryCatalog));
        Check(!StrictCommandResolver.ShouldRunOriginal(StrictCommandResolver.Tokenize("aud"), boundaryCatalog));
        Check(!StrictCommandResolver.ShouldRunOriginal(StrictCommandResolver.Tokenize("AUDIO mas"), boundaryCatalog));
        Check(!StrictCommandResolver.ShouldRunOriginal(StrictCommandResolver.Tokenize("clear"), boundaryCatalog));
        Check(!StrictCommandResolver.ShouldRunOriginal(StrictCommandResolver.Tokenize("clear now"), boundaryCatalog));

        var argumentCountCatalog = new List<CommandDefinition>
        {
            new CommandDefinition("invtest", null, new object(), Counts(("", 0, 0))),
            new CommandDefinition("health", new[] { "help", "current", "set" }, new object(), Counts(("help", 0, 0), ("current", 0, 0), ("set", 1, 1))),
            new CommandDefinition("audio", new[] { "help", "master" }, new object(), Counts(("help", 0, 0), ("master", 1, 1))),
            new CommandDefinition("loadmenu", null, new object(), Counts(("", 0, 0))),
            new CommandDefinition("load", null, new object(), Counts(("", 1, 1))),
            new CommandDefinition("help", null, new object(), Counts(("", 0, 1))),
            new CommandDefinition("invincible", null, new object(), Counts(("", 0, 0))),
            new CommandDefinition("graybox", new[] { "show", "hide" }, new object(), Counts(("help", 0, 0), ("show", 0, 0), ("hide", 0, 0))),
            new CommandDefinition("completion", new[] { "help", "base" }, new object(), Counts(("help", 0, 0), ("base", 0, 0))),
            new CommandDefinition("kill", null, new object(), Counts(("", 0, 2))),
            new CommandDefinition("map", new[] { "help", "reveal" }, new object(), Counts(("help", 0, 0), ("reveal", 1, 2))),
            new CommandDefinition("command", new[] { "help", "shared-entry" }, new object(), Counts(("help", 0, 0)), allowUnlistedArgumentCounts: true)
        };
        Check(StrictCommandResolver.ShouldRunOriginal(StrictCommandResolver.Tokenize("INVTEST"), argumentCountCatalog));
        Check(!StrictCommandResolver.ShouldRunOriginal(StrictCommandResolver.Tokenize("invtest abc def"), argumentCountCatalog));
        Check(StrictCommandResolver.ShouldRunOriginal(StrictCommandResolver.Tokenize("HEALTH SET 10"), argumentCountCatalog));
        Check(!StrictCommandResolver.ShouldRunOriginal(StrictCommandResolver.Tokenize("health set"), argumentCountCatalog));
        Check(!StrictCommandResolver.ShouldRunOriginal(StrictCommandResolver.Tokenize("health set 10 extra"), argumentCountCatalog));
        Check(StrictCommandResolver.ShouldRunOriginal(StrictCommandResolver.Tokenize("AuDiO MaStEr 1"), argumentCountCatalog));
        Check(!StrictCommandResolver.ShouldRunOriginal(StrictCommandResolver.Tokenize("audio master 1 extra"), argumentCountCatalog));
        Check(StrictCommandResolver.ShouldRunOriginal(StrictCommandResolver.Tokenize("loadmenu"), argumentCountCatalog));
        Check(!StrictCommandResolver.ShouldRunOriginal(StrictCommandResolver.Tokenize("loadmenu extra"), argumentCountCatalog));
        Check(StrictCommandResolver.ShouldRunOriginal(StrictCommandResolver.Tokenize("load monastery"), argumentCountCatalog));
        Check(!StrictCommandResolver.ShouldRunOriginal(StrictCommandResolver.Tokenize("load"), argumentCountCatalog));
        Check(StrictCommandResolver.ShouldRunOriginal(StrictCommandResolver.Tokenize("help invincible"), argumentCountCatalog));
        Check(!StrictCommandResolver.ShouldRunOriginal(StrictCommandResolver.Tokenize("help invincible extra"), argumentCountCatalog));
        Check(StrictCommandResolver.ShouldRunOriginal(StrictCommandResolver.Tokenize("kill player prieudieu"), argumentCountCatalog));
        Check(!StrictCommandResolver.ShouldRunOriginal(StrictCommandResolver.Tokenize("kill all extra"), argumentCountCatalog));
        Check(!StrictCommandResolver.ShouldRunOriginal(StrictCommandResolver.Tokenize("kill player other"), argumentCountCatalog));
        Check(StrictCommandResolver.ShouldRunOriginal(StrictCommandResolver.Tokenize("MAP REVEAL secret"), argumentCountCatalog));
        Check(StrictCommandResolver.ShouldRunOriginal(StrictCommandResolver.Tokenize("map reveal secret room"), argumentCountCatalog));
        Check(!StrictCommandResolver.ShouldRunOriginal(StrictCommandResolver.Tokenize("map reveal"), argumentCountCatalog));
        Check(!StrictCommandResolver.ShouldRunOriginal(StrictCommandResolver.Tokenize("map reveal one two three"), argumentCountCatalog));
        Check(StrictCommandResolver.ShouldRunOriginal(StrictCommandResolver.Tokenize("command shared-entry arbitrary arguments"), argumentCountCatalog));
        Check(StrictCommandResolver.FindSafeHelpCommand("HEALTH", argumentCountCatalog).Name == "health");
        Check(StrictCommandResolver.FindSafeHelpCommand("COMPLETION", argumentCountCatalog).Name == "completion");
        Check(StrictCommandResolver.FindSafeHelpCommand("invincible", argumentCountCatalog) == null);
        Check(StrictCommandResolver.FindSafeHelpCommand("graybox", argumentCountCatalog) == null);
        Check(StrictCommandResolver.FindSafeHelpCommand("help", argumentCountCatalog) == null);

        int invalidArgumentOriginalCalls = 0;
        Check(!RouteThroughCommandBoundary("invtest abc def", argumentCountCatalog, _ => { }, _ => invalidArgumentOriginalCalls++));
        Check(invalidArgumentOriginalCalls == 0);
        int validArgumentOriginalCalls = 0;
        Check(RouteThroughCommandBoundary("AuDiO MaStEr 1", argumentCountCatalog, _ => { }, _ => validArgumentOriginalCalls++));
        Check(validArgumentOriginalCalls == 1);
        int internalExecutions = 0;
        int internalOriginalCalls = 0;
        foreach (string name in new[] { "clear", "cls", "mirrorlog" })
        {
            string[] tokens = StrictCommandResolver.Tokenize(name);
            Check(StrictCommandResolver.ShouldProcessInternalCommand(name, tokens, boundaryCatalog));
            Check(!StrictCommandResolver.ShouldRunOriginal(tokens, boundaryCatalog));
            Check(!RouteThroughCommandBoundary(name, boundaryCatalog, _ => internalExecutions++, _ => internalOriginalCalls++));
        }
        Check(internalExecutions == 3 && internalOriginalCalls == 0);
        Check(!StrictCommandResolver.ShouldProcessInternalCommand("clear ", StrictCommandResolver.Tokenize("clear "), boundaryCatalog));
        Check(!StrictCommandResolver.ShouldProcessInternalCommand(" clear", StrictCommandResolver.Tokenize(" clear"), boundaryCatalog));

        var collidingCatalog = new List<CommandDefinition>
        {
            new CommandDefinition("clear", null, new object()),
            new CommandDefinition("clear")
        };
        int collisionInternalCalls = 0;
        int collisionOriginalCalls = 0;
        Check(StrictCommandResolver.ShouldProcessInternalCommand("clear", StrictCommandResolver.Tokenize("clear"), collidingCatalog));
        Check(!RouteThroughCommandBoundary("clear", collidingCatalog, _ => collisionInternalCalls++, _ => collisionOriginalCalls++));
        Check(collisionInternalCalls == 1 && collisionOriginalCalls == 0);
        foreach (string invalidInput in new[] { "clear ", " clear", "clear now" })
            Check(!RouteThroughCommandBoundary(invalidInput, collidingCatalog, _ => collisionInternalCalls++, _ => collisionOriginalCalls++));
        Check(collisionInternalCalls == 1 && collisionOriginalCalls == 0);

        int invalidInternalCalls = 0;
        int invalidOriginalCalls = 0;
        Check(!RouteThroughCommandBoundary("clear ", boundaryCatalog, _ => invalidInternalCalls++, _ => invalidOriginalCalls++));
        Check(!RouteThroughCommandBoundary("clear now", boundaryCatalog, _ => invalidInternalCalls++, _ => invalidOriginalCalls++));
        Check(invalidInternalCalls == 0 && invalidOriginalCalls == 0);

        string rawParameters = " AUDIO  MASTER  MiXeD-Token ";
        int originalCalls = 0;
        string receivedInput = null;
        Check(RouteThroughCommandBoundary(rawParameters, boundaryCatalog, _ => { }, raw =>
        {
            originalCalls++;
            receivedInput = raw;
        }));
        Check(originalCalls == 1 && receivedInput == rawParameters);

        int validationCalls = 0;
        bool validationSucceeded = true;
        Check(RouteThroughCommandBoundary("grant invalid-int", boundaryCatalog, _ => { }, raw =>
        {
            validationCalls++;
            validationSucceeded = int.TryParse(StrictCommandResolver.Tokenize(raw)[1], out _);
        }));
        Check(validationCalls == 1 && !validationSucceeded);

        int rejectedExecutions = 0;
        Check(!RouteThroughCommandBoundary("shared", boundaryCatalog, _ => rejectedExecutions++, _ => rejectedExecutions++));
        Check(rejectedExecutions == 0);

        int sharedExecutions = 0;
        int recursiveExecutions = 0;
        int recursiveOriginalCalls = 0;
        Check(RouteThroughCommandBoundary("shared_sequence", boundaryCatalog, _ => { }, _ =>
        {
            sharedExecutions++;
            Check(!RouteThroughCommandBoundary("clear", boundaryCatalog, __ => recursiveExecutions++, __ => recursiveOriginalCalls++));
        }));
        Check(sharedExecutions == 1 && recursiveExecutions == 1 && recursiveOriginalCalls == 0);
    }

    private static bool RouteThroughCommandBoundary(
        string rawText,
        IList<CommandDefinition> commands,
        Action<string> processInternal,
        Action<string> original)
    {
        string[] tokens = StrictCommandResolver.Tokenize(rawText);
        if (StrictCommandResolver.ShouldProcessInternalCommand(rawText, tokens, commands))
        {
            processInternal(rawText);
            return false;
        }

        if (!StrictCommandResolver.ShouldRunOriginal(tokens, commands))
            return false;

        original(rawText);
        return true;
    }

    private static Dictionary<string, ArgumentCountRange> Counts(params (string Subcommand, int Minimum, int Maximum)[] rules)
    {
        var counts = new Dictionary<string, ArgumentCountRange>(StringComparer.OrdinalIgnoreCase);
        foreach ((string subcommand, int minimum, int maximum) in rules)
            counts.Add(subcommand, new ArgumentCountRange(minimum, maximum));
        return counts;
    }

    private static void Check(bool condition)
    {
        if (!condition)
            throw new InvalidOperationException("strict command resolver check failed");
    }
}

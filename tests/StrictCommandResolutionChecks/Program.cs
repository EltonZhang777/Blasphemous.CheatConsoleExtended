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
        normalizedConfig.CommandSuggestionDistance = StrictCommandResolver.NormalizeDistance(normalizedConfig.CommandSuggestionDistance);
        Check(normalizedConfig.CommandSuggestionDistance == 0);

        var boundaryCatalog = new List<CommandDefinition>
        {
            new CommandDefinition("audio", new[] { "help", "master" }, new object()),
            new CommandDefinition("grant", null, new object()),
            new CommandDefinition("shared_sequence", null, new object()),
            new CommandDefinition("clear")
        };
        Check(StrictCommandResolver.ShouldRunOriginal(StrictCommandResolver.Tokenize("AUDIO MASTER MiXeD"), boundaryCatalog));
        Check(!StrictCommandResolver.ShouldRunOriginal(StrictCommandResolver.Tokenize("aud"), boundaryCatalog));
        Check(!StrictCommandResolver.ShouldRunOriginal(StrictCommandResolver.Tokenize("AUDIO mas"), boundaryCatalog));
        Check(StrictCommandResolver.ShouldRunOriginal(StrictCommandResolver.Tokenize("clear"), boundaryCatalog));
        Check(!StrictCommandResolver.ShouldRunOriginal(StrictCommandResolver.Tokenize("clear now"), boundaryCatalog));
        int internalExecutions = 0;
        Check(RouteThroughOriginalBoundary("clear", boundaryCatalog, _ => internalExecutions++));
        Check(internalExecutions == 1);

        string rawParameters = " AUDIO  MASTER  MiXeD-Token ";
        int originalCalls = 0;
        string receivedInput = null;
        Check(RouteThroughOriginalBoundary(rawParameters, boundaryCatalog, raw =>
        {
            originalCalls++;
            receivedInput = raw;
        }));
        Check(originalCalls == 1 && receivedInput == rawParameters);

        int validationCalls = 0;
        bool validationSucceeded = true;
        Check(RouteThroughOriginalBoundary("grant invalid-int", boundaryCatalog, raw =>
        {
            validationCalls++;
            validationSucceeded = int.TryParse(StrictCommandResolver.Tokenize(raw)[1], out _);
        }));
        Check(validationCalls == 1 && !validationSucceeded);

        int rejectedExecutions = 0;
        Check(!RouteThroughOriginalBoundary("shared", boundaryCatalog, _ => rejectedExecutions++));
        Check(rejectedExecutions == 0);

        int sharedExecutions = 0;
        int recursiveExecutions = 0;
        Check(RouteThroughOriginalBoundary("shared_sequence", boundaryCatalog, _ =>
        {
            sharedExecutions++;
            Check(RouteThroughOriginalBoundary("clear", boundaryCatalog, __ => recursiveExecutions++));
        }));
        Check(sharedExecutions == 1 && recursiveExecutions == 1);
    }

    private static bool RouteThroughOriginalBoundary(
        string rawText,
        IList<CommandDefinition> commands,
        Action<string> original)
    {
        if (!StrictCommandResolver.ShouldRunOriginal(StrictCommandResolver.Tokenize(rawText), commands))
            return false;

        original(rawText);
        return true;
    }

    private static void Check(bool condition)
    {
        if (!condition)
            throw new InvalidOperationException("strict command resolver check failed");
    }
}

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
    }

    private static void Check(bool condition)
    {
        if (!condition)
            throw new InvalidOperationException("strict command resolver check failed");
    }
}

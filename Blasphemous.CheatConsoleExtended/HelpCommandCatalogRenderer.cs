using System;
using System.Collections.Generic;
using System.Linq;

namespace Blasphemous.CheatConsoleExtended;

internal sealed class CommandCatalogEntry
{
    internal CommandCatalogEntry(params string[] names)
    {
        Names = names == null ? new List<string>() : new List<string>(names);
    }

    internal List<string> Names { get; }
}

internal static class HelpCommandCatalogRenderer
{
    private const string VanillaHeading = "All vanilla commands (command aliases separated by | sign):";
    private const string SharedHeading = "All shared commands (script IDs):";
    private const string ModHeading = "All mod commands (command aliases separated by | sign):";

    internal static List<string> Render(
        IEnumerable<CommandCatalogEntry> vanillaCommands,
        IEnumerable<string> sharedCommandIds,
        IEnumerable<CommandCatalogEntry> modCommands)
    {
        List<string> lines = new List<string>();
        AppendSection(lines, VanillaHeading, vanillaCommands, false);
        AppendSharedSection(lines, sharedCommandIds);
        AppendSection(lines, ModHeading, modCommands, true);
        return lines;
    }

    private static void AppendSharedSection(List<string> lines, IEnumerable<string> commandIds)
    {
        lines.Add(SharedHeading);

        List<string> sortedCommandIds = (commandIds ?? Enumerable.Empty<string>())
            .Where(commandId => !string.IsNullOrEmpty(commandId))
            .OrderBy(commandId => commandId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (string commandId in sortedCommandIds)
        {
            lines.Add("\t" + commandId);
        }

        if (sortedCommandIds.Count == 0)
        {
            lines.Add("\tNo shared commands loaded!");
        }
    }

    private static void AppendSection(
        List<string> lines,
        string heading,
        IEnumerable<CommandCatalogEntry> commands,
        bool showEmptyModMessage)
    {
        lines.Add(heading);

        List<CommandCatalogEntry> sortedCommands = (commands ?? Enumerable.Empty<CommandCatalogEntry>())
            .Where(HasName)
            .OrderBy(command => command.Names[0], StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (CommandCatalogEntry command in sortedCommands)
        {
            lines.Add("\t" + Format(command));
        }

        if (showEmptyModMessage && sortedCommands.Count == 0)
        {
            lines.Add("\tNo mod commands registered!");
        }
    }

    private static bool HasName(CommandCatalogEntry command)
    {
        return command != null && command.Names != null && command.Names.Count > 0 && !string.IsNullOrEmpty(command.Names[0]);
    }

    private static string Format(CommandCatalogEntry command)
    {
        List<string> names = new List<string> { command.Names[0] };
        names.AddRange(command.Names.Skip(1).Where(name => !string.IsNullOrEmpty(name)).OrderBy(name => name, StringComparer.OrdinalIgnoreCase));
        return string.Join(" | ", names.ToArray());
    }
}
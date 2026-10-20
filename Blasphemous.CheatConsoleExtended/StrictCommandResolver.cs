using System;
using System.Collections.Generic;

namespace Blasphemous.CheatConsoleExtended;

/// <summary>Describes a command name, its optional executable value, and its supported subcommands.</summary>
internal sealed class CommandDefinition
{
    /// <summary>Creates a command definition with no executable value or subcommands.</summary>
    public CommandDefinition(string name) : this(name, null, null) { }

    /// <summary>Creates a command definition with the specified subcommands and no executable value.</summary>
    public CommandDefinition(string name, IEnumerable<string> subcommands) : this(name, subcommands, null) { }

    /// <summary>Creates a command definition with optional subcommands and an associated executable value.</summary>
    /// <param name="value">The command object used for dispatch; <see langword="null"/> marks an internal console command.</param>
    internal CommandDefinition(string name, IEnumerable<string> subcommands, object value)
    {
        Name = name;
        Value = value;
        Subcommands = [];
        if (subcommands != null)
            Subcommands.AddRange(subcommands);
    }

    public string Name { get; }

    public object Value { get; }

    public List<string> Subcommands { get; }
}

/// <summary>Provides exact command matching, dispatch decisions, and edit-distance-based suggestions.</summary>
internal static class StrictCommandResolver
{
    /// <summary>Splits an input line into whitespace-separated tokens after trimming it and removing carriage returns.</summary>
    public static string[] Tokenize(string rawText)
    {
        string text = (rawText ?? string.Empty).Replace("\r", string.Empty).Trim();
        return text.Length == 0
            ? []
            : text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>Finds the first command whose name exactly matches the token, ignoring case.</summary>
    /// <returns>The matching command, or <see langword="null"/> when no exact match exists.</returns>
    public static CommandDefinition FindExact(string token, IList<CommandDefinition> commands)
    {
        if (string.IsNullOrEmpty(token) || commands == null)
            return null;

        foreach (CommandDefinition command in commands)
        {
            if (command != null && string.Equals(command.Name, token, StringComparison.OrdinalIgnoreCase))
                return command;
        }

        return null;
    }

    /// <summary>Determines whether an exact command and, when required, its first subcommand can run.</summary>
    /// <returns><see langword="true"/> when the vanilla handler should execute the input; otherwise, <see langword="false"/>.</returns>
    public static bool ShouldRunOriginal(string[] tokens, IList<CommandDefinition> commands)
    {
        if (tokens == null || tokens.Length == 0)
            return false;

        if (IsInternalCommand(tokens[0], commands))
            return false;

        CommandDefinition command = FindExact(tokens[0], commands);
        if (command == null || command.Value == null)
            return false;

        return tokens.Length == 1
            || command.Subcommands.Count == 0
            || FindExactSubcommand(command, tokens[1]) != null;
    }

    /// <summary>Determines whether a one-token input exactly names an internal console command.</summary>
    /// <returns><see langword="true"/> only when the raw input is that single command name, ignoring case.</returns>
    public static bool ShouldProcessInternalCommand(string rawText, string[] tokens, IList<CommandDefinition> commands)
    {
        if (tokens == null || tokens.Length != 1 || !string.Equals(rawText, tokens[0], StringComparison.OrdinalIgnoreCase))
            return false;

        return IsInternalCommand(tokens[0], commands);
    }

    /// <summary>Checks whether a token names a command represented without an executable value.</summary>
    private static bool IsInternalCommand(string token, IList<CommandDefinition> commands)
    {
        if (string.IsNullOrEmpty(token) || commands == null)
            return false;

        foreach (CommandDefinition command in commands)
        {
            if (command != null && command.Value == null && string.Equals(command.Name, token, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>Returns the closest matching top-level command names within the allowed edit distance.</summary>
    /// <param name="maxDistance">The maximum Damerau-Levenshtein distance; zero disables suggestions.</param>
    /// <returns>All names tied for the smallest qualifying distance, or an empty list when none qualify.</returns>
    public static List<string> GetSuggestions(string input, IList<CommandDefinition> commands, int maxDistance)
    {
        List<string> suggestions = [];
        foreach (CommandDefinition command in FindNearest(input, commands, maxDistance))
            suggestions.Add(command.Name);
        return suggestions;
    }

    /// <summary>Finds a subcommand whose name exactly matches the token, ignoring case.</summary>
    /// <returns>The matching subcommand name, or <see langword="null"/> when no exact match exists.</returns>
    public static string FindExactSubcommand(CommandDefinition command, string token)
    {
        if (command == null || string.IsNullOrEmpty(token))
            return null;

        foreach (string subcommand in command.Subcommands)
        {
            if (string.Equals(subcommand, token, StringComparison.OrdinalIgnoreCase))
                return subcommand;
        }

        return null;
    }

    /// <summary>Returns the closest matching subcommands qualified by their parent command name.</summary>
    /// <param name="maxDistance">The maximum Damerau-Levenshtein distance; zero disables suggestions.</param>
    /// <returns>Qualified suggestions tied for the smallest qualifying distance.</returns>
    public static List<string> GetSubcommandSuggestions(CommandDefinition command, string input, int maxDistance)
    {
        List<string> suggestions = [];
        if (command == null)
            return suggestions;

        foreach (string subcommand in FindNearestNames(input, command.Subcommands, maxDistance))
            suggestions.Add(command.Name + " " + subcommand);
        return suggestions;
    }

    /// <summary>Suggests close top-level names and close subcommands beneath those top-level candidates.</summary>
    /// <param name="maxDistance">The maximum Damerau-Levenshtein distance used at each command level.</param>
    /// <returns>Top-level names and qualified subcommand suggestions that meet the distance threshold.</returns>
    public static List<string> GetHierarchicalSuggestions(
        string topLevelInput,
        string subcommandInput,
        IList<CommandDefinition> commands,
        int maxDistance)
    {
        List<string> suggestions = [];
        List<CommandDefinition> topLevelCandidates = FindNearest(topLevelInput, commands, maxDistance);
        foreach (CommandDefinition command in topLevelCandidates)
            suggestions.Add(command.Name);

        foreach (CommandDefinition command in topLevelCandidates)
        {
            foreach (string subcommand in FindNearestNames(subcommandInput, command.Subcommands, maxDistance))
                suggestions.Add(command.Name + " " + subcommand);
        }

        return suggestions;
    }

    /// <summary>Formats suggestions as a console prompt, or returns <see langword="null"/> when none are available.</summary>
    public static string FormatSuggestionLine(IList<string> suggestions)
    {
        return suggestions == null || suggestions.Count == 0
            ? null
            : "Did you mean: " + string.Join(", ", ToArray(suggestions)) + "?";
    }

    /// <summary>Clamps a suggestion distance to a nonnegative value.</summary>
    /// <param name="distance">The configured distance, which may be negative.</param>
    /// <returns>The distance, or zero when the supplied value is negative.</returns>
    public static int NormalizeDistance(int distance)
    {
        return Math.Max(0, distance);
    }

    /// <summary>Finds all commands tied for the smallest distance within the supplied threshold.</summary>
    /// <param name="maxDistance">The maximum Damerau-Levenshtein distance; zero disables matching.</param>
    private static List<CommandDefinition> FindNearest(
        string input,
        IList<CommandDefinition> commands,
        int maxDistance)
    {
        List<CommandDefinition> nearest = [];
        if (string.IsNullOrEmpty(input) || commands == null || NormalizeDistance(maxDistance) == 0)
            return nearest;

        int bestDistance = int.MaxValue;
        foreach (CommandDefinition command in commands)
        {
            if (command == null || string.IsNullOrEmpty(command.Name))
                continue;

            int distance = DamerauLevenshteinDistance(input, command.Name);
            if (distance > maxDistance)
                continue;

            if (distance < bestDistance)
            {
                nearest.Clear();
                bestDistance = distance;
            }

            if (distance == bestDistance)
                nearest.Add(command);
        }

        return nearest;
    }

    /// <summary>Finds all names tied for the smallest distance within the supplied threshold.</summary>
    /// <param name="maxDistance">The maximum Damerau-Levenshtein distance; zero disables matching.</param>
    private static List<string> FindNearestNames(string input, IList<string> names, int maxDistance)
    {
        List<string> nearest = [];
        if (string.IsNullOrEmpty(input) || names == null || NormalizeDistance(maxDistance) == 0)
            return nearest;

        int bestDistance = int.MaxValue;
        foreach (string name in names)
        {
            if (string.IsNullOrEmpty(name))
                continue;

            int distance = DamerauLevenshteinDistance(input, name);
            if (distance > maxDistance)
                continue;

            if (distance < bestDistance)
            {
                nearest.Clear();
                bestDistance = distance;
            }

            if (distance == bestDistance)
                nearest.Add(name);
        }

        return nearest;
    }

    /// <summary>Computes the case-insensitive, unrestricted Damerau-Levenshtein distance between two strings.</summary>
    private static int DamerauLevenshteinDistance(string left, string right)
    {
        left = left.ToUpperInvariant();
        right = right.ToUpperInvariant();
        int infinity = left.Length + right.Length;
        int[,] distances = new int[left.Length + 2, right.Length + 2];
        distances[0, 0] = infinity;

        for (int i = 0; i <= left.Length; i++)
        {
            distances[i + 1, 0] = infinity;
            distances[i + 1, 1] = i;
        }
        for (int j = 0; j <= right.Length; j++)
        {
            distances[0, j + 1] = infinity;
            distances[1, j + 1] = j;
        }

        Dictionary<char, int> lastRow = [];

        for (int i = 1; i <= left.Length; i++)
        {
            int lastMatchColumn = 0;
            for (int j = 1; j <= right.Length; j++)
            {
                int lastMatchRow = lastRow.ContainsKey(right[j - 1]) ? lastRow[right[j - 1]] : 0;
                int substitution = left[i - 1] == right[j - 1] ? 0 : 1;
                if (substitution == 0)
                    lastMatchColumn = j;

                distances[i + 1, j + 1] = Math.Min(
                    Math.Min(distances[i, j] + substitution, distances[i + 1, j] + 1),
                    Math.Min(
                        distances[i, j + 1] + 1,
                        distances[lastMatchRow, lastMatchColumn]
                            + (i - lastMatchRow - 1)
                            + 1
                            + (j - lastMatchColumn - 1)));
            }

            lastRow[left[i - 1]] = i;
        }

        return distances[left.Length + 1, right.Length + 1];
    }

    /// <summary>Copies a string list into an array.</summary>
    private static string[] ToArray(IList<string> values)
    {
        string[] result = new string[values.Count];
        for (int i = 0; i < values.Count; i++)
            result[i] = values[i];
        return result;
    }
}
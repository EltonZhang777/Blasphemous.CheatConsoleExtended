using System;
using System.Collections.Generic;

namespace Blasphemous.CheatConsoleExtended;

internal sealed class CommandDefinition
{
    public CommandDefinition(string name) : this(name, null, null) { }

    public CommandDefinition(string name, IEnumerable<string> subcommands) : this(name, subcommands, null) { }

    internal CommandDefinition(string name, IEnumerable<string> subcommands, object value)
    {
        Name = name;
        Value = value;
        Subcommands = new List<string>();
        if (subcommands != null)
            Subcommands.AddRange(subcommands);
    }

    public string Name { get; }

    public object Value { get; }

    public List<string> Subcommands { get; }
}

internal static class StrictCommandResolver
{
    public static string[] Tokenize(string rawText)
    {
        string text = (rawText ?? string.Empty).Replace("\r", string.Empty).Trim();
        return text.Length == 0
            ? new string[0]
            : text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
    }

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

    public static List<string> GetSuggestions(string input, IList<CommandDefinition> commands, int maxDistance)
    {
        List<string> suggestions = new List<string>();
        foreach (CommandDefinition command in FindNearest(input, commands, maxDistance))
            suggestions.Add(command.Name);
        return suggestions;
    }

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

    public static List<string> GetSubcommandSuggestions(CommandDefinition command, string input, int maxDistance)
    {
        List<string> suggestions = new List<string>();
        if (command == null)
            return suggestions;

        foreach (string subcommand in FindNearestNames(input, command.Subcommands, maxDistance))
            suggestions.Add(command.Name + " " + subcommand);
        return suggestions;
    }

    public static List<string> GetHierarchicalSuggestions(
        string topLevelInput,
        string subcommandInput,
        IList<CommandDefinition> commands,
        int maxDistance)
    {
        List<string> suggestions = new List<string>();
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

    public static string FormatSuggestionLine(IList<string> suggestions)
    {
        return suggestions == null || suggestions.Count == 0
            ? null
            : "Did you mean: " + string.Join(", ", ToArray(suggestions)) + "?";
    }

    public static int NormalizeDistance(int distance)
    {
        return Math.Max(0, distance);
    }

    private static List<CommandDefinition> FindNearest(
        string input,
        IList<CommandDefinition> commands,
        int maxDistance)
    {
        List<CommandDefinition> nearest = new List<CommandDefinition>();
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

    private static List<string> FindNearestNames(string input, IList<string> names, int maxDistance)
    {
        List<string> nearest = new List<string>();
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

        Dictionary<char, int> lastRow = new Dictionary<char, int>();

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

    private static string[] ToArray(IList<string> values)
    {
        string[] result = new string[values.Count];
        for (int i = 0; i < values.Count; i++)
            result[i] = values[i];
        return result;
    }
}

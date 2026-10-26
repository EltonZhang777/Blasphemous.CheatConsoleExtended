using Blasphemous.ModdingAPI;
using Gameplay.UI.Console;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Blasphemous.CheatConsoleExtended;

/// <summary>Localizes output emitted by directly registered vanilla console commands.</summary>
internal static class VanillaConsoleOutputLocalizer
{
    internal const int ExpectedTemplateCount = 417;

    private static readonly Regex PlaceholderPattern = new(
        @"\{(?<index>\d+)(?:,[^}:]*)?(?::[^}]*)?\}",
        RegexOptions.CultureInvariant);

    [ThreadStatic]
    private static bool outputEnabled;

    [ThreadStatic]
    private static bool suppressRecursiveOutput;

    private static Catalog catalog = Catalog.Empty;
    private static Func<string, string> localize;

    /// <summary>Captures the prior nested command scope and disables localization during resolution.</summary>
    internal static ProcessCommandScope BeginProcessCommand()
    {
        ProcessCommandScope scope = new(outputEnabled, suppressRecursiveOutput);
        outputEnabled = false;
        return scope;
    }

    /// <summary>Enables output localization for an eligible original command execution.</summary>
    internal static void SelectCommand(Type commandType)
    {
        outputEnabled = ShouldEnableOutput(commandType, suppressRecursiveOutput);
        if (!outputEnabled)
            suppressRecursiveOutput = true;
    }

    /// <summary>Restores the command-localization state that was active before a nested call.</summary>
    internal static void EndProcessCommand(ProcessCommandScope scope)
    {
        outputEnabled = scope.PreviousOutputEnabled;
        suppressRecursiveOutput = scope.PreviousRecursiveSuppression;
    }

    /// <summary>Localizes a console message only while an eligible vanilla command is executing.</summary>
    internal static bool IsOutputEnabled
    {
        get { return outputEnabled; }
    }

    internal static bool IsRecursiveOutputSuppressed
    {
        get { return suppressRecursiveOutput; }
    }
    internal static string PrepareOutputForWrite(string message)
    {
        return PrepareOutputForWrite(catalog, message, outputEnabled, localize);
    }

    /// <summary>Returns whether the resolved command belongs to a direct top-level vanilla invocation.</summary>
    internal static bool ShouldEnableOutput(Type commandType, bool recursiveOutputSuppressed)
    {
        return !recursiveOutputSuppressed
            && commandType != null
            && commandType.Assembly == typeof(ConsoleCommand).Assembly
            && !typeof(SharedCommandsCommand).IsAssignableFrom(commandType);
    }

    /// <summary>Translates a message through a catalog when the caller has established command scope.</summary>
    internal static string PrepareOutputForWrite(
        Catalog sourceCatalog,
        string message,
        bool eligible,
        Func<string, string> localizer)
    {
        return eligible && sourceCatalog != null
            ? sourceCatalog.LocalizeOutput(message, localizer)
            : message;
    }

    /// <summary>Loads the installed English templates and uses ModdingAPI for current-language lookup.</summary>
    internal static void Initialize(CheatConsoleExtended mod)
    {
        Initialize(Catalog.Empty, null);

        try
        {
            string path = Path.Combine(
                Path.Combine(mod.FileHandler.ModdingFolder, "localization"),
                mod.Name + ".txt");
            Catalog loaded = Catalog.Load(File.ReadAllLines(path));
            if (loaded.Count != ExpectedTemplateCount)
                throw new InvalidOperationException(
                    "Expected " + ExpectedTemplateCount + " vanilla output templates, found " + loaded.Count + ".");

            Initialize(loaded, mod.LocalizationHandler.Localize);
        }
        catch (Exception exception)
        {
            ModLog.Warn(
                "Vanilla console output localization is unavailable; original output will be used. " + exception.Message,
                mod);
        }
    }

    /// <summary>Sets the catalog and current-language resolver used at the Write seam.</summary>
    internal static void Initialize(Catalog sourceCatalog, Func<string, string> localizer)
    {
        catalog = sourceCatalog ?? Catalog.Empty;
        localize = localizer;
    }

    /// <summary>Stores the nesting state required to restore localization after a ProcessCommand call.</summary>
    internal readonly struct ProcessCommandScope
    {
        internal ProcessCommandScope(bool previousOutputEnabled, bool previousRecursiveSuppression)
        {
            PreviousOutputEnabled = previousOutputEnabled;
            PreviousRecursiveSuppression = previousRecursiveSuppression;
        }

        internal bool PreviousOutputEnabled { get; }

        internal bool PreviousRecursiveSuppression { get; }
    }

    /// <summary>Maps approved English templates to semantic keys and translates rendered console lines.</summary>
    internal sealed class Catalog
    {
        private readonly Dictionary<string, Template> _templatesByKey;
        private readonly Dictionary<string, Template> _templatesByEnglish;
        private readonly Dictionary<string, Template> _staticTemplates;
        private readonly List<Template> _dynamicTemplates;

        private Catalog()
        {
            _templatesByKey = new Dictionary<string, Template>(StringComparer.Ordinal);
            _templatesByEnglish = new Dictionary<string, Template>(StringComparer.Ordinal);
            _staticTemplates = new Dictionary<string, Template>(StringComparer.Ordinal);
            _dynamicTemplates = [];
        }

        internal static Catalog Empty
        {
            get { return new Catalog(); }
        }

        internal System.Collections.Generic.IEnumerable<string> Keys
        {
            get { return _templatesByKey.Keys; }
        }

        internal string GetRenderedSample(string key)
        {
            return _templatesByKey.TryGetValue(key, out Template template)
                ? template.CreateRenderedSample()
                : null;
        }

        internal string GetEnglish(string key)
        {
            return _templatesByKey.TryGetValue(key, out Template template) ? template.English : null;
        }
        internal int Count
        {
            get { return _templatesByKey.Count; }
        }

        /// <summary>Reads vanilla entries from the English section while preserving source boundary spaces.</summary>
        internal static Catalog Load(IEnumerable<string> lines)
        {
            Catalog catalog = new();
            string language = null;

            if (lines == null)
                return catalog;

            foreach (string line in lines)
            {
                if (line == null)
                    continue;

                int separator = line.IndexOf(':');
                if (separator < 0)
                    continue;

                string key = line.Substring(0, separator).Trim();
                string value = line.Substring(separator + 1);
                if (key == "lang")
                {
                    language = value.Trim();
                    continue;
                }

                if (!string.Equals(language, "en", StringComparison.Ordinal)
                    || !key.StartsWith("vanilla.", StringComparison.Ordinal))
                    continue;

                // Localization resources use one space after the colon as a separator.
                if (value.StartsWith(" ", StringComparison.Ordinal))
                    value = value.Substring(1);
                value = value.Replace("\\n", "\n");

                Template template = new(key, value);
                if (catalog._templatesByKey.ContainsKey(key))
                    throw new InvalidOperationException("Duplicate vanilla output key: " + key);
                if (catalog._templatesByEnglish.ContainsKey(value))
                    throw new InvalidOperationException("Duplicate vanilla output template: " + value);

                catalog._templatesByKey.Add(key, template);
                catalog._templatesByEnglish.Add(value, template);
                if (template.ArgumentIndices.Count == 0)
                    catalog._staticTemplates.Add(value, template);
                else
                    catalog._dynamicTemplates.Add(template);
            }

            catalog._dynamicTemplates.Sort(Template.CompareSpecificity);
            return catalog;
        }

        /// <summary>Localizes an exact or rendered dynamic template without changing captured runtime values.</summary>
        internal string LocalizeOutput(string message, Func<string, string> localizer)
        {
            if (message == null || localizer == null)
                return message;

            if (_staticTemplates.TryGetValue(message, out Template staticTemplate))
                return Translate(staticTemplate, message, null, localizer);

            foreach (Template template in _dynamicTemplates)
            {
                Match greedyMatch = template.GreedyPattern.Match(message);
                if (!greedyMatch.Success)
                    continue;

                Match lazyMatch = template.LazyPattern.Match(message);
                if (!lazyMatch.Success)
                    return message;

                for (int i = 0; i < template.GroupNames.Count; i++)
                {
                    string groupName = template.GroupNames[i];
                    if (!string.Equals(
                        greedyMatch.Groups[groupName].Value,
                        lazyMatch.Groups[groupName].Value,
                        StringComparison.Ordinal))
                        return message;
                }

                Dictionary<int, string> arguments = [];
                for (int i = 0; i < template.ArgumentIndices.Count; i++)
                {
                    arguments[template.ArgumentIndices[i]] = greedyMatch.Groups[template.GroupNames[i]].Value;
                }

                return Translate(template, message, arguments, localizer);
            }

            return message;
        }

        private static string Translate(
            Template template,
            string originalMessage,
            Dictionary<int, string> arguments,
            Func<string, string> localizer)
        {
            string translated;
            try
            {
                translated = localizer(template.Key);
            }
            catch (Exception)
            {
                return originalMessage;
            }

            if (string.IsNullOrEmpty(translated)
                || string.Equals(translated, "LOC_ERROR", StringComparison.Ordinal))
                return originalMessage;

            string translatedCore = translated.Trim();
            if (!TrySubstitute(translatedCore, arguments, out string localizedCore))
                return originalMessage;

            string leading = template.LeadingWhitespace;
            string trailing = template.TrailingWhitespace;
            return leading + localizedCore + trailing;
        }

        private static bool TrySubstitute(
            string template,
            Dictionary<int, string> arguments,
            out string result)
        {
            MatchCollection matches = PlaceholderPattern.Matches(template);
            if (matches.Count == 0)
            {
                result = template;
                return arguments == null || arguments.Count == 0;
            }

            if (arguments == null || arguments.Count == 0)
            {
                result = template;
                return false;
            }

            StringBuilder builder = new(template.Length);
            List<int> usedArguments = [];
            int position = 0;

            foreach (Match match in matches)
            {
                if (!int.TryParse(match.Groups["index"].Value, out int argumentIndex)
                    || !arguments.ContainsKey(argumentIndex))
                {
                    result = template;
                    return false;
                }

                builder.Append(template, position, match.Index - position);
                builder.Append(arguments[argumentIndex]);
                if (!usedArguments.Contains(argumentIndex))
                    usedArguments.Add(argumentIndex);
                position = match.Index + match.Length;
            }

            foreach (int argumentIndex in arguments.Keys)
            {
                if (!usedArguments.Contains(argumentIndex))
                {
                    result = template;
                    return false;
                }
            }

            builder.Append(template, position, template.Length - position);
            result = builder.ToString();
            return true;
        }
    }

    private sealed class Template
    {
        internal readonly string Key;
        internal readonly string English;
        internal readonly Regex GreedyPattern;
        internal readonly Regex LazyPattern;
        internal readonly List<int> ArgumentIndices;
        internal readonly List<string> GroupNames;
        internal readonly string LeadingWhitespace;
        internal readonly string TrailingWhitespace;
        private readonly int _literalLength;

        internal Template(string key, string english)
        {
            Key = key;
            English = english;
            ArgumentIndices = [];
            GroupNames = [];

            int leadingLength = 0;
            while (leadingLength < english.Length && char.IsWhiteSpace(english[leadingLength]))
                leadingLength++;
            LeadingWhitespace = english.Substring(0, leadingLength);

            int trailingStart = english.Length;
            while (trailingStart > leadingLength && char.IsWhiteSpace(english[trailingStart - 1]))
                trailingStart--;
            TrailingWhitespace = english.Substring(trailingStart);

            StringBuilder greedyPattern = new("^");
            StringBuilder lazyPattern = new("^");
            int sourcePosition = 0;
            MatchCollection placeholders = PlaceholderPattern.Matches(english);
            int groupIndex = 0;
            foreach (Match placeholder in placeholders)
            {
                string literal = english.Substring(sourcePosition, placeholder.Index - sourcePosition);
                string escapedLiteral = Regex.Escape(literal);
                greedyPattern.Append(escapedLiteral);
                lazyPattern.Append(escapedLiteral);

                int argumentIndex = int.Parse(placeholder.Groups["index"].Value);
                string groupName = "arg" + groupIndex++;
                // shortcut: differing greedy/lazy captures are ambiguous and fall back to vanilla output.
                greedyPattern.Append("(?<").Append(groupName).Append(">.*)");
                lazyPattern.Append("(?<").Append(groupName).Append(">.*?)");
                ArgumentIndices.Add(argumentIndex);
                GroupNames.Add(groupName);
                sourcePosition = placeholder.Index + placeholder.Length;
            }

            string suffix = english.Substring(sourcePosition);
            string escapedSuffix = Regex.Escape(suffix) + "$";
            greedyPattern.Append(escapedSuffix);
            lazyPattern.Append(escapedSuffix);
            _literalLength = english.Length;
            foreach (Match placeholder in placeholders)
                _literalLength -= placeholder.Length;
            RegexOptions options = RegexOptions.CultureInvariant | RegexOptions.Singleline;
            GreedyPattern = new Regex(greedyPattern.ToString(), options);
            LazyPattern = new Regex(lazyPattern.ToString(), options);
        }

        internal string CreateRenderedSample()
        {
            StringBuilder sample = new(English.Length);
            MatchCollection placeholders = PlaceholderPattern.Matches(English);
            int sourcePosition = 0;

            foreach (Match placeholder in placeholders)
            {
                sample.Append(English, sourcePosition, placeholder.Index - sourcePosition);
                sample.Append("__codex45_runtime_")
                    .Append(placeholder.Groups["index"].Value)
                    .Append("__");
                sourcePosition = placeholder.Index + placeholder.Length;
            }

            sample.Append(English, sourcePosition, English.Length - sourcePosition);
            return sample.ToString();
        }
        internal static int CompareSpecificity(Template left, Template right)
        {
            int result = right._literalLength.CompareTo(left._literalLength);
            if (result != 0)
                return result;

            result = left.ArgumentIndices.Count.CompareTo(right.ArgumentIndices.Count);
            return result != 0 ? result : string.CompareOrdinal(left.Key, right.Key);
        }
    }
}
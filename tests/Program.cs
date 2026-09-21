using System;
using System.Collections.Generic;
using System.Linq;

namespace Blasphemous.CheatConsoleExtended;

internal static class Program
{
    private static int Main()
    {
        AssertCatalogWithCommands();
        AssertSharedRefreshSnapshot();
        AssertEmptyModSection();
        Console.WriteLine("Help command catalog self-check passed.");
        return 0;
    }

    private static void AssertCatalogWithCommands()
    {
        List<string> lines = HelpCommandCatalogRenderer.Render(
            new[]
            {
                new CommandCatalogEntry("zulu", "ZuluAlias", "alphaAlias"),
                new CommandCatalogEntry("Bravo"),
                new CommandCatalogEntry("alpha"),
                new CommandCatalogEntry("command"),
                new CommandCatalogEntry("load"),
                new CommandCatalogEntry("loadmenu"),
                new CommandCatalogEntry("duplicate")
            },
            new[]
            {
                "ZuluShared",
                "alphaShared",
                "shared-after-refresh",
                "duplicate",
                "remove",
                "remove-extra"
            },
            new[]
            {
                new CommandCatalogEntry("ZuluMod"),
                new CommandCatalogEntry("alphaMod"),
                new CommandCatalogEntry("duplicate")
            });

        AssertSequence(
            lines,
            "All vanilla commands (command aliases separated by | sign):",
            "\talpha",
            "\tBravo",
            "\tcommand",
            "\tduplicate",
            "\tload",
            "\tloadmenu",
            "\tzulu | alphaAlias | ZuluAlias",
            "All shared commands (script IDs):",
            "\talphaShared",
            "\tduplicate",
            "\tremove",
            "\tremove-extra",
            "\tshared-after-refresh",
            "\tZuluShared",
            "All mod commands (command aliases separated by | sign):",
            "\talphaMod",
            "\tduplicate",
            "\tZuluMod");
    }

    private static void AssertSharedRefreshSnapshot()
    {
        List<string> sharedCommandIds = new List<string> { "old-shared" };
        List<string> beforeRefresh = HelpCommandCatalogRenderer.Render(
            new CommandCatalogEntry[0],
            sharedCommandIds,
            new CommandCatalogEntry[0]);
        AssertSequence(
            beforeRefresh,
            "All vanilla commands (command aliases separated by | sign):",
            "All shared commands (script IDs):",
            "\told-shared",
            "All mod commands (command aliases separated by | sign):",
            "\tNo mod commands registered!");

        sharedCommandIds.Clear();
        sharedCommandIds.Add("new-shared");
        List<string> afterRefresh = HelpCommandCatalogRenderer.Render(
            new CommandCatalogEntry[0],
            sharedCommandIds,
            new CommandCatalogEntry[0]);
        AssertSequence(
            afterRefresh,
            "All vanilla commands (command aliases separated by | sign):",
            "All shared commands (script IDs):",
            "\tnew-shared",
            "All mod commands (command aliases separated by | sign):",
            "\tNo mod commands registered!");

        if (afterRefresh.Contains("\told-shared"))
        {
            throw new InvalidOperationException("Refreshed catalog retained an old shared command ID.");
        }
    }

    private static void AssertEmptyModSection()
    {
        List<string> lines = HelpCommandCatalogRenderer.Render(
            new[] { new CommandCatalogEntry("command") },
            new string[0],
            new CommandCatalogEntry[0]);

        AssertSequence(
            lines,
            "All vanilla commands (command aliases separated by | sign):",
            "\tcommand",
            "All shared commands (script IDs):",
            "\tNo shared commands loaded!",
            "All mod commands (command aliases separated by | sign):",
            "\tNo mod commands registered!");

        if (lines.Any(string.IsNullOrEmpty))
        {
            throw new InvalidOperationException("Catalog contains an empty line.");
        }
    }

    private static void AssertSequence(IList<string> actual, params string[] expected)
    {
        if (actual.Count != expected.Length)
        {
            throw new InvalidOperationException($"Expected {expected.Length} lines, got {actual.Count}.");
        }

        for (int i = 0; i < expected.Length; i++)
        {
            if (actual[i] != expected[i])
            {
                throw new InvalidOperationException($"Line {i} differs: expected '{expected[i]}', got '{actual[i]}'.");
            }
        }
    }
}

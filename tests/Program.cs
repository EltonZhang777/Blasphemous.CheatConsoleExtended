using System;
using System.Collections.Generic;
using System.Linq;

namespace Blasphemous.CheatConsoleExtended;

internal static class Program
{
    private static int Main()
    {
        AssertCatalogWithCommands();
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
                new CommandCatalogEntry("shared-after-refresh"),
                new CommandCatalogEntry("load"),
                new CommandCatalogEntry("loadmenu"),
                new CommandCatalogEntry("duplicate")
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
            "\tshared-after-refresh",
            "\tzulu | alphaAlias | ZuluAlias",
            "All mod commands (command aliases separated by | sign):",
            "\talphaMod",
            "\tduplicate",
            "\tZuluMod");
    }

    private static void AssertEmptyModSection()
    {
        List<string> lines = HelpCommandCatalogRenderer.Render(
            new[] { new CommandCatalogEntry("command") },
            new CommandCatalogEntry[0]);

        AssertSequence(
            lines,
            "All vanilla commands (command aliases separated by | sign):",
            "\tcommand",
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

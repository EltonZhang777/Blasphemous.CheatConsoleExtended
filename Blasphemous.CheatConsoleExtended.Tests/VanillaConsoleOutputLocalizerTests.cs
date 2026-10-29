using Gameplay.UI.Console;
using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace Blasphemous.CheatConsoleExtended.Tests;

public sealed class VanillaConsoleOutputLocalizerTests
{
    private static VanillaConsoleOutputLocalizer.Catalog LoadCatalog()
    {
        string path = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "resources",
            "localization",
            "Cheat Console Extended.txt");
        return VanillaConsoleOutputLocalizer.Catalog.Load(File.ReadAllLines(path));
    }

    private static VanillaConsoleOutputLocalizer.Catalog LoadCaptionCatalog()
    {
        return VanillaConsoleOutputLocalizer.Catalog.Load(new[]
        {
            "lang: en",
            "vanilla.test.caption: {0} ({1})"
        });
    }

    private static string WriteCommandOutput(string message)
    {
        VanillaConsoleOutputLocalizer.ProcessCommandScope scope =
            VanillaConsoleOutputLocalizer.BeginProcessCommand();
        try
        {
            VanillaConsoleOutputLocalizer.SelectCommand(typeof(Help));
            global::Blasphemous.CheatConsoleExtended.ConsoleWidget_Write_Patch.Prefix(ref message);
            return message;
        }
        finally
        {
            VanillaConsoleOutputLocalizer.EndProcessCommand(scope);
        }
    }

    [Fact]
    public void EnglishCatalogContainsAllApprovedTemplates()
    {
        VanillaConsoleOutputLocalizer.Catalog catalog = LoadCatalog();

        Assert.Equal(VanillaConsoleOutputLocalizer.ExpectedTemplateCount, catalog.Count);
        Assert.Equal(
            "  {0}  -- {1} ({2})",
            catalog.GetEnglish("vanilla.skill.list.entry"));
        Assert.Equal(
            "   Cell: {0} {1}",
            catalog.GetEnglish("vanilla.map.unrevealed.cell"));
        Assert.Equal(
            "   Portal from scene {0}",
            catalog.GetEnglish("vanilla.miriam.status.portal.from_scene"));
    }

    [Fact]
    public void EveryApprovedTemplateResolvesToItsOwnSemanticKey()
    {
        VanillaConsoleOutputLocalizer.Catalog catalog = LoadCatalog();
        int checkedTemplates = 0;

        foreach (string key in catalog.Keys)
        {
            string sample = catalog.GetRenderedSample(key);
            string resolvedKey = null;
            string output = catalog.LocalizeOutput(
                sample,
                candidateKey =>
                {
                    resolvedKey = candidateKey;
                    return catalog.GetEnglish(candidateKey);
                });

            Assert.Equal(key, resolvedKey);
            Assert.Equal(sample, output);
            checkedTemplates++;
        }

        Assert.Equal(VanillaConsoleOutputLocalizer.ExpectedTemplateCount, checkedTemplates);
    }

    [Fact]
    public void DynamicTemplateRetainsRuntimeValues()
    {
        VanillaConsoleOutputLocalizer.Catalog catalog = LoadCatalog();
        const string original = "The command health executed successfully";

        string translated = catalog.LocalizeOutput(
            original,
            key => key == "vanilla.result.success" ? "Finished command: {0}" : "LOC_ERROR");

        Assert.Equal("Finished command: health", translated);
    }

    [Fact]
    public void ParenthesizedDynamicCaptionWithUniqueSeparatorCanReorderFields()
    {
        VanillaConsoleOutputLocalizer.Catalog catalog = LoadCaptionCatalog();
        const string original = "Altar(west) (active)";

        string translated = catalog.LocalizeOutput(
            original,
            key => key == "vanilla.test.caption" ? "{1} — {0}" : "LOC_ERROR");

        Assert.Equal("active — Altar(west)", translated);
    }

    [Fact]
    public void AmbiguousParenthesizedCaptionFallsBackToOriginalWhenTranslationReordersFields()
    {
        VanillaConsoleOutputLocalizer.Catalog catalog = LoadCaptionCatalog();
        const string original = "Skill (advanced) (locked)";

        string translated = catalog.LocalizeOutput(
            original,
            key => key == "vanilla.test.caption" ? "{1} — {0}" : "LOC_ERROR");

        Assert.Equal(original, translated);
    }

    [Fact]
    public void TranslatedTemplateCanReorderPlaceholders()
    {
        VanillaConsoleOutputLocalizer.Catalog catalog = LoadCaptionCatalog();
        const string original = "Altar (active)";

        string translated = catalog.LocalizeOutput(
            original,
            key => key == "vanilla.test.caption" ? "{1} — {0}" : "LOC_ERROR");

        Assert.Equal("active — Altar", translated);
    }

    [Fact]
    public void FormatSpecifiersPreserveTheRenderedRuntimeValue()
    {
        VanillaConsoleOutputLocalizer.Catalog catalog = LoadCatalog();
        const string original = "Current completion: 12.500%";

        string translated = catalog.LocalizeOutput(
            original,
            key => key == "vanilla.completion.get.current_percentage" ? "Completion: {0}%" : "LOC_ERROR");

        Assert.Equal("Completion: 12.500%", translated);
    }

    [Fact]
    public void MissingOrEmptyTranslationFallsBackToTheOriginalEnglishLine()
    {
        VanillaConsoleOutputLocalizer.Catalog catalog = LoadCatalog();
        const string original = "Killing entity: runtime_enemy";

        Assert.Equal(original, catalog.LocalizeOutput(original, key => "LOC_ERROR"));
        Assert.Equal(original, catalog.LocalizeOutput(original, key => string.Empty));
    }

    [Fact]
    public void RawCommandEchoIsNeverRewritten()
    {
        VanillaConsoleOutputLocalizer.Catalog catalog = LoadCatalog();
        const string echo = "> health set 1";

        Assert.Equal(
            echo,
            VanillaConsoleOutputLocalizer.PrepareOutputForWrite(catalog, echo, false, key => "Changed: {0}"));
        Assert.Equal(echo, catalog.LocalizeOutput(echo, key => "Changed: {0}"));
    }

    [Fact]
    public void ExtensionsNestedCommandsAndTheSharedCommandManagerAreExcluded()
    {
        Assert.True(VanillaConsoleOutputLocalizer.ShouldEnableOutput(typeof(Help), false));
        Assert.False(VanillaConsoleOutputLocalizer.ShouldEnableOutput(typeof(ExtensionAdapter), false));
        Assert.False(VanillaConsoleOutputLocalizer.ShouldEnableOutput(typeof(Help), true));

        // Type-based exclusion covers every manager subcommand and its inherited helper output.
        Assert.False(VanillaConsoleOutputLocalizer.ShouldEnableOutput(typeof(SharedCommandsCommand), false));
    }

    [Fact]
    public void SharedManagerSuppressesRecursiveScriptCommandsAndRestoresScope()
    {
        VanillaConsoleOutputLocalizer.ProcessCommandScope managerScope =
            VanillaConsoleOutputLocalizer.BeginProcessCommand();
        try
        {
            VanillaConsoleOutputLocalizer.SelectCommand(typeof(SharedCommandsCommand));
            Assert.False(VanillaConsoleOutputLocalizer.IsOutputEnabled);
            Assert.True(VanillaConsoleOutputLocalizer.IsRecursiveOutputSuppressed);

            VanillaConsoleOutputLocalizer.ProcessCommandScope scriptScope =
                VanillaConsoleOutputLocalizer.BeginProcessCommand();
            try
            {
                VanillaConsoleOutputLocalizer.SelectCommand(typeof(Help));
                Assert.False(VanillaConsoleOutputLocalizer.IsOutputEnabled);
            }
            finally
            {
                VanillaConsoleOutputLocalizer.EndProcessCommand(scriptScope);
            }

            Assert.True(VanillaConsoleOutputLocalizer.IsRecursiveOutputSuppressed);
        }
        finally
        {
            VanillaConsoleOutputLocalizer.EndProcessCommand(managerScope);
        }

        Assert.False(VanillaConsoleOutputLocalizer.IsOutputEnabled);
        Assert.False(VanillaConsoleOutputLocalizer.IsRecursiveOutputSuppressed);
    }

    [Fact]
    public void WriteSeamPreservesExistingHistoryAndUsesCurrentLocaleWithEnglishFallback()
    {
        VanillaConsoleOutputLocalizer.Catalog catalog = LoadCatalog();
        const string readyMessage = "Debug console ready to use. Type Help to display the command list.";
        const string original = "Available commands:";
        const string key = "vanilla.help.available_commands";
        string currentLanguage = "en";
        Func<string, string> localize = candidateKey =>
        {
            if (currentLanguage == "fr")
                return candidateKey == key ? "Commandes disponibles:" : "LOC_ERROR";

            return currentLanguage == "en" ? catalog.GetEnglish(candidateKey) : "LOC_ERROR";
        };
        VanillaConsoleOutputLocalizer.Initialize(catalog, localize);
        try
        {
            var history = new List<string>();
            string readyRow = readyMessage;
            global::Blasphemous.CheatConsoleExtended.ConsoleWidget_Write_Patch.Prefix(ref readyRow);
            history.Add(readyRow);
            history.Add(WriteCommandOutput(original));
            string[] existingRows = history.ToArray();

            currentLanguage = "fr";
            history.Add(WriteCommandOutput(original));
            history.Add(WriteCommandOutput("Reloading current level..."));

            Assert.Equal(readyMessage, history[0]);
            Assert.Equal(original, history[1]);
            Assert.Equal("Commandes disponibles:", history[2]);
            Assert.Equal("Reloading current level...", history[3]);
            Assert.Equal(existingRows, history.GetRange(0, existingRows.Length));
        }
        finally
        {
            VanillaConsoleOutputLocalizer.Initialize(VanillaConsoleOutputLocalizer.Catalog.Empty, null);
        }
    }

    private sealed class ExtensionAdapter
    {
    }
}

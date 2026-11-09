using System.Collections.Generic;
using Xunit;

namespace Blasphemous.CheatConsoleExtended.Tests;

public sealed class HelpCommandCatalogRendererTests
{
    [Fact]
    public void RendersSortedVanillaSharedAndModCommands()
    {
        List<string> lines = HelpCommandCatalogRenderer.Render(
            [
                new CommandCatalogEntry("zulu", "ZuluAlias", "alphaAlias"),
                new CommandCatalogEntry("Bravo"),
                new CommandCatalogEntry("alpha"),
                new CommandCatalogEntry("command"),
                new CommandCatalogEntry("load"),
                new CommandCatalogEntry("loadmenu"),
                new CommandCatalogEntry("duplicate")
            ],
            [
                "ZuluShared",
                "alphaShared",
                "shared-after-refresh",
                "duplicate",
                "remove",
                "remove-extra"
            ],
            [
                new CommandCatalogEntry("ZuluMod"),
                new CommandCatalogEntry("alphaMod"),
                new CommandCatalogEntry("duplicate")
            ]);

        Assert.Equal(new[]
        {
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
            "\tZuluMod"
        }, lines);
    }

    [Fact]
    public void RendersFreshSnapshotOfSharedCommandIds()
    {
        List<string> sharedCommandIds = ["old-shared"];
        List<string> beforeRefresh = HelpCommandCatalogRenderer.Render(
            [], sharedCommandIds, []);

        Assert.Equal(new[]
        {
            "All vanilla commands (command aliases separated by | sign):",
            "All shared commands (script IDs):",
            "\told-shared",
            "All mod commands (command aliases separated by | sign):",
            "\tNo mod commands registered!"
        }, beforeRefresh);

        sharedCommandIds.Clear();
        sharedCommandIds.Add("new-shared");
        List<string> afterRefresh = HelpCommandCatalogRenderer.Render(
            [], sharedCommandIds, []);
        Assert.Equal(new[]
        {
            "All vanilla commands (command aliases separated by | sign):",
            "All shared commands (script IDs):",
            "\tnew-shared",
            "All mod commands (command aliases separated by | sign):",
            "\tNo mod commands registered!"
        }, afterRefresh);
        Assert.DoesNotContain("\told-shared", afterRefresh);
    }

    [Fact]
    public void ShowsEmptySectionMessagesWithoutBlankLines()
    {
        List<string> lines = HelpCommandCatalogRenderer.Render(
            [new CommandCatalogEntry("command")],
            [],
            []);

        Assert.Equal(new[]
        {
            "All vanilla commands (command aliases separated by | sign):",
            "\tcommand",
            "All shared commands (script IDs):",
            "\tNo shared commands loaded!",
            "All mod commands (command aliases separated by | sign):",
            "\tNo mod commands registered!"
        }, lines);
        Assert.DoesNotContain(lines, string.IsNullOrEmpty);
    }
}
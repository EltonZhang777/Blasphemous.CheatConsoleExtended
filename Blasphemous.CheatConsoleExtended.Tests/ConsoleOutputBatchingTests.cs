using Blasphemous.CheatConsoleExtended;
using Gameplay.UI.Console;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using Xunit;

namespace Blasphemous.CheatConsoleExtended.Tests;

[Collection(nameof(ConsoleGlobalStateCollection))]
public sealed class ConsoleOutputBatchingTests
{
    [Fact]
    public void ProcessCommandScopeFlushesJoinedTextThroughConsoleWidgetWrite()
    {
        using ConsoleProcessCommandHarness harness = new();
        Exception commandException = new InvalidOperationException("command failed");
        ConsoleCommand command = harness.RegisterCommand("batch", _ =>
        {
            harness.Write("first");
            harness.Write("second");
            throw commandException;
        });
        object state = harness.StartCommand("batch");

        try
        {
            Exception thrown = Record.Exception(() => harness.ExecuteCommand(command, "batch"));
            Assert.Same(commandException, thrown);
            Exception returnedException = harness.FinishCommand(state, thrown);
            state = null;
            Assert.Same(commandException, returnedException);
        }
        finally
        {
            harness.FinishCommandIfNeeded(ref state);
        }

        Assert.Equal(new[] { "first\nsecond" }, harness.Writes);
    }

    [Fact]
    public void ProcessCommandPrefixBatchesResolvedCommandOutput()
    {
        using ConsoleProcessCommandHarness harness = new();
        ConsoleCommand command = harness.RegisterCommand("batch", _ =>
        {
            harness.Write("first");
            harness.Write("second");
        });
        object state = harness.StartCommand("batch");

        try
        {
            harness.ExecuteCommand(command, "batch");
            Assert.Null(harness.FinishCommand(state, null));
            state = null;
        }
        finally
        {
            harness.FinishCommandIfNeeded(ref state);
        }

        Assert.Equal(new[] { "first\nsecond" }, harness.Writes);
    }

    [Fact]
    public void ConsoleFontHelpUsesOneConsoleWidgetWrite()
    {
        using ConsoleProcessCommandHarness harness = new();
        ConsoleFontCommand command = (ConsoleFontCommand)FormatterServices.GetUninitializedObject(typeof(ConsoleFontCommand));
        MethodInfo showHelp = AccessTools.Method(typeof(ConsoleFontCommand), "ShowHelp");
        object state = harness.StartCommand();

        try
        {
            showHelp.Invoke(command, new object[] { Array.Empty<string>() });
            Assert.Null(harness.FinishCommand(state, null));
            state = null;
        }
        finally
        {
            harness.FinishCommandIfNeeded(ref state);
        }

        Assert.Equal(new[]
        {
            "consolefont set <font family name>: select an installed system font.\n"
                + "consolefont reset: restore Consolas, falling back to Arial.\n"
                + "consolefont list: list names returned by Unity's system-font API.\n"
                + "Font family names are not TTF file names."
        }, harness.Writes);
    }

    [Fact]
    public void HelpCatalogLinesUseOneConsoleWidgetWrite()
    {
        using ConsoleProcessCommandHarness harness = new();
        List<string> lines = HelpCommandCatalogRenderer.Render(
            [new CommandCatalogEntry("vanilla")],
            ["shared-script"],
            [new CommandCatalogEntry("consolefont")]);
        object state = harness.StartCommand();

        try
        {
            foreach (string line in lines)
                harness.Write(line);
            Assert.Null(harness.FinishCommand(state, null));
            state = null;
        }
        finally
        {
            harness.FinishCommandIfNeeded(ref state);
        }

        Assert.Equal(new[] { string.Join("\n", lines) }, harness.Writes);
    }

    [Fact]
    public void CommandSpecificHelpOutputUsesOneConsoleWidgetWrite()
    {
        using ConsoleProcessCommandHarness harness = new();
        ConsoleCommand command = harness.RegisterCommand("health", _ =>
        {
            harness.Write("Health current: 10");
            harness.Write("Health maximum: 20");
        });
        object state = harness.StartCommand();

        try
        {
            Assert.True(Help_RenderCommandCatalog_Patch.DispatchCommandHelp(
                ["health"],
                _ =>
                {
                    harness.ExecuteCommand(command, "health", "help");
                    return true;
                },
                harness.Write));
            Assert.Null(harness.FinishCommand(state, null));
            state = null;
        }
        finally
        {
            harness.FinishCommandIfNeeded(ref state);
        }

        Assert.Equal(new[] { "Health current: 10\nHealth maximum: 20" }, harness.Writes);
    }

    [Fact]
    public void ProcessCommandScopeKeepsSingleWriteText()
    {
        using ConsoleProcessCommandHarness harness = new();
        object state = harness.StartCommand();

        try
        {
            harness.Write("single line");
            Assert.Null(harness.FinishCommand(state, null));
            state = null;
        }
        finally
        {
            harness.FinishCommandIfNeeded(ref state);
        }

        Assert.Equal(new[] { "single line" }, harness.Writes);
    }

    [Fact]
    public void FlushFailureDoesNotReplaceCommandException()
    {
        using ConsoleProcessCommandHarness harness = new();
        object state = harness.StartCommand();
        harness.Write("command output");

        Exception commandException = new InvalidOperationException("command failed");
        harness.WriteException = new InvalidOperationException("console write failed");

        Assert.Same(commandException, harness.FinishCommand(state, commandException));
    }

    [Fact]
    public void RecursiveSharedCommandOutputKeepsOrderInOneWrite()
    {
        using ConsoleProcessCommandHarness harness = new();
        SharedCommandsCommand sharedCommand =
            (SharedCommandsCommand)FormatterServices.GetUninitializedObject(typeof(SharedCommandsCommand));
        harness.SetSharedCommandExecution(() =>
        {
            harness.Write("before");
            object nestedState = harness.StartCommand("nested");
            try
            {
                harness.Write("nested");
                Assert.Null(harness.FinishCommand(nestedState, null));
                nestedState = null;
            }
            finally
            {
                harness.FinishCommandIfNeeded(ref nestedState);
            }
            harness.Write("after");
        });
        ConsoleCommand outerCommand = harness.RegisterCommand(
            "outer",
            _ => Assert.True(CheatConsoleController.ExecuteSharedCommandFromParser(sharedCommand, "shared")));
        object outerState = harness.StartCommand("outer");

        try
        {
            harness.ExecuteCommand(outerCommand, "outer");
            Assert.Null(harness.FinishCommand(outerState, null));
            outerState = null;
        }
        finally
        {
            harness.FinishCommandIfNeeded(ref outerState);
        }

        Assert.Equal(new[] { "before\nnested\nafter" }, harness.Writes);
    }
}
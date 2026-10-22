using System;
using Blasphemous.ModdingAPI;
using Gameplay.UI.Widgets;
using Xunit;

namespace Blasphemous.CheatConsoleExtended.Tests;

public sealed class ConsoleControlApiChecks
{
    [Fact]
    public void Empty_submission_does_not_emit_user_access_error()
    {
        bool previousUserPolicy = CheatConsoleController.IsUserEnabled;
        bool previousApiPolicy = CheatConsoleController.IsApiEnabled;
        var console = new ConsoleWidget();
        console.input.text = " ";
        CheatConsoleController.SetUserEnabled(false);
        CheatConsoleController.SetApiAccessEnabled(false);
        ModLog.Errors.Clear();

        try
        {
            Assert.True(CheatConsoleController.BeginPlayerSubmission(console, out int previousSubmissionDepth));
            CheatConsoleController.EndPlayerSubmission(previousSubmissionDepth);
            Assert.Empty(console.WrittenMessages);
            Assert.Empty(ModLog.Errors);
        }
        finally
        {
            CheatConsoleController.SetUserEnabled(previousUserPolicy);
            CheatConsoleController.SetApiAccessEnabled(previousApiPolicy);
            ModLog.Errors.Clear();
        }
    }

    [Fact]
    public void Handles_widget_lifecycle_and_submits_commands_while_closed()
    {
        CheatConsoleController.SetUserEnabled(true);
        CheatConsoleController.SetApiAccessEnabled(true);
        ConsoleWidget.Instance = null;
        try
        {
            Assert.False(CheatConsoleController.IsConsoleWidgetOpen);

            Assert.Throws<InvalidOperationException>(() => CheatConsoleController.SetConsoleOpen(true));
            Assert.False(CheatConsoleController.ExecuteCommand("queued command"));

            var console = new ConsoleWidget();
            ConsoleWidget.Instance = console;
            Assert.False(CheatConsoleController.IsConsoleWidgetOpen);

            CheatConsoleController.SetConsoleOpen(true);
            Assert.True(CheatConsoleController.IsConsoleWidgetOpen);
            CheatConsoleController.SetConsoleOpen(false);
            Assert.False(CheatConsoleController.IsConsoleWidgetOpen);

            const string command = "  AUDIO  MASTER MiXeD-Token  ";
            Assert.True(CheatConsoleController.ExecuteCommand(command));
            Assert.False(console.IsEnabled());
            Assert.Equal(new[] { command }, console.ProcessedCommands);
        }
        finally
        {
            CheatConsoleController.SetUserEnabled(true);
            CheatConsoleController.SetApiAccessEnabled(true);
            ConsoleWidget.Instance = null;
        }
    }

    [Fact]
    public void Keeps_user_and_api_access_policies_independent()
    {
        CheatConsoleController.SetUserEnabled(true);
        CheatConsoleController.SetApiAccessEnabled(true);
        ConsoleWidget.Instance = null;

        try
        {
            Assert.True(CheatConsoleController.IsUserEnabled);
            Assert.True(CheatConsoleController.IsApiEnabled);

            CheatConsoleController.SetUserEnabled(false);
            Assert.False(CheatConsoleController.IsUserEnabled);
            Assert.True(CheatConsoleController.IsApiEnabled);
            Assert.False(CheatConsoleController.ExecuteCommand("unavailable before widget"));

            var console = new ConsoleWidget();
            ConsoleWidget.Instance = console;
            Assert.True(CheatConsoleController.ExecuteCommand("api while user disabled"));
            Assert.Equal(new[] { "api while user disabled" }, console.ProcessedCommands);

            CheatConsoleController.SetApiAccessEnabled(false);
            Assert.False(CheatConsoleController.IsApiEnabled);
            Assert.False(CheatConsoleController.ExecuteCommand("blocked by api policy"));
            CheatConsoleController.SetUserEnabled(true);
            Assert.True(CheatConsoleController.IsUserEnabled);
            Assert.False(CheatConsoleController.IsApiEnabled);

            CheatConsoleController.SetApiAccessEnabled(true);
            Assert.True(CheatConsoleController.ExecuteCommand("restored api"));
            Assert.Equal(new[] { "api while user disabled", "restored api" }, console.ProcessedCommands);
        }
        finally
        {
            CheatConsoleController.SetUserEnabled(true);
            CheatConsoleController.SetApiAccessEnabled(true);
            ConsoleWidget.Instance = null;
        }
    }
}

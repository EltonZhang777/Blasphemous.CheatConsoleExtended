using System;
using Blasphemous.ModdingAPI;
using Gameplay.UI.Widgets;
using Xunit;

namespace Blasphemous.CheatConsoleExtended.Tests;

public sealed class ConsoleControlApiChecks
{
    [Fact]
    public void Player_submission_respects_user_policy_while_api_access_is_disabled()
    {
        const string error = "Error: Player command rejected because user access is disabled.";
        const string logError = "Cheat Console: " + error;
        bool previousUserPolicy = CheatConsoleController.IsUserEnabled;
        bool previousApiPolicy = CheatConsoleController.IsApiEnabled;
        ConsoleWidget previousConsole = ConsoleWidget.Instance;
        var console = new ConsoleWidget();
        ConsoleWidget.Instance = console;
        console.input.text = "help";
        console.scrollRect.verticalNormalizedPosition = 1f;
        CheatConsoleController.SetUserEnabled(true);
        CheatConsoleController.SetApiAccessEnabled(true);
        CheatConsoleController.SetConsoleOpen(true);
        CheatConsoleController.SetUserEnabled(false);
        CheatConsoleController.SetApiAccessEnabled(false);
        ModLog.Errors.Clear();

        try
        {
            console.PressEnter();

            Assert.Empty(console.ProcessedCommands);
            Assert.Equal(new[] { error }, console.WrittenMessages);
            Assert.Equal(new object[] { logError }, ModLog.Errors);
            Assert.Equal(0f, console.scrollRect.verticalNormalizedPosition);
            Assert.True(CheatConsoleController.IsConsoleWidgetOpen);

            CheatConsoleController.SetUserEnabled(true);
            console.PressEnter();
            Assert.Equal(new[] { "help" }, console.ProcessedCommands);
            Assert.Equal(new object[] { logError }, ModLog.Errors);
        }
        finally
        {
            CheatConsoleController.SetUserEnabled(previousUserPolicy);
            CheatConsoleController.SetApiAccessEnabled(previousApiPolicy);
            ConsoleWidget.Instance = previousConsole;
            ModLog.Errors.Clear();
        }
    }

    [Fact]
    public void Empty_player_submission_does_not_emit_user_access_error()
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
            console.PressEnter();

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

    [Fact]
    public void Rejects_a_direct_mod_open_when_api_access_is_disabled()
    {
        bool previousApiPolicy = CheatConsoleController.IsApiEnabled;
        var console = new ConsoleWidget();
        CheatConsoleController.SetApiAccessEnabled(false);

        try
        {
            console.SetEnabled(true);

            Assert.False(console.IsEnabled());
        }
        finally
        {
            CheatConsoleController.SetApiAccessEnabled(previousApiPolicy);
        }
    }

    [Fact]
    public void Rejects_an_unknown_api_open_when_api_access_is_disabled()
    {
        bool previousApiPolicy = CheatConsoleController.IsApiEnabled;
        var console = new ConsoleWidget();
        ConsoleWidget.Instance = console;
        CheatConsoleController.SetApiAccessEnabled(false);

        try
        {
            CheatConsoleController.SetConsoleOpen(true);

            Assert.False(console.IsEnabled());
        }
        finally
        {
            CheatConsoleController.SetApiAccessEnabled(previousApiPolicy);
            ConsoleWidget.Instance = null;
        }
    }
}
using System;
using Gameplay.UI.Widgets;
using Xunit;

namespace Blasphemous.CheatConsoleExtended.Tests;

public sealed class ConsoleControlApiChecks
{
    [Fact]
    public void Handles_widget_lifecycle_and_submits_commands_while_closed()
    {
        ConsoleWidget.Instance = null;
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
}

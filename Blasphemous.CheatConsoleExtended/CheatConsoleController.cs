using System;
using Gameplay.UI.Widgets;

namespace Blasphemous.CheatConsoleExtended;

/// <summary>Provides programmatic access to the game's cheat console.</summary>
public static class CheatConsoleController
{
    /// <summary>Gets whether the active console widget is open.</summary>
    public static bool IsConsoleWidgetOpen
    {
        get
        {
            ConsoleWidget console = ConsoleWidget.Instance;
            return console != null && console.IsEnabled();
        }
    }

    /// <summary>Opens or closes the active console widget.</summary>
    /// <exception cref="InvalidOperationException">The console widget has not been initialized.</exception>
    public static void SetConsoleOpen(bool open)
    {
        ConsoleWidget console = ConsoleWidget.Instance;
        if (console == null)
            throw new InvalidOperationException("The console widget is not available yet.");

        console.SetEnabled(open);
    }

    /// <summary>Submits a command through the game's existing parser, whether or not its UI is open.</summary>
    /// <returns>True when a console widget accepted the command; otherwise, false.</returns>
    public static bool ExecuteCommand(string command)
    {
        ConsoleWidget console = ConsoleWidget.Instance;
        if (console == null)
            return false;

        console.ProcessCommand(command);
        return true;
    }
}

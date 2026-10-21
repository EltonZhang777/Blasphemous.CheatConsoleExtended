using System.Collections.Generic;

namespace Gameplay.UI.Widgets;

public sealed class ConsoleWidget
{
    public static ConsoleWidget Instance;
    public readonly List<string> ProcessedCommands = new List<string>();

    private bool isEnabled;

    public bool IsEnabled() => isEnabled;

    public void SetEnabled(bool enabled) => isEnabled = enabled;

    public void ProcessCommand(string command) => ProcessedCommands.Add(command);
}

using System.Collections.Generic;

namespace Gameplay.UI.Widgets
{
    public sealed class ConsoleWidget
    {
        public static ConsoleWidget Instance;
        public readonly List<string> ProcessedCommands = new List<string>();
        public readonly List<string> WrittenMessages = new List<string>();
        public ConsoleInput input = new ConsoleInput();

        private bool isEnabled;

        public bool IsEnabled() => isEnabled;

        public void SetEnabled(bool enabled) => isEnabled = enabled;

        public void Write(string message) => WrittenMessages.Add(message);

        public void Submit() { }

        public void ProcessCommand(string command) => ProcessedCommands.Add(command);
    }

    public sealed class ConsoleInput
    {
        public string text = string.Empty;
    }
}

namespace Gameplay.UI.Console
{
    public class ConsoleCommand
    {
        public virtual void Execute(string command, string[] parameters) { }
    }

    public sealed class SharedCommandsCommand
    {
        public bool ExecuteIfIsCommand(string command) => false;
    }
}

namespace Blasphemous.ModdingAPI
{
    public static class ModLog
    {
        public static readonly List<object> Errors = new List<object>();

        public static void Error(object message) => Errors.Add(message);
    }
}

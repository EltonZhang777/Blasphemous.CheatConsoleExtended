using System.Collections.Generic;
using Blasphemous.CheatConsoleExtended;
using UnityEngine.UI;

namespace Gameplay.UI.Widgets
{
    public sealed class ConsoleWidget
    {
        public static ConsoleWidget Instance;
        public readonly List<string> ProcessedCommands = new List<string>();
        public readonly List<string> WrittenMessages = new List<string>();
        public ConsoleInput input = new ConsoleInput();
        public ScrollRect scrollRect = new ScrollRect();

        private bool isEnabled;

        public bool IsEnabled() => isEnabled;

        public void SetEnabled(bool enabled)
        {
            if (CheatConsoleController.CanSetConsoleOpen(this, enabled))
                isEnabled = enabled;
        }

        public void Write(string message) => WrittenMessages.Add(message);

        public void PressEnter() => CheatConsoleController.SubmitFromPlayerInput(this);

        public void Submit()
        {
            if (!CheatConsoleController.BeginPlayerSubmission(this, out int previousSubmissionDepth))
            {
                CheatConsoleController.EndPlayerSubmission(previousSubmissionDepth);
                return;
            }

            try
            {
                CheatConsoleController.ProcessCommandFromSubmit(this, input.text);
            }
            finally
            {
                CheatConsoleController.EndPlayerSubmission(previousSubmissionDepth);
            }
        }

        public void ProcessCommand(string command) => ProcessedCommands.Add(command);
    }

    public sealed class ConsoleInput
    {
        public string text = string.Empty;
    }
}

namespace UnityEngine.UI
{
    public sealed class ScrollRect
    {
        public float verticalNormalizedPosition = 1f;
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

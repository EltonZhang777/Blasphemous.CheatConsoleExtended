using System;

namespace Blasphemous.CheatConsoleExtended;

internal static class ConsoleUiText
{
    internal const string ReadyMessageKey = "console.ready";
    internal const string ReadyMessageEnglish = "Debug console ready to use. Type Help to display the command list.";

    internal static string GetReadyMessage(Func<string, string> localize)
    {
        if (localize == null)
        {
            return ReadyMessageEnglish;
        }

        string message = localize(ReadyMessageKey);
        return string.IsNullOrEmpty(message) || message == "LOC_ERROR"
            ? ReadyMessageEnglish
            : message;
    }
}

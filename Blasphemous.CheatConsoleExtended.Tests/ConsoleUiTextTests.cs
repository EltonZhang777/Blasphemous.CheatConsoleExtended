using Xunit;

namespace Blasphemous.CheatConsoleExtended.Tests;

public sealed class ConsoleUiTextTests
{
    [Fact]
    public void UsesLocalizedReadyMessageAndFallsBackToEnglishWhenMissing()
    {
        const string localizedMessage = "Console prêt.";

        Assert.Equal(localizedMessage, ConsoleUiText.GetReadyMessage(key =>
        {
            Assert.Equal(ConsoleUiText.ReadyMessageKey, key);
            return localizedMessage;
        }));

        Assert.Equal(ConsoleUiText.ReadyMessageEnglish, ConsoleUiText.GetReadyMessage(_ => "LOC_ERROR"));
        Assert.Equal(ConsoleUiText.ReadyMessageEnglish, ConsoleUiText.GetReadyMessage(_ => string.Empty));
        Assert.Equal(ConsoleUiText.ReadyMessageEnglish, ConsoleUiText.GetReadyMessage(_ => null));
    }
}

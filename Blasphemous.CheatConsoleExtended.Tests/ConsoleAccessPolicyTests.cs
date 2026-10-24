using Blasphemous.CheatConsoleExtended;
using Xunit;

namespace Blasphemous.CheatConsoleExtended.Tests;

public sealed class ConsoleAccessPolicyTests
{
    [Fact]
    public void UserAndApiAccessCanBeChangedIndependentlyThroughThePublicApi()
    {
        bool previousUserPolicy = CheatConsoleController.IsUserEnabled;
        bool previousApiPolicy = CheatConsoleController.IsApiEnabled;

        Assert.True(previousUserPolicy);
        Assert.True(previousApiPolicy);

        try
        {
            CheatConsoleController.SetUserEnabled(false);
            Assert.False(CheatConsoleController.IsUserEnabled);
            Assert.True(CheatConsoleController.IsApiEnabled);

            CheatConsoleController.SetApiAccessEnabled(false);
            Assert.False(CheatConsoleController.IsUserEnabled);
            Assert.False(CheatConsoleController.IsApiEnabled);

            CheatConsoleController.SetUserEnabled(true);
            Assert.True(CheatConsoleController.IsUserEnabled);
            Assert.False(CheatConsoleController.IsApiEnabled);
        }
        finally
        {
            CheatConsoleController.SetUserEnabled(previousUserPolicy);
            CheatConsoleController.SetApiAccessEnabled(previousApiPolicy);
        }
    }
}

using Blasphemous.CheatConsoleExtended.Patches;
using Gameplay.UI.Widgets;
using HarmonyLib;
using System.Reflection;
using System.Reflection.Emit;
using Xunit;

namespace Blasphemous.CheatConsoleExtended.Tests;

public sealed class ConsoleConfiguredHotkeyRoutingTests
{
    [Fact]
    public void ConfiguredHotkeyUsesPlayerToggleMarkerWhenAvailable()
    {
        MethodInfo marker = typeof(ConsoleConfiguredHotkeyRoutingTests).GetMethod(
            nameof(PlayerToggleMarker), BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo target = AccessTools.Method(typeof(ConsoleWidget), "SetEnabled", [typeof(bool)]);
        var instruction = new CodeInstruction(OpCodes.Call, target);

        CodeInstruction routed = Assert.Single(
            CheatConsoleConfiguredHotkey_PlayerAccess_Patch.RouteConfiguredHotkey([instruction], marker));

        Assert.Equal(OpCodes.Call, routed.opcode);
        Assert.Equal(marker, routed.operand);
    }

    [Fact]
    public void ConfiguredHotkeyFallsBackWhenPlayerToggleMarkerIsUnavailable()
    {
        MethodInfo target = AccessTools.Method(typeof(ConsoleWidget), "SetEnabled", [typeof(bool)]);
        MethodInfo fallback = AccessTools.Method(typeof(CheatConsoleController), nameof(CheatConsoleController.SetEnabledFromPlayerInput));
        var instruction = new CodeInstruction(OpCodes.Call, target);

        CodeInstruction routed = Assert.Single(
            CheatConsoleConfiguredHotkey_PlayerAccess_Patch.RouteConfiguredHotkey([instruction], null));

        Assert.Equal(OpCodes.Call, routed.opcode);
        Assert.Equal(fallback, routed.operand);
    }

    private static void PlayerToggleMarker(ConsoleWidget console, bool open)
    {
    }
}

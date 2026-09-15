using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Gameplay.UI.Widgets;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace Blasphemous.CheatConsoleExtended;

internal static class ConsoleColoring
{
    public static void SetOutputText(Text text, string message)
    {
        text.color = GetOutputColor();
        text.supportRichText = false;
        text.text = message ?? string.Empty;
    }

    private static Color GetOutputColor()
    {
        string value = Main.CheatConsoleExtended == null || Main.CheatConsoleExtended.Config == null
            ? MasterConfig.DefaultOutputColor
            : Main.CheatConsoleExtended.Config.OutputColor as string ?? MasterConfig.DefaultOutputColor;

        Color color;
        if (ColorUtility.TryParseHtmlString(value, out color))
            return color;

        ColorUtility.TryParseHtmlString(MasterConfig.DefaultOutputColor, out color);
        return color;
    }
}

[HarmonyPatch(typeof(ConsoleWidget), "Write")]
internal static class ConsoleWidget_Write_Patch
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        MethodInfo setter = AccessTools.PropertySetter(typeof(Text), nameof(Text.text));
        MethodInfo replacement = AccessTools.Method(typeof(ConsoleColoring), nameof(ConsoleColoring.SetOutputText));

        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.operand is MethodInfo method && method == setter)
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = replacement;
            }
            yield return instruction;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Blasphemous.ModdingAPI;
using Gameplay.UI.Console;
using Gameplay.UI.Widgets;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace Blasphemous.CheatConsoleExtended;

internal static class ConsoleColoring
{
    private static readonly Dictionary<Type, bool> subcommandCache = new Dictionary<Type, bool>();
    private static ConsoleWidget pendingEchoConsole;
    private static string pendingEchoText;
    private static bool inputColorUpdateInProgress;
    private static bool inputColorDiagnosticLogged;
    private static bool inputColorReentryDiagnosticLogged;
    private static bool inputColorCompletionDiagnosticLogged;

    public static void SetOutputText(Text text, string message)
    {
        ConsoleTextColorEffect effect = text.GetComponent<ConsoleTextColorEffect>();
        if (effect != null)
            effect.enabled = false;

        ConsoleWidget console;
        string command;
        if (TryConsumeCommandEcho(message, out console, out command))
        {
            SetCommandEchoText(text, console, command);
            return;
        }

        text.color = GetOutputColor();
        text.supportRichText = false;
        text.text = message ?? string.Empty;
    }

    public static void SetInputText(InputField input, ConsoleWidget console)
    {
        if (inputColorUpdateInProgress)
        {
            if (!inputColorReentryDiagnosticLogged)
            {
                inputColorReentryDiagnosticLogged = true;
                ModLog.Debug("[DIAG][InputColor] Suppressed reentrant InputField.UpdateLabel.");
            }

            return;
        }

        inputColorUpdateInProgress = true;

        try
        {
            if (!inputColorDiagnosticLogged)
            {
                inputColorDiagnosticLogged = true;
                ModLog.Debug("[DIAG][InputColor] SetInputText entered for console input.");
            }

            Text text = input.textComponent;
            if (text == null)
                return;

            ConsoleTextColorEffect effect = text.GetComponent<ConsoleTextColorEffect>();
            if (effect == null)
                effect = text.gameObject.AddComponent<ConsoleTextColorEffect>();
            effect.enabled = true;
            effect.Configure(console, input.text, false);

            if (!inputColorCompletionDiagnosticLogged)
            {
                inputColorCompletionDiagnosticLogged = true;
                ModLog.Debug("[DIAG][InputColor] SetInputText completed after Configure.");
            }
        }
        finally
        {
            inputColorUpdateInProgress = false;
        }
    }

    public static void BeginCommandEcho(ConsoleWidget console)
    {
        pendingEchoConsole = null;
        pendingEchoText = null;

        if (ReferenceEquals(console, null) || console.input == null)
            return;

        string input = console.input.text;
        if (!string.IsNullOrEmpty(input) && input.Trim().Length > 0)
        {
            pendingEchoConsole = console;
            pendingEchoText = input;
        }
    }

    public static void EndCommandEcho()
    {
        pendingEchoConsole = null;
        pendingEchoText = null;
    }

    internal static Color GetOutputColor()
    {
        string value = GetConfiguredColor(
            Main.CheatConsoleExtended == null || Main.CheatConsoleExtended.Config == null
                ? null
                : Main.CheatConsoleExtended.Config.OutputColor,
            MasterConfig.DefaultOutputColor);

        Color color;
        if (ColorUtility.TryParseHtmlString(value, out color))
            return color;

        ColorUtility.TryParseHtmlString(MasterConfig.DefaultOutputColor, out color);
        return color;
    }

    private static void SetCommandEchoText(Text text, ConsoleWidget console, string command)
    {
        text.color = Color.white;
        text.supportRichText = false;
        text.text = "> " + (command ?? string.Empty);

        ConsoleTextColorEffect effect = text.GetComponent<ConsoleTextColorEffect>();
        if (effect == null)
            effect = text.gameObject.AddComponent<ConsoleTextColorEffect>();
        effect.enabled = true;
        effect.Configure(console, command, true);
    }

    private static bool TryConsumeCommandEcho(string message, out ConsoleWidget console, out string command)
    {
        console = pendingEchoConsole;
        command = pendingEchoText;

        if (ReferenceEquals(console, null) || command == null || message != "> " + command)
            return false;

        EndCommandEcho();
        return true;
    }

    internal static string GetFirstToken(string command)
    {
        if (string.IsNullOrEmpty(command))
            return string.Empty;

        int start = 0;
        while (start < command.Length && command[start] == ' ')
            start++;

        int end = start;
        while (end < command.Length && command[end] != ' ')
            end++;

        return command.Substring(start, end - start);
    }

    private static string GetTokenColor(int tokenIndex, bool hasSubcommand)
    {
        if (Main.CheatConsoleExtended == null || Main.CheatConsoleExtended.Config == null)
        {
            if (tokenIndex == 0)
                return MasterConfig.DefaultTopLevelCommandColor;
            if (tokenIndex == 1 && hasSubcommand)
                return MasterConfig.DefaultSubcommandColor;
            return MasterConfig.DefaultParameterColor;
        }

        if (tokenIndex == 0)
            return GetConfiguredColor(Main.CheatConsoleExtended.Config.TopLevelCommandColor, MasterConfig.DefaultTopLevelCommandColor);
        if (tokenIndex == 1 && hasSubcommand)
            return GetConfiguredColor(Main.CheatConsoleExtended.Config.SubcommandColor, MasterConfig.DefaultSubcommandColor);
        return GetConfiguredColor(Main.CheatConsoleExtended.Config.ParameterColor, MasterConfig.DefaultParameterColor);
    }

    internal static bool HasCommandSubcommand(ConsoleWidget console, string token)
    {
        return HasSubcommand(console, token);
    }

    internal static Color GetCommandCharacterColor(string command, int characterIndex, bool hasSubcommand)
    {
        int tokenIndex = GetTokenIndex(command, characterIndex);
        if (tokenIndex < 0)
            return GetOutputColor();

        Color color;
        if (ColorUtility.TryParseHtmlString(GetTokenColor(tokenIndex, hasSubcommand), out color))
            return color;

        return GetOutputColor();
    }

    private static int GetTokenIndex(string command, int characterIndex)
    {
        if (string.IsNullOrEmpty(command) || characterIndex < 0 || characterIndex >= command.Length || command[characterIndex] == ' ')
            return -1;

        int tokenIndex = -1;
        for (int i = 0; i <= characterIndex; i++)
        {
            if (command[i] != ' ' && (i == 0 || command[i - 1] == ' '))
                tokenIndex++;
        }

        return tokenIndex;
    }

    private static bool HasSubcommand(ConsoleWidget console, string token)
    {
        if (ReferenceEquals(console, null) || string.IsNullOrEmpty(token))
            return false;

        try
        {
            MethodInfo getCommandFromName = console.GetType().GetMethod(
                "GetCommandFromName",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (getCommandFromName == null)
                return false;

            object[] arguments = { token, null };
            getCommandFromName.Invoke(console, arguments);
            ConsoleCommand command = arguments[1] as ConsoleCommand;
            if (command == null)
                return false;

            Type type = command.GetType();
            bool result;
            if (subcommandCache.TryGetValue(type, out result))
                return result;

            MethodInfo execute = type.GetMethod("Execute", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            MethodInfo getSubcommand = typeof(ConsoleCommand).GetMethod(
                "GetSubcommand",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            result = Calls(execute, getSubcommand);
            subcommandCache[type] = result;
            return result;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool Calls(MethodInfo method, MethodInfo target)
    {
        if (method == null || target == null || method.GetMethodBody() == null)
            return false;

        byte[] body = method.GetMethodBody().GetILAsByteArray();
        for (int i = 0; i + 4 < body.Length; i++)
        {
            if (body[i] != 0x28 && body[i] != 0x6F)
                continue;

            try
            {
                MethodBase called = method.Module.ResolveMethod(BitConverter.ToInt32(body, i + 1));
                if (called == target)
                    return true;
            }
            catch (ArgumentException)
            {
            }
        }

        return false;
    }

    private static string GetConfiguredColor(object value, string fallback)
    {
        string text = value as string;
        if (text == null || text.Length != 7 || text[0] != '#')
            return fallback;

        for (int i = 1; i < text.Length; i++)
        {
            if (!Uri.IsHexDigit(text[i]))
                return fallback;
        }

        return text.ToUpperInvariant();
    }
}

[RequireComponent(typeof(Text))]
internal sealed class ConsoleTextColorEffect : MonoBehaviour
{
    private string command;
    private bool commandEcho;
    private bool hasSubcommand;

    public void Configure(ConsoleWidget console, string command, bool commandEcho)
    {
        this.command = command ?? string.Empty;
        this.commandEcho = commandEcho;
        hasSubcommand = ConsoleColoring.HasCommandSubcommand(console, ConsoleColoring.GetFirstToken(this.command));

        Text text = GetComponent<Text>();
        if (text != null)
            text.SetVerticesDirty();
    }

    public void Apply(VertexHelper vertexHelper)
    {
        if (!enabled || !gameObject.activeInHierarchy)
            return;

        int prefixLength = commandEcho ? 2 : 0;
        for (int vertexIndex = 0; vertexIndex < vertexHelper.currentVertCount; vertexIndex++)
        {
            int textIndex = vertexIndex / 4;
            int commandIndex = textIndex - prefixLength;
            Color color = textIndex < prefixLength
                ? ConsoleColoring.GetOutputColor()
                : ConsoleColoring.GetCommandCharacterColor(command, commandIndex, hasSubcommand);

            UIVertex vertex = default(UIVertex);
            vertexHelper.PopulateUIVertex(ref vertex, vertexIndex);
            vertex.color = color;
            vertexHelper.SetUIVertex(vertex, vertexIndex);
        }
    }
}

[HarmonyPatch(typeof(Text), "OnPopulateMesh", new[] { typeof(VertexHelper) })]
internal static class Text_OnPopulateMesh_Patch
{
    public static void Postfix(Text __instance, VertexHelper toFill)
    {
        ConsoleTextColorEffect effect = __instance.GetComponent<ConsoleTextColorEffect>();
        if (effect != null)
            effect.Apply(toFill);
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

[HarmonyPatch(typeof(ConsoleWidget), "Submit")]
internal static class ConsoleWidget_Submit_Patch
{
    public static void Prefix(ConsoleWidget __instance)
    {
        ConsoleColoring.BeginCommandEcho(__instance);
    }

    public static void Postfix()
    {
        ConsoleColoring.EndCommandEcho();
    }

    public static Exception Finalizer(Exception __exception)
    {
        ConsoleColoring.EndCommandEcho();
        return __exception;
    }
}

[HarmonyPatch(typeof(InputField), "UpdateLabel")]
internal static class InputField_UpdateLabel_Patch
{
    public static void Postfix(InputField __instance)
    {
        ConsoleWidget console = __instance.GetComponentInParent<ConsoleWidget>();
        if (!ReferenceEquals(console, null) && console.input == __instance)
            ConsoleColoring.SetInputText(__instance, console);
    }
}

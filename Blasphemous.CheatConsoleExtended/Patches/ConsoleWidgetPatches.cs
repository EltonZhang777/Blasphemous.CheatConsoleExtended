using Blasphemous.CheatConsoleExtended;
using Blasphemous.ModdingAPI;
using Gameplay.GameControllers.Penitent;
using Gameplay.UI.Console;
using Gameplay.UI.Widgets;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using UnityEngine.UI;

namespace Blasphemous.CheatConsoleExtended
{

    internal static class ConsoleColoring
    {
        private static readonly Dictionary<Type, bool> subcommandCache = [];
        private static ConsoleWidget pendingEchoConsole;
        private static string pendingEchoText;
        private static bool inputColorUpdateInProgress;
        private static bool inputColorDiagnosticLogged;
        private static bool inputColorReentryDiagnosticLogged;
        private static bool inputColorCompletionDiagnosticLogged;

        /// <summary>Applies output coloring and preserves a matching pending command echo.</summary>
        public static void SetOutputText(Text text, string message)
        {
            ConsoleTextColorEffect effect = text.GetComponent<ConsoleTextColorEffect>();
            effect?.enabled = false;

            if (TryConsumeCommandEcho(message, out ConsoleWidget console, out string command))
            {
                SetCommandEchoText(text, console, command);
                return;
            }

            text.color = GetOutputColor();
            text.supportRichText = false;
            text.text = message ?? string.Empty;
        }

        /// <summary>Applies configured role colors to the console input without changing its text.</summary>
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

                ConsoleTextColorEffect effect = text.GetComponent<ConsoleTextColorEffect>() ?? text.gameObject.AddComponent<ConsoleTextColorEffect>();
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

        /// <summary>Captures the current input so its submitted command echo can be colored.</summary>
        public static void BeginCommandEcho(ConsoleWidget console)
        {
            pendingEchoConsole = null;
            pendingEchoText = null;

            if (console is null || console.input == null)
                return;

            string input = console.input.text;
            if (!string.IsNullOrEmpty(input) && input.Trim().Length > 0)
            {
                pendingEchoConsole = console;
                pendingEchoText = input;
            }
        }

        /// <summary>Clears the pending command echo state.</summary>
        public static void EndCommandEcho()
        {
            pendingEchoConsole = null;
            pendingEchoText = null;
        }

        /// <summary>Returns the configured output color, falling back to the default when needed.</summary>
        internal static Color GetOutputColor()
        {
            string value = GetConfiguredColor(
                Main.CheatConsoleExtended == null || Main.CheatConsoleExtended.Config == null
                    ? null
                    : Main.CheatConsoleExtended.Config.OutputColor,
                MasterConfig.DefaultOutputColor);

            if (ColorUtility.TryParseHtmlString(value, out Color color))
                return color;

            ColorUtility.TryParseHtmlString(MasterConfig.DefaultOutputColor, out color);
            return color;
        }

        /// <summary>Writes a command echo and applies role colors to its command text.</summary>
        private static void SetCommandEchoText(Text text, ConsoleWidget console, string command)
        {
            text.color = Color.white;
            text.supportRichText = false;
            text.text = "> " + (command ?? string.Empty);

            ConsoleTextColorEffect effect = text.GetComponent<ConsoleTextColorEffect>() ?? text.gameObject.AddComponent<ConsoleTextColorEffect>();
            effect.enabled = true;
            effect.Configure(console, command, true);
        }

        /// <summary>Matches a line against the pending echo and returns the captured console and command.</summary>
        private static bool TryConsumeCommandEcho(string message, out ConsoleWidget console, out string command)
        {
            console = pendingEchoConsole;
            command = pendingEchoText;

            if (console is null || command == null || message != "> " + command)
                return false;

            EndCommandEcho();
            return true;
        }

        /// <summary>Returns the first space-delimited token after any leading spaces.</summary>
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

        /// <summary>Selects the configured color for a token based on its position and command type.</summary>
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

        /// <summary>Reports whether the named top-level command uses subcommands.</summary>
        internal static bool HasCommandSubcommand(ConsoleWidget console, string token)
        {
            return HasSubcommand(console, token);
        }

        /// <summary>Returns the color for a command character, using the output color for spaces or invalid indices.</summary>
        internal static Color GetCommandCharacterColor(string command, int characterIndex, bool hasSubcommand)
        {
            int tokenIndex = GetTokenIndex(command, characterIndex);
            if (tokenIndex < 0)
                return GetOutputColor();

            if (ColorUtility.TryParseHtmlString(GetTokenColor(tokenIndex, hasSubcommand), out Color color))
                return color;

            return GetOutputColor();
        }

        /// <summary>Finds the token containing a character index, or returns -1 for spaces and invalid indices.</summary>
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

        /// <summary>Determines whether the resolved command's execution delegates to its subcommand handler.</summary>
        private static bool HasSubcommand(ConsoleWidget console, string token)
        {
            if (console is null || string.IsNullOrEmpty(token))
                return false;

            try
            {
                MethodInfo getCommandFromName = console.GetType().GetMethod(
                    "GetCommandFromName",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (getCommandFromName == null)
                    return false;

                object[] arguments = [token, null];
                getCommandFromName.Invoke(console, arguments);
                if (arguments[1] is not ConsoleCommand command)
                    return false;

                Type type = command.GetType();
                if (subcommandCache.TryGetValue(type, out bool result))
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

        /// <summary>Checks whether a method body directly calls the specified method.</summary>
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

        /// <summary>Returns a valid hexadecimal color in uppercase, or the supplied fallback.</summary>
        private static string GetConfiguredColor(object value, string fallback)
        {
            if (value is not string text || text.Length != 7 || text[0] != '#')
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

        /// <summary>Stores the command display state and invalidates the text mesh for recoloring.</summary>
        public void Configure(ConsoleWidget console, string command, bool commandEcho)
        {
            this.command = command ?? string.Empty;
            this.commandEcho = commandEcho;
            hasSubcommand = ConsoleColoring.HasCommandSubcommand(console, ConsoleColoring.GetFirstToken(this.command));

            Text text = GetComponent<Text>();
            text?.SetVerticesDirty();
        }

        /// <summary>Assigns vertex colors according to command-token roles and the echo prefix.</summary>
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

                UIVertex vertex = default;
                vertexHelper.PopulateUIVertex(ref vertex, vertexIndex);
                vertex.color = color;
                vertexHelper.SetUIVertex(vertex, vertexIndex);
            }
        }
    }

    [HarmonyPatch(typeof(Text), "OnPopulateMesh", [typeof(VertexHelper)])]
    internal static class Text_OnPopulateMesh_Patch
    {
        /// <summary>Applies the attached color effect after Unity populates a text mesh.</summary>
        public static void Postfix(Text __instance, VertexHelper toFill)
        {
            ConsoleTextColorEffect effect = __instance.GetComponent<ConsoleTextColorEffect>();
            effect?.Apply(toFill);
        }
    }

    [HarmonyPatch(typeof(ConsoleWidget), "Write")]
    internal static class ConsoleWidget_Write_Patch
    {
        /// <summary>Routes console text assignments through the output-coloring handler.</summary>
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
        /// <summary>Captures the input before the console writes its command echo.</summary>
        public static void Prefix(ConsoleWidget __instance)
        {
            ConsoleColoring.BeginCommandEcho(__instance);
        }

        /// <summary>Clears pending echo state after a successful console submission.</summary>
        public static void Postfix()
        {
            ConsoleColoring.EndCommandEcho();
        }

        /// <summary>Clears pending echo state after submission and preserves any exception.</summary>
        public static Exception Finalizer(Exception __exception)
        {
            ConsoleColoring.EndCommandEcho();
            return __exception;
        }
    }

    [HarmonyPatch(typeof(InputField), "UpdateLabel")]
    internal static class InputField_UpdateLabel_Patch
    {
        /// <summary>Applies role colors to the console input after its label is updated.</summary>
        public static void Postfix(InputField __instance)
        {
            ConsoleWidget console = __instance.GetComponentInParent<ConsoleWidget>();
            if (console is not null && console.input == __instance)
                ConsoleColoring.SetInputText(__instance, console);
        }
    }
}

namespace Blasphemous.CheatConsoleExtended.Patches
{
    [HarmonyPatch(typeof(ConsoleWidget), "Update")]
    internal static class ConsoleWidget_Update_PlayerToggle_Patch
    {
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return ConsoleWidget_SetEnabled_Async_Patch.RoutePlayerToggleThroughMarker(instructions);
        }
    }

    [HarmonyPatch]
    internal static class CheatConsole_Update_PlayerToggle_Patch
    {
        private static MethodBase TargetMethod()
        {
            Type patchType = typeof(Blasphemous.CheatConsole.CommandRegister).Assembly.GetType(
                "Blasphemous.CheatConsole.ConsoleWidget_Update_Patch",
                false);
            return FindPatchTarget(patchType);
        }

        internal static MethodInfo FindPatchTarget(Type patchType)
        {
            return patchType?.GetMethod(
                "Postfix",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(ConsoleWidget), typeof(bool) },
                null);
        }

        private static bool Prepare()
        {
            return ShouldPatch(
                TargetMethod(),
                ConsoleWidget_SetEnabled_Async_Patch.HasControllerPlayerInputRoute);
        }

        internal static bool ShouldPatch(MethodBase targetMethod, bool hasControllerPlayerInputRoute)
        {
            return targetMethod != null && !hasControllerPlayerInputRoute;
        }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return ConsoleWidget_SetEnabled_Async_Patch.RouteConfiguredPlayerToggleThroughMarker(
                instructions,
                null);
        }
    }

    internal sealed class PendingConsoleOpen
    {
        internal PendingConsoleOpen(bool isPlayerToggle)
        {
            ShouldOpen = true;
            HasPlayerToggle = isPlayerToggle;
        }

        internal bool ShouldOpen { get; private set; }
        internal bool HasPlayerToggle { get; private set; }

        internal void RecordRequest(bool isPlayerToggle)
        {
            if (!isPlayerToggle)
            {
                ShouldOpen = true;
                HasPlayerToggle = false;
                return;
            }

            if (HasPlayerToggle)
                ShouldOpen = !ShouldOpen;

            HasPlayerToggle = true;
        }
    }

    internal sealed class PlayerToggleFrameTracker
    {
        private readonly Dictionary<object, HashSet<KeyCode>> _keysByConsole = [];
        private readonly HashSet<object> _consolesWithUnknownBinding = [];
        private int _frameCount = int.MinValue;

        internal bool TryRegister(object console, int currentFrame, KeyCode? key)
        {
            if (_frameCount != currentFrame)
            {
                _keysByConsole.Clear();
                _consolesWithUnknownBinding.Clear();
                _frameCount = currentFrame;
            }

            if (key == null || key == KeyCode.None)
            {
                if (_consolesWithUnknownBinding.Contains(console)
                    || (_keysByConsole.TryGetValue(console, out HashSet<KeyCode> registeredKeys)
                        && registeredKeys.Count > 0))
                    return false;

                return _consolesWithUnknownBinding.Add(console);
            }

            if (_consolesWithUnknownBinding.Contains(console))
                return false;

            if (!_keysByConsole.TryGetValue(console, out HashSet<KeyCode> keys))
            {
                keys = [];
                _keysByConsole.Add(console, keys);
            }

            return keys.Add(key.Value);
        }
    }

    [HarmonyPatch(typeof(ConsoleWidget), "SetEnabled")]
    internal static class ConsoleWidget_SetEnabled_Async_Patch
    {
        private static readonly Dictionary<ConsoleWidget, PendingConsoleOpen> pendingOpens = [];
        private static readonly HashSet<ConsoleWidget> invokingOriginal = [];
        private static readonly HashSet<ConsoleWidget> playerToggleInProgress = [];
        private static readonly PlayerToggleFrameTracker playerToggleFrameTracker = new PlayerToggleFrameTracker();
        private static readonly System.Reflection.MethodInfo capturePlayerOpenRequest = FindControllerMethod(
            "TryCapturePlayerOpenRequest",
            typeof(bool),
            typeof(ConsoleWidget));
        private static readonly System.Reflection.MethodInfo setEnabledFromPlayerInput = FindControllerMethod(
            "SetEnabledFromPlayerInput",
            typeof(void),
            typeof(ConsoleWidget),
            typeof(bool));

        internal static bool HasControllerPlayerInputRoute => setEnabledFromPlayerInput != null;

        [HarmonyPrefix]
        private static bool Prefix(ConsoleWidget __instance, bool enabled)
        {
            if (invokingOriginal.Contains(__instance))
                return true;

            if (!enabled)
            {
                pendingOpens.Remove(__instance);
                return true;
            }

            bool isPlayerOpenRequest = CapturePlayerOpenRequest(__instance);
            if (pendingOpens.TryGetValue(__instance, out PendingConsoleOpen pendingOpen))
            {
                pendingOpen.RecordRequest(isPlayerOpenRequest);
                return false;
            }

            pendingOpens.Add(__instance, new PendingConsoleOpen(isPlayerOpenRequest));
            __instance.StartCoroutine(OpenNextFrame(__instance));
            return false;
        }

        private static IEnumerator OpenNextFrame(ConsoleWidget console)
        {
            // shortcut: activation still runs on Unity's main thread; split it only if the in-game check still stalls.
            yield return null;

            PendingConsoleOpen pendingOpen;
            if (console == null
                || !pendingOpens.TryGetValue(console, out pendingOpen)
                || !pendingOpens.Remove(console))
                yield break;

            if (!pendingOpen.ShouldOpen)
                yield break;

            invokingOriginal.Add(console);
            try
            {
                if (pendingOpen.HasPlayerToggle)
                    OpenFromPlayerInput(console);
                else
                    console.SetEnabled(true);
            }
            finally
            {
                invokingOriginal.Remove(console);
            }
        }

        internal static IEnumerable<CodeInstruction> RoutePlayerToggleThroughMarker(
            IEnumerable<CodeInstruction> instructions)
        {
            return RoutePlayerToggleThroughMarker(instructions, setEnabledFromPlayerInput);
        }

        internal static IEnumerable<CodeInstruction> RoutePlayerToggleThroughMarker(
            IEnumerable<CodeInstruction> instructions,
            MethodInfo controllerRoute)
        {
            return RoutePlayerToggleThroughMarker(instructions, controllerRoute, configuredHotkey: false);
        }

        internal static IEnumerable<CodeInstruction> RouteConfiguredPlayerToggleThroughMarker(
            IEnumerable<CodeInstruction> instructions,
            MethodInfo controllerRoute)
        {
            return RoutePlayerToggleThroughMarker(instructions, controllerRoute, configuredHotkey: true);
        }

        private static IEnumerable<CodeInstruction> RoutePlayerToggleThroughMarker(
            IEnumerable<CodeInstruction> instructions,
            MethodInfo controllerRoute,
            bool configuredHotkey)
        {
            MethodInfo setEnabled = AccessTools.Method(typeof(ConsoleWidget), "SetEnabled", [typeof(bool)]);
            MethodInfo marker = AccessTools.Method(
                typeof(ConsoleWidget_SetEnabled_Async_Patch),
                configuredHotkey
                    ? nameof(SetEnabledFromConfiguredPlayerToggle)
                    : nameof(SetEnabledFromPlayerToggle),
                [typeof(ConsoleWidget), typeof(bool)]);
            int recognizedRoutes = 0;

            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.operand is MethodInfo calledMethod)
                {
                    if (calledMethod == setEnabled || calledMethod == controllerRoute)
                    {
                        instruction.opcode = OpCodes.Call;
                        instruction.operand = marker;
                        recognizedRoutes++;
                    }
                    else if (calledMethod == marker)
                    {
                        recognizedRoutes++;
                    }
                }

                yield return instruction;
            }

            if (recognizedRoutes != 1)
                throw new InvalidOperationException($"Expected one player-toggle route, found {recognizedRoutes}.");
        }

        internal static void SetEnabledFromPlayerToggle(ConsoleWidget console, bool enabled)
        {
            SetEnabledFromPlayerToggle(console, enabled, KeyCode.F1);
        }

        internal static void SetEnabledFromConfiguredPlayerToggle(ConsoleWidget console, bool enabled)
        {
            SetEnabledFromPlayerToggle(console, enabled, GetConfiguredConsoleKey());
        }

        private static void SetEnabledFromPlayerToggle(ConsoleWidget console, bool enabled, KeyCode? inputKey)
        {
            if (!playerToggleFrameTracker.TryRegister(console, Time.frameCount, inputKey))
                return;

            bool addedMarker = playerToggleInProgress.Add(console);
            try
            {
                if (setEnabledFromPlayerInput != null)
                    setEnabledFromPlayerInput.Invoke(null, new object[] { console, enabled });
                else
                    console.SetEnabled(enabled);
            }
            finally
            {
                if (addedMarker)
                    playerToggleInProgress.Remove(console);
            }
        }

        private static KeyCode? GetConfiguredConsoleKey()
        {
            // shortcut: unknown bindings coalesce once per frame; use a public keybinding API if ModdingAPI adds one.
            Type mainType = AccessTools.TypeByName("Blasphemous.CheatConsole.Main");
            PropertyInfo cheatConsoleProperty = mainType?.GetProperty(
                "CheatConsole",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            object cheatConsole = cheatConsoleProperty?.GetValue(null, null);
            PropertyInfo inputHandlerProperty = cheatConsole?.GetType().GetProperty("InputHandler");
            object inputHandler = inputHandlerProperty?.GetValue(cheatConsole, null);
            if (inputHandler == null)
                return null;

            Dictionary<string, KeyCode> keybindings = Traverse.Create(inputHandler)
                .Field("_keybindings")
                .GetValue<Dictionary<string, KeyCode>>();
            return keybindings != null
                && keybindings.TryGetValue("Console", out KeyCode key)
                && key != KeyCode.None
                    ? key
                    : (KeyCode?)null;
        }

        private static bool CapturePlayerOpenRequest(ConsoleWidget console)
        {
            return playerToggleInProgress.Contains(console)
                || (capturePlayerOpenRequest != null
                    && (bool)capturePlayerOpenRequest.Invoke(null, new object[] { console }));
        }

        private static void OpenFromPlayerInput(ConsoleWidget console)
        {
            if (setEnabledFromPlayerInput == null)
            {
                console.SetEnabled(true);
                return;
            }

            setEnabledFromPlayerInput.Invoke(null, new object[] { console, true });
        }

        private static System.Reflection.MethodInfo FindControllerMethod(
            string methodName,
            Type returnType,
            params Type[] parameterTypes)
        {
            Type controllerType = typeof(ConsoleWidget_SetEnabled_Async_Patch).Assembly.GetType(
                "Blasphemous.CheatConsoleExtended.CheatConsoleController",
                false);
            System.Reflection.MethodInfo method = controllerType?.GetMethod(
                methodName,
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic,
                null,
                parameterTypes,
                null);

            return method != null && method.ReturnType == returnType ? method : null;
        }
    }

    [HarmonyPatch(typeof(ConsoleWidget), "OnPlayerSpawn", [typeof(Penitent)])]
    internal static class ConsoleWidget_OnPlayerSpawn_ApplyFont_Patch
    {
        /// <summary>Applies the selected font and refreshes console layout after the player spawns.</summary>
        [HarmonyPostfix]
        private static void Postfix(ConsoleWidget __instance)
        {
            ConsoleFontApplicator.ApplyTo(__instance);
            ConsoleFontApplicator.RefreshLayout(__instance);
        }
    }

    [HarmonyPatch(typeof(ConsoleWidget), "Write", [typeof(string)])]
    internal static class ConsoleWidget_Write_ApplyFont_Patch
    {
        /// <summary>Applies the selected font to the output line just written.</summary>
        [HarmonyPostfix]
        private static void Postfix(ConsoleWidget __instance)
        {
            ConsoleFontApplicator.ApplyToLatestOutput(__instance);
        }
    }

    internal static class ConsoleFontApplicator
    {
        /// <summary>Applies the selected font and refreshes layout on the active console.</summary>
        internal static void ApplyCurrent()
        {
            var console = ConsoleWidget.Instance;
            if (console != null)
            {
                ApplyTo(console);
                RefreshLayout(console);
            }
        }

        /// <summary>Applies the selected font to input, placeholder, and existing output.</summary>
        internal static void ApplyTo(ConsoleWidget console)
        {
            var font = GetCurrentFont();
            if (console == null || font == null)
            {
                return;
            }

            if (console.input != null)
            {
                console.input.textComponent?.font = font;

                if (console.input.placeholder != null)
                {
                    var placeholderText = console.input.placeholder.GetComponent<Text>();
                    placeholderText?.font = font;
                }
            }

            if (console.content == null)
            {
                return;
            }

            for (var i = 0; i < console.content.childCount; i++)
            {
                var text = console.content.GetChild(i).GetComponent<Text>();
                text?.font = font;
            }

            Canvas.ForceUpdateCanvases();
            for (var i = 0; i < console.content.childCount; i++)
            {
                var text = console.content.GetChild(i).GetComponent<Text>();
                if (text != null)
                {
                    ApplyOutputLayout(text, font);
                }
            }
        }

        /// <summary>Applies the selected font and layout sizing to the latest output line.</summary>
        internal static void ApplyToLatestOutput(ConsoleWidget console)
        {
            var font = GetCurrentFont();
            if (console == null || font == null || console.content == null || console.content.childCount == 0)
            {
                return;
            }

            var text = console.content.GetChild(console.content.childCount - 1).GetComponent<Text>();
            if (text != null)
            {
                text.font = font;
                Canvas.ForceUpdateCanvases();
                ApplyOutputLayout(text, font);
                LayoutRebuilder.ForceRebuildLayoutImmediate(console.content);
            }
        }

        /// <summary>Preserves a rendered output row's layout height after changing its font.</summary>
        private static void ApplyOutputLayout(Text text, Font font)
        {
            var layoutElement = text.GetComponent<LayoutElement>() ?? text.gameObject.AddComponent<LayoutElement>();
            var preferredHeight = text.preferredHeight;
            var lineCount = Mathf.Max(1, text.cachedTextGeneratorForLayout.lineCount);
            var lineHeight = font.lineHeight / Mathf.Max(1f, text.pixelsPerUnit);
            var rowHeight = lineHeight > 0f ? lineCount * lineHeight : preferredHeight;
            if (preferredHeight > rowHeight)
            {
                rowHeight = preferredHeight;
            }

            if (rowHeight > 0f)
            {
                layoutElement.preferredHeight = rowHeight;
            }
        }

        /// <summary>Rebuilds the active console's output layout after a font change.</summary>
        internal static void RefreshLayout(ConsoleWidget console)
        {
            if (console.content == null)
            {
                return;
            }

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(console.content);
        }

        /// <summary>Gets the font currently selected by the mod's font manager.</summary>
        private static Font GetCurrentFont()
        {
            var mod = Main.CheatConsoleExtended;
            return mod == null || mod.ConsoleFont == null ? null : mod.ConsoleFont.CurrentFont;
        }
    }
}

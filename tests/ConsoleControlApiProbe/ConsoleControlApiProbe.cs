using System;
using System.Collections;
using System.Collections.Generic;
using BepInEx;
using Blasphemous.CheatConsole;
using Blasphemous.CheatConsoleExtended;
using Blasphemous.ModdingAPI;
using Gameplay.UI.Console;
using Gameplay.UI.Widgets;
using UnityEngine;
using UnityEngine.UI;

namespace Blasphemous.CheatConsoleExtended.Tests;

[BepInPlugin("EltonZhang777.ConsoleControlApiProbe", "Console Control API Probe", "0.1.0")]
[BepInDependency("Blasphemous.CheatConsoleExtended")]
[BepInDependency("Blasphemous.ModdingAPI", "3.0.1")]
internal sealed class ConsoleControlApiProbe : BaseUnityPlugin
{
    internal const string SentinelOutputPrefix = "[Console API Probe] Sentinel output ";

    internal static int SentinelExecutionCount;

    private bool _hasRun;
    private bool _manualStageReady;

    private void Awake()
    {
        _ = new ConsoleControlApiProbeMod();

        if (ConsoleWidget.Instance != null)
        {
            Logger.LogWarning("[Console API Probe] Pre-widget checks skipped; the widget already exists.");
            return;
        }

        bool userWasEnabled = CheatConsoleController.IsUserEnabled;
        bool apiWasEnabled = CheatConsoleController.IsApiEnabled;
        try
        {
            Check(!CheatConsoleController.IsConsoleWidgetOpen, "closed state before widget");
            Check(userWasEnabled, "user access enabled before widget");
            Check(apiWasEnabled, "API access enabled before widget");
            Check(!CheatConsoleController.ExecuteCommand("__api_probe_before_widget__"), "reject before widget");

            CheatConsoleController.SetUserEnabled(false);
            Check(!CheatConsoleController.IsUserEnabled, "set user policy before widget");
            CheatConsoleController.SetApiAccessEnabled(false);
            Check(!CheatConsoleController.IsApiEnabled, "set API policy before widget");
            CheatConsoleController.SetUserEnabled(userWasEnabled);
            CheatConsoleController.SetApiAccessEnabled(apiWasEnabled);

            CheckThrowsBeforeWidget(false);
            CheckThrowsBeforeWidget(true);
            Logger.LogInfo("[Console API Probe] PASS pre-widget state, open rejection, and no admission.");
            Logger.LogInfo("[Console API Probe] Wait for the main menu, then press F8 and wait for PASS live API checks before using F7 or consoleapiprobe reentry.");
        }
        catch (Exception exception)
        {
            Logger.LogError("[Console API Probe] FAIL pre-widget checks: " + exception);
        }
        finally
        {
            CheatConsoleController.SetUserEnabled(userWasEnabled);
            CheatConsoleController.SetApiAccessEnabled(apiWasEnabled);
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F9))
        {
            CheatConsoleController.SetUserEnabled(true);
            CheatConsoleController.SetApiAccessEnabled(true);
            _manualStageReady = false;
            _hasRun = false;
            Logger.LogInfo("[Console API Probe] User and API access restored. Submit the retained command again to verify recovery; press F8 and wait for PASS before another F7 cycle.");
            return;
        }

        if (Input.GetKeyDown(KeyCode.F7))
        {
            if (!_manualStageReady)
            {
                Logger.LogWarning("[Console API Probe] F7 ignored; press F8 and wait for PASS live API checks first.");
                return;
            }

            CheatConsoleController.SetUserEnabled(false);
            _manualStageReady = false;
            Logger.LogInfo("[Console API Probe] MANUAL phase 2: API access remains disabled and user access is now disabled. Console open state must not change. The configured Console hotkey and F1 must still toggle the console. Enter help and press Enter; expect only the user-access Error in console and ModLog, with no help list. Press F9 to restore.");
            return;
        }

        if (_hasRun || !Input.GetKeyDown(KeyCode.F8))
            return;

        _hasRun = true;
        Logger.LogInfo("[Console API Probe] F8 received; running live API checks.");
        StartCoroutine(RunAvailableWidgetChecks());
    }

    private IEnumerator RunAvailableWidgetChecks()
    {
        ConsoleWidget console = null;
        if (!TryRunChecks(() =>
        {
            console = ConsoleWidget.Instance;
            Check(console != null, "widget is available");

            CheatConsoleController.SetConsoleOpen(false);
            Check(!CheatConsoleController.IsConsoleWidgetOpen, "API closes the widget");
            CheatConsoleController.SetConsoleOpen(true);
        }))
            yield break;

        yield return null;

        if (!TryRunChecks(() =>
        {
            Check(CheatConsoleController.IsConsoleWidgetOpen, "API opens the widget");
            CheatConsoleController.SetConsoleOpen(false);
            Check(!CheatConsoleController.IsConsoleWidgetOpen, "API closes the widget again");

            int initialSentinelCount = ConsoleControlApiProbe.SentinelExecutionCount;
            Check(CheatConsoleController.ExecuteCommand("consoleapiprobe sentinel"), "sentinel command admitted");
            Check(!CheatConsoleController.IsConsoleWidgetOpen, "sentinel command leaves UI closed");
            Check(ConsoleControlApiProbe.SentinelExecutionCount == initialSentinelCount + 1, "sentinel command executes through the parser");
            Check(ContainsOutput(console, SentinelOutputPrefix + (initialSentinelCount + 1) + "."), "API command uses the console output path");
            Check(CheatConsoleController.ExecuteCommand("__console_api_probe_missing__"), "unrecognized command still reports parser admission");

            CheatConsoleController.SetUserEnabled(false);
            try
            {
                int userDisabledSentinelCount = ConsoleControlApiProbe.SentinelExecutionCount;
                Check(CheatConsoleController.ExecuteCommand("consoleapiprobe sentinel"), "API command admitted while user access is disabled");
                Check(ConsoleControlApiProbe.SentinelExecutionCount == userDisabledSentinelCount + 1, "API command remains independent of user access");
                Check(ContainsOutput(console, SentinelOutputPrefix + (userDisabledSentinelCount + 1) + "."), "user-disabled API command writes output");
            }
            finally
            {
                CheatConsoleController.SetUserEnabled(true);
            }

            CheatConsoleController.SetApiAccessEnabled(false);
            try
            {
                int blockedSentinelCount = ConsoleControlApiProbe.SentinelExecutionCount;
                Check(!CheatConsoleController.ExecuteCommand("help"), "facade rejects command while API access is disabled");
                console.ProcessCommand("consoleapiprobe sentinel");
                Check(ConsoleControlApiProbe.SentinelExecutionCount == blockedSentinelCount, "direct vanilla ProcessCommand is blocked");
                console.input.text = "consoleapiprobe sentinel";
                console.Submit();
                Check(ConsoleControlApiProbe.SentinelExecutionCount == blockedSentinelCount, "direct vanilla Submit is blocked");
                console.SetEnabled(true);
                CheatConsoleController.SetConsoleOpen(true);
                Check(!CheatConsoleController.IsConsoleWidgetOpen, "direct and facade open requests are blocked");
                CheatConsoleController.SetConsoleOpen(false);
                Check(!CheatConsoleController.IsConsoleWidgetOpen, "closing remains available while API access is disabled");
                console.input.text = string.Empty;
            }
            finally
            {
                CheatConsoleController.SetApiAccessEnabled(true);
            }

            CheatConsoleController.SetConsoleOpen(true);
        }))
            yield break;

        yield return null;

        if (TryRunChecks(() =>
        {
            Check(CheatConsoleController.IsConsoleWidgetOpen, "API reopens the widget for inspection");
            Check(OutputRowsUseCurrentLayout(console), "closed output rows are laid out after reopening");
        }))
        {
            CheatConsoleController.SetUserEnabled(true);
            CheatConsoleController.SetApiAccessEnabled(false);
            CheatConsoleController.SetConsoleOpen(false);
            _manualStageReady = true;
            Logger.LogInfo("[Console API Probe] PASS live API checks. MANUAL phase 1: API access is disabled; the configured Console hotkey and F1 must still open the console. Enter help and press Enter; the help list should run because user access remains enabled. Then enter `consoleapiprobe reentry` and press Enter. The command should close the console; reopen it with either hotkey and verify its PASS line, with no nested help output. Then press F7 to verify user-policy independence.");
        }
    }

    private bool TryRunChecks(Action checks)
    {
        try
        {
            checks();
            return true;
        }
        catch (Exception exception)
        {
            Logger.LogError("[Console API Probe] FAIL live API checks: " + exception);
            return false;
        }
    }

    private static bool OutputRowsUseCurrentLayout(ConsoleWidget console)
    {
        if (console.content == null)
            return false;

        Canvas.ForceUpdateCanvases();
        for (int index = 0; index < console.content.childCount; index++)
        {
            Text output = console.content.GetChild(index).GetComponent<Text>();
            if (output == null)
                continue;

            LayoutElement layout = output.GetComponent<LayoutElement>();
            if (layout == null)
                return false;

            float lineHeight = output.font == null ? 0f : output.font.lineHeight / Mathf.Max(1f, output.pixelsPerUnit);
            float expectedHeight = lineHeight > 0f
                ? Mathf.Max(output.preferredHeight, Mathf.Max(1, output.cachedTextGeneratorForLayout.lineCount) * lineHeight)
                : output.preferredHeight;
            if (Mathf.Abs(layout.preferredHeight - expectedHeight) > 1f)
                return false;
        }

        return true;
    }

    private static bool ContainsOutput(ConsoleWidget console, string expected)
    {
        if (console.content == null)
            return false;

        for (int index = 0; index < console.content.childCount; index++)
        {
            Text output = console.content.GetChild(index).GetComponent<Text>();
            if (output != null && output.text == expected)
                return true;
        }

        return false;
    }

    private static void CheckThrowsBeforeWidget(bool open)
    {
        try
        {
            CheatConsoleController.SetConsoleOpen(open);
        }
        catch (InvalidOperationException)
        {
            return;
        }

        throw new InvalidOperationException("SetConsoleOpen should throw before the widget exists.");
    }

    private static void Check(bool condition, string label)
    {
        if (!condition)
            throw new InvalidOperationException("Expected " + label + ".");
    }
}

internal sealed class ConsoleControlApiProbeMod : BlasMod
{
    internal ConsoleControlApiProbeMod()
        : base("EltonZhang777.ConsoleControlApiProbe.Commands", "Console Control API Probe", "NewbieElton", "0.1.0")
    {
    }

    protected override void OnRegisterServices(ModServiceProvider provider)
    {
        CommandRegister.RegisterCommand(provider, new ConsoleApiReentryProbeCommand());
    }
}

internal sealed class ConsoleApiReentryProbeCommand : ModCommand
{
    protected override string CommandName => "consoleapiprobe";

    protected override bool AllowUppercase => false;

    protected override Dictionary<string, Action<string[]>> AddSubCommands()
    {
        return new Dictionary<string, Action<string[]>>
        {
            ["reentry"] = RunReentryCheck,
            ["sentinel"] = RunSentinel
        };
    }

    private void RunSentinel(string[] parameters)
    {
        ConsoleControlApiProbe.SentinelExecutionCount++;
        Write(ConsoleControlApiProbe.SentinelOutputPrefix + ConsoleControlApiProbe.SentinelExecutionCount + ".");
    }

    private void RunReentryCheck(string[] parameters)
    {
        if (parameters.Length != 0)
        {
            Write("consoleapiprobe reentry takes no parameters.");
            return;
        }

        if (CheatConsoleController.IsApiEnabled)
        {
            Write("[Console API Probe] BLOCKED: API access is enabled. Press F8 and wait for PASS live API checks first.");
            return;
        }

        ConsoleWidget console = ConsoleWidget.Instance;
        int initialSentinelCount = ConsoleControlApiProbe.SentinelExecutionCount;
        console.ProcessCommand("consoleapiprobe sentinel");
        bool nestedProcessCommandBlocked = ConsoleControlApiProbe.SentinelExecutionCount == initialSentinelCount;

        console.SetEnabled(false);
        console.SetEnabled(true);
        bool directOpenBlocked = !console.IsEnabled();

        if (nestedProcessCommandBlocked && directOpenBlocked)
        {
            Write("[Console API Probe] PASS reentrant mod command could not bypass disabled API access.");
            return;
        }

        Write("[Console API Probe] FAIL reentry checks: nested command "
            + (nestedProcessCommandBlocked ? "blocked" : "executed")
            + "; console reopening "
            + (directOpenBlocked ? "blocked" : "allowed")
            + ".");
    }
}

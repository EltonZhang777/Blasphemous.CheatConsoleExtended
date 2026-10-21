using System;
using System.Collections;
using BepInEx;
using Blasphemous.CheatConsoleExtended;
using Gameplay.UI.Widgets;
using UnityEngine;
using UnityEngine.UI;

namespace Blasphemous.CheatConsoleExtended.Tests;

[BepInPlugin("EltonZhang777.ConsoleControlApiProbe", "Console Control API Probe", "0.1.0")]
[BepInDependency("Blasphemous.CheatConsoleExtended")]
internal sealed class ConsoleControlApiProbe : BaseUnityPlugin
{
    private const string MissingCommandOutput = "Command not found. Use Help for more information.";

    private bool _hasRun;

    private void Awake()
    {
        if (ConsoleWidget.Instance != null)
        {
            Logger.LogWarning("[Console API Probe] Pre-widget checks skipped; the widget already exists.");
            return;
        }

        try
        {
            Check(!CheatConsoleController.IsConsoleWidgetOpen, "closed state before widget");
            Check(!CheatConsoleController.ExecuteCommand("__api_probe_before_widget__"), "reject before widget");
            CheckThrowsBeforeWidget(false);
            CheckThrowsBeforeWidget(true);
            Logger.LogInfo("[Console API Probe] PASS pre-widget state, open rejection, and no admission.");
        }
        catch (Exception exception)
        {
            Logger.LogError("[Console API Probe] FAIL pre-widget checks: " + exception);
        }
    }

    private void Update()
    {
        if (_hasRun || !Input.GetKeyDown(KeyCode.F8))
            return;

        _hasRun = true;
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

            int initialLineCount = console.content.childCount;
            Check(CheatConsoleController.ExecuteCommand("help"), "help command admitted");
            Check(!CheatConsoleController.IsConsoleWidgetOpen, "help leaves UI closed");
            Check(console.content.childCount > initialLineCount, "help writes output while UI is closed");

            Check(CheatConsoleController.ExecuteCommand("__api_probe_missing_command__"), "unknown command admitted");
            Check(!CheatConsoleController.IsConsoleWidgetOpen, "unknown command leaves UI closed");
            Check(ContainsOutput(console, MissingCommandOutput), "parser output uses vanilla Write path");

            CheatConsoleController.SetConsoleOpen(true);
        }))
            yield break;

        yield return null;

        TryRunChecks(() =>
        {
            Check(CheatConsoleController.IsConsoleWidgetOpen, "API reopens the widget for inspection");
            Logger.LogInfo("[Console API Probe] PASS live API checks. Console left open for output inspection; close it with the normal hotkey.");
        });
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

    private static bool ContainsOutput(ConsoleWidget console, string expected)
    {
        if (console.content == null)
            return false;

        for (int index = 0; index < console.content.childCount; index++)
        {
            Text output = console.content.GetChild(index).GetComponent<Text>();
            if (output != null && output.text.IndexOf(expected, StringComparison.Ordinal) >= 0)
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

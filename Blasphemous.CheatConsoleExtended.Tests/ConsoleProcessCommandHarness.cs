using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using Blasphemous.CheatConsole;
using Gameplay.UI.Console;
using Gameplay.UI.Widgets;
using HarmonyLib;
using Xunit;

namespace Blasphemous.CheatConsoleExtended.Tests;

internal sealed class ConsoleProcessCommandHarness : IDisposable
{
    private static ConsoleProcessCommandHarness active;

    private readonly Harmony harmony;
    private readonly MethodInfo processPrefix;
    private readonly MethodInfo processFinalizer;
    private readonly bool previousApiAccess;
    private ConsoleCommand command;
    private Action sharedCommandExecution;

    internal ConsoleProcessCommandHarness()
    {
        // shortcut: Unity-only ProcessCommand cannot run headless; use an in-game fixture when one is available.
        Writes = [];
        active = this;
        harmony = new("Blasphemous.CheatConsoleExtended.Tests.ConsoleProcessCommandHarness");

        MethodInfo writeMethod = AccessTools.Method(typeof(ConsoleWidget), "Write", [typeof(string)])
            ?? throw new InvalidOperationException("ConsoleWidget.Write(string) was not found.");
        MethodInfo captureMethod = typeof(ConsoleProcessCommandHarness).GetMethod(
            nameof(CaptureConsoleWrite),
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ConsoleWidget.Write probe was not found.");
        harmony.Patch(writeMethod, prefix: new HarmonyMethod(captureMethod));

        processPrefix = AccessTools.Method(typeof(ConsoleWidget_ProcessCommand_Patch), "Prefix")
            ?? throw new InvalidOperationException("ConsoleWidget.ProcessCommand prefix was not found.");
        processFinalizer = AccessTools.Method(typeof(ConsoleWidget_ProcessCommand_Patch), "Finalizer")
            ?? throw new InvalidOperationException("ConsoleWidget.ProcessCommand finalizer was not found.");
        MethodInfo strictProcess = AccessTools.Method(typeof(StrictCommandRuntime), "Process")
            ?? throw new InvalidOperationException("Strict command processor was not found.");
        MethodInfo resolveCommand = typeof(ConsoleProcessCommandHarness).GetMethod(
            nameof(ResolveTestCommand),
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Test command resolver was not found.");
        harmony.Patch(strictProcess, prefix: new HarmonyMethod(resolveCommand));

        MethodInfo modCommandWrite = AccessTools.Method(typeof(ModCommand), "Write", [typeof(string)])
            ?? throw new InvalidOperationException("ModCommand.Write(string) was not found.");
        MethodInfo captureModWrite = typeof(ConsoleProcessCommandHarness).GetMethod(
            nameof(CaptureModCommandWrite),
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ModCommand.Write probe was not found.");
        harmony.Patch(modCommandWrite, prefix: new HarmonyMethod(captureModWrite));

        MethodInfo sharedCommand = AccessTools.Method(
            typeof(SharedCommandsCommand),
            nameof(SharedCommandsCommand.ExecuteIfIsCommand),
            [typeof(string)])
            ?? throw new InvalidOperationException("Shared command execution method was not found.");
        MethodInfo executeSharedCommand = typeof(ConsoleProcessCommandHarness).GetMethod(
            nameof(ExecuteTestSharedCommand),
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Shared command probe was not found.");
        harmony.Patch(sharedCommand, prefix: new HarmonyMethod(executeSharedCommand));
        previousApiAccess = CheatConsoleController.IsApiEnabled;
        CheatConsoleController.SetApiAccessEnabled(true);
        Console = (ConsoleWidget)FormatterServices.GetUninitializedObject(typeof(ConsoleWidget));
    }

    internal ConsoleWidget Console { get; }

    internal List<string> Writes { get; }

    internal Exception WriteException { get; set; }

    internal ConsoleCommand RegisterCommand(string name, Action<string[]> execute)
    {
        HarnessConsoleCommand testCommand =
            (HarnessConsoleCommand)FormatterServices.GetUninitializedObject(typeof(HarnessConsoleCommand));
        testCommand.Initialize(name, execute);
        command = testCommand;
        return command;
    }

    internal void ExecuteCommand(ConsoleCommand command, string name, params string[] parameters)
    {
        command.Execute(name, parameters);
    }

    internal void SetSharedCommandExecution(Action execute)
    {
        sharedCommandExecution = execute;
    }

    internal object StartCommand(string rawText = null)
    {
        object[] prefixArguments = { Console, rawText ?? string.Empty, null };
        bool runOriginal = (bool)processPrefix.Invoke(null, prefixArguments);
        Assert.Equal(!string.IsNullOrEmpty(rawText), runOriginal);
        return prefixArguments[2];
    }

    internal Exception FinishCommand(object state, Exception exception)
    {
        return (Exception)processFinalizer.Invoke(null, new[] { exception, state });
    }

    internal void FinishCommandIfNeeded(ref object state)
    {
        if (state == null)
            return;

        try
        {
            FinishCommand(state, null);
        }
        finally
        {
            state = null;
        }
    }

    internal void Write(string message)
    {
        Assert.False(ConsoleWidget_Write_Patch.Prefix(ref message));
    }

    public void Dispose()
    {
        CheatConsoleController.SetApiAccessEnabled(previousApiAccess);
        harmony.UnpatchSelf();
        active = null;
    }

    private static bool CaptureConsoleWrite(string message)
    {
        if (active.WriteException != null)
            throw active.WriteException;

        active.Writes.Add(message);
        return false;
    }

    private static bool ResolveTestCommand(
        ConsoleWidget console,
        string rawText,
        ref ConsoleCommand resolvedCommand,
        ref bool __result)
    {
        if (active.command == null || string.IsNullOrEmpty(rawText))
            return true;

        resolvedCommand = active.command;
        __result = true;
        return false;
    }

    private static bool CaptureModCommandWrite(ModCommand __instance, string text)
    {
        active.Write(text);
        return false;
    }

    private static bool ExecuteTestSharedCommand(SharedCommandsCommand __instance, ref bool __result)
    {
        if (active.sharedCommandExecution == null)
            return true;

        active.sharedCommandExecution();
        __result = true;
        return false;
    }

    private sealed class HarnessConsoleCommand : ConsoleCommand
    {
        private string name;
        private Action<string[]> execute;

        internal void Initialize(string name, Action<string[]> execute)
        {
            this.name = name;
            this.execute = execute;
        }

        public override void Execute(string name, string[] parameters)
        {
            execute(parameters);
        }

        public override List<string> GetNames()
        {
            return [name];
        }
    }
}
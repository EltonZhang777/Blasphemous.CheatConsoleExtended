using Blasphemous.CheatConsole;
using Blasphemous.ModdingAPI;
using System;
using System.Collections.Generic;

namespace Blasphemous.CheatConsoleExtended;

internal sealed class ConsoleFontCommand : ModCommand
{
    protected override string CommandName => "consolefont";

    protected override bool AllowUppercase => false;

    protected override Dictionary<string, Action<string[]>> AddSubCommands()
    {
        return new Dictionary<string, Action<string[]>>
        {
            { "help", ShowHelp },
            { "set", SetFont },
            { "reset", ResetFont },
            { "list", ListFonts },
        };
    }

    private void ShowHelp(string[] parameters)
    {
        Write("consolefont set <font family name>: select an installed system font.");
        Write("consolefont reset: restore Consolas, falling back to Arial.");
        Write("consolefont list: list names returned by Unity's system-font API.");
        Write("Font family names are not TTF file names.");
    }

    private void SetFont(string[] parameters)
    {
        if (parameters == null || parameters.Length == 0)
        {
            Write("Usage: consolefont set <font family name>");
            return;
        }

        var fontName = string.Join(" ", parameters).Trim();
        var mod = Main.CheatConsoleExtended;
        if (mod == null || mod.ConsoleFont == null)
        {
            Write("Warning: console font service is not initialized.");
            return;
        }

        if (!mod.ConsoleFont.TrySetFont(fontName))
        {
            ModLog.Warn($"ConsoleFont: requested system font '{fontName}' was not resolved.");
            Write($"Warning: system font '{fontName}' was not found; configuration unchanged.");
            return;
        }

        mod.SaveConfig();
        Patches.ConsoleFontApplicator.ApplyCurrent();
        Write($"Console font set to '{mod.ConsoleFont.CurrentFontName}'.");
    }

    private void ResetFont(string[] parameters)
    {
        var mod = Main.CheatConsoleExtended;
        if (mod == null || mod.ConsoleFont == null)
        {
            Write("Warning: console font service is not initialized.");
            return;
        }

        mod.ConsoleFont.Reset();
        mod.SaveConfig();
        Patches.ConsoleFontApplicator.ApplyCurrent();
        Write($"Console font reset to '{mod.ConsoleFont.CurrentFontName}'.");
    }

    private void ListFonts(string[] parameters)
    {
        var mod = Main.CheatConsoleExtended;
        if (mod == null || mod.ConsoleFont == null)
        {
            Write("Warning: console font service is not initialized.");
            return;
        }

        var fontNames = mod.ConsoleFont.GetInstalledFontNames();
        Write("Installed system fonts:");
        for (var i = 0; i < fontNames.Length; i++)
        {
            Write(fontNames[i]);
        }
    }
}
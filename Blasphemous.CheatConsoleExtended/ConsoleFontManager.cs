using System;
using Blasphemous.ModdingAPI;
using UnityEngine;

namespace Blasphemous.CheatConsoleExtended;

internal sealed class ConsoleFontManager
{
    private const string FallbackFontName = "Arial";
    private const int DynamicFontSize = 22;

    private readonly MasterConfig _config;

    internal Font CurrentFont { get; private set; }

    internal string CurrentFontName { get; private set; }

    internal ConsoleFontManager(MasterConfig config)
    {
        _config = config;
        Reload();
    }

    internal string[] GetInstalledFontNames()
    {
        try
        {
            return Font.GetOSInstalledFontNames() ?? new string[0];
        }
        catch (Exception)
        {
            return new string[0];
        }
    }

    internal bool TrySetFont(string fontName)
    {
        var installedFontNames = GetInstalledFontNames();
        var font = TryCreateFont(fontName, installedFontNames, out var resolvedFontName);
        if (font == null)
        {
            return false;
        }

        _config.ConsoleFont = resolvedFontName;
        CurrentFont = font;
        CurrentFontName = resolvedFontName;
        return true;
    }

    internal void Reset()
    {
        _config.ConsoleFont = MasterConfig.DefaultConsoleFont;
        Reload();
    }

    private void Reload()
    {
        var installedFontNames = GetInstalledFontNames();
        var configuredFontName = _config.ConsoleFont;
        var font = TryCreateFont(configuredFontName, installedFontNames, out var resolvedFontName);

        if (font == null)
        {
            font = TryCreateFont(MasterConfig.DefaultConsoleFont, installedFontNames, out resolvedFontName);
        }

        if (font == null)
        {
            font = TryCreateFont(FallbackFontName, installedFontNames, out resolvedFontName);
        }

        if (font == null)
        {
            ModLog.Error("ConsoleFont: no usable installed font was found; keeping the previous font.");
            return;
        }

        if (!string.Equals(configuredFontName, resolvedFontName, StringComparison.OrdinalIgnoreCase))
        {
            ModLog.Warn($"ConsoleFont: '{configuredFontName}' is unavailable; using '{resolvedFontName}'.");
        }

        CurrentFont = font;
        CurrentFontName = resolvedFontName;
    }

    private static Font TryCreateFont(string requestedFontName, string[] installedFontNames, out string resolvedFontName)
    {
        resolvedFontName = null;
        if (string.IsNullOrEmpty(requestedFontName) || requestedFontName.Trim().Length == 0)
        {
            return null;
        }

        foreach (var installedFontName in installedFontNames)
        {
            if (!string.Equals(installedFontName, requestedFontName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                var font = Font.CreateDynamicFontFromOSFont(installedFontName, DynamicFontSize);
                if (font == null)
                {
                    return null;
                }

                resolvedFontName = installedFontName;
                return font;
            }
            catch (Exception)
            {
                return null;
            }
        }

        return null;
    }
}

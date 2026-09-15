using Gameplay.GameControllers.Penitent;
using Gameplay.UI.Widgets;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace Blasphemous.CheatConsoleExtended.Patches;

[HarmonyPatch(typeof(ConsoleWidget), "OnPlayerSpawn", new[] { typeof(Penitent) })]
internal static class ConsoleWidget_OnPlayerSpawn_ApplyFont_Patch
{
    [HarmonyPostfix]
    private static void Postfix(ConsoleWidget __instance)
    {
        ConsoleFontApplicator.ApplyTo(__instance);
    }
}

[HarmonyPatch(typeof(ConsoleWidget), "Write", new[] { typeof(string) })]
internal static class ConsoleWidget_Write_ApplyFont_Patch
{
    [HarmonyPostfix]
    private static void Postfix(ConsoleWidget __instance)
    {
        ConsoleFontApplicator.ApplyToLatestOutput(__instance);
    }
}

internal static class ConsoleFontApplicator
{
    internal static void ApplyCurrent()
    {
        var console = ConsoleWidget.Instance;
        if (console != null)
        {
            ApplyTo(console);
            RefreshLayout(console);
        }
    }

    internal static void ApplyTo(ConsoleWidget console)
    {
        var font = GetCurrentFont();
        if (console == null || font == null)
        {
            return;
        }

        if (console.input != null)
        {
            if (console.input.textComponent != null)
            {
                console.input.textComponent.font = font;
            }

            if (console.input.placeholder != null)
            {
                var placeholderText = console.input.placeholder.GetComponent<Text>();
                if (placeholderText != null)
                {
                    placeholderText.font = font;
                }
            }
        }

        if (console.content == null)
        {
            return;
        }

        for (var i = 0; i < console.content.childCount; i++)
        {
            var text = console.content.GetChild(i).GetComponent<Text>();
            if (text != null)
            {
                ApplyOutputFont(text, font);
            }
        }
    }

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
            ApplyOutputFont(text, font);
        }
    }

    private static void ApplyOutputFont(Text text, Font font)
    {
        var nativeHeight = text.preferredHeight;
        var layoutElement = text.GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = text.gameObject.AddComponent<LayoutElement>();
        }

        if (layoutElement.preferredHeight < 0f && nativeHeight > 0f)
        {
            layoutElement.preferredHeight = nativeHeight;
        }

        text.font = font;
    }

    private static void RefreshLayout(ConsoleWidget console)
    {
        if (console.content == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(console.content);
        if (console.scrollRect != null)
        {
            console.scrollRect.verticalNormalizedPosition = 0f;
        }
    }

    private static Font GetCurrentFont()
    {
        var mod = Main.CheatConsoleExtended;
        return mod == null || mod.ConsoleFont == null ? null : mod.ConsoleFont.CurrentFont;
    }
}

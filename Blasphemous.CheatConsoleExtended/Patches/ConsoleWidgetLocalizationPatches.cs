using Blasphemous.CheatConsoleExtended;
using Gameplay.UI.Widgets;
using HarmonyLib;
using UnityEngine.UI;

namespace Blasphemous.CheatConsoleExtended.Patches;

[HarmonyPatch(typeof(ConsoleWidget), "SetEnabled", new[] { typeof(bool) })]
internal static class ConsoleWidget_SetEnabled_LocalizeReadyMessage_Patch
{
    [HarmonyPostfix]
    private static void Postfix(ConsoleWidget __instance, bool enabled)
    {
        if (enabled)
        {
            ConsoleUiLocalizer.ApplyReadyMessage(__instance);
        }
    }
}

internal static class ConsoleUiLocalizer
{
    private static Text readyMessageText;

    internal static void ApplyReadyMessage(ConsoleWidget console)
    {
        if (console == null || console.content == null)
        {
            return;
        }

        Text text = FindReadyMessageText(console);
        CheatConsoleExtended mod = Main.CheatConsoleExtended;
        if (text == null || mod == null)
        {
            return;
        }

        text.text = ConsoleUiText.GetReadyMessage(mod.LocalizationHandler.Localize);
    }

    private static Text FindReadyMessageText(ConsoleWidget console)
    {
        if (readyMessageText != null)
        {
            return readyMessageText;
        }

        Text[] texts = console.content.GetComponentsInChildren<Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] != null && texts[i].text == ConsoleUiText.ReadyMessageEnglish)
            {
                readyMessageText = texts[i];
                return readyMessageText;
            }
        }

        return null;
    }
}

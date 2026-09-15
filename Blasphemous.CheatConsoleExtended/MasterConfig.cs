using System;

namespace Blasphemous.CheatConsoleExtended;

internal class MasterConfig
{
    public const string DefaultTopLevelCommandColor = "#FFFF00";
    public const string DefaultSubcommandColor = "#00FFFF";
    public const string DefaultParameterColor = "#0000FF";
    public const string DefaultOutputColor = "#FFFFFF";

    public object TopLevelCommandColor { get; set; }
    public object SubcommandColor { get; set; }
    public object ParameterColor { get; set; }
    public object OutputColor { get; set; }

    public MasterConfig()
    {
        TopLevelCommandColor = DefaultTopLevelCommandColor;
        SubcommandColor = DefaultSubcommandColor;
        ParameterColor = DefaultParameterColor;
        OutputColor = DefaultOutputColor;
    }

    public static MasterConfig CreateDefault()
    {
        return new MasterConfig
        {
            TopLevelCommandColor = DefaultTopLevelCommandColor,
            SubcommandColor = DefaultSubcommandColor,
            ParameterColor = DefaultParameterColor,
            OutputColor = DefaultOutputColor
        };
    }

    public void Normalize()
    {
        TopLevelCommandColor = NormalizeColor(TopLevelCommandColor, DefaultTopLevelCommandColor);
        SubcommandColor = NormalizeColor(SubcommandColor, DefaultSubcommandColor);
        ParameterColor = NormalizeColor(ParameterColor, DefaultParameterColor);
        OutputColor = NormalizeColor(OutputColor, DefaultOutputColor);
    }

    private static string NormalizeColor(object value, string fallback)
    {
        string text = value as string;
        if (text == null || text.Length != 7 || text[0] != '#')
        {
            return fallback;
        }

        for (int i = 1; i < text.Length; i++)
        {
            if (!Uri.IsHexDigit(text[i]))
            {
                return fallback;
            }
        }

        return text.ToUpperInvariant();
    }
}

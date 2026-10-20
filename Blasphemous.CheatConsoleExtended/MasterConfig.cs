using System;

namespace Blasphemous.CheatConsoleExtended;

internal sealed class MasterConfig
{
    public int CommandSuggestionDistance { get; set; } = 2;

    internal const string DefaultConsoleFont = "Consolas";

    public string ConsoleFont { get; set; } = DefaultConsoleFont;

    public const string DefaultTopLevelCommandColor = "#FFFF55";
    public const string DefaultSubcommandColor = "#00FFFF";
    public const string DefaultParameterColor = "#0088FF";
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
        CommandSuggestionDistance = StrictCommandResolver.NormalizeDistance(CommandSuggestionDistance);
        TopLevelCommandColor = NormalizeColor(TopLevelCommandColor, DefaultTopLevelCommandColor);
        SubcommandColor = NormalizeColor(SubcommandColor, DefaultSubcommandColor);
        ParameterColor = NormalizeColor(ParameterColor, DefaultParameterColor);
        OutputColor = NormalizeColor(OutputColor, DefaultOutputColor);
    }

    private static string NormalizeColor(object value, string fallback)
    {
        if (value is not string text || text.Length != 7 || text[0] != '#')
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
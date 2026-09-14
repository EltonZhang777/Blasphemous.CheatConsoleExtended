using Blasphemous.ModdingAPI;

namespace Blasphemous.CheatConsoleExtended;

public class CheatConsoleExtended : BlasMod
{
    internal MasterConfig Config { get; private set; }

    internal ConsoleFontManager ConsoleFont { get; private set; }

    internal CheatConsoleExtended() : base(ModInfo.MOD_ID, ModInfo.MOD_NAME, ModInfo.MOD_AUTHOR, ModInfo.MOD_VERSION) { }

    protected override void OnInitialize()
    {
        Config = ConfigHandler.Load<MasterConfig>() ?? new MasterConfig();
        ConsoleFont = new ConsoleFontManager(Config);
    }

    protected override void OnAllInitialized()
    {
        ConfigHandler.Save(Config);
    }
}

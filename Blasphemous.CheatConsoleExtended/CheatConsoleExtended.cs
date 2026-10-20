using Blasphemous.ModdingAPI;
using System;

using Blasphemous.NewbieEltonLibs.CheatConsole;

namespace Blasphemous.CheatConsoleExtended;

public class CheatConsoleExtended : BlasMod
{
    internal MasterConfig Config { get; private set; }
    private bool skipConfigSave;

    internal ConsoleFontManager ConsoleFont { get; private set; }

    internal CheatConsoleExtended() : base(ModInfo.MOD_ID, ModInfo.MOD_NAME, ModInfo.MOD_AUTHOR, ModInfo.MOD_VERSION) { }

    protected override void OnInitialize()
    {
        try
        {
            Config = ConfigHandler.Load<MasterConfig>() ?? throw new InvalidOperationException("Configuration is null.");
            Config.Normalize();
        }
        catch (Exception exception)
        {
            Config = MasterConfig.CreateDefault();
            skipConfigSave = true;
            ModLog.Warn("Failed to load configuration; using defaults. " + exception.Message, this);
        }
        ConsoleFont = new ConsoleFontManager(Config);

#if DEBUG
        CheatConsoleLogging.LogCheatConsoleInput(true, LogLevel.Info, true);
        CheatConsoleLogging.LogCheatConsoleOutput(true, LogLevel.Info, true);
#endif
    }

    protected override void OnRegisterServices(ModServiceProvider provider)
    {
        Blasphemous.CheatConsole.CommandRegister.RegisterCommand(provider, new ConsoleFontCommand());
    }

    internal void SaveConfig()
    {
        if (!skipConfigSave)
        {
            ConfigHandler.Save(Config);
        }
    }

    protected override void OnAllInitialized()
    {
        SaveConfig();
    }
}

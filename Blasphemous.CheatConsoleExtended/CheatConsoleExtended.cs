using System;
using Blasphemous.ModdingAPI;

namespace Blasphemous.CheatConsoleExtended;

public class CheatConsoleExtended : BlasMod
{
    internal MasterConfig Config { get; private set; }
    private bool skipConfigSave;

    internal CheatConsoleExtended() : base(ModInfo.MOD_ID, ModInfo.MOD_NAME, ModInfo.MOD_AUTHOR, ModInfo.MOD_VERSION) { }

    protected override void OnInitialize()
    {
        try
        {
            Config = ConfigHandler.Load<MasterConfig>();
            if (Config == null)
            {
                throw new InvalidOperationException("Configuration is null.");
            }

            Config.Normalize();
        }
        catch (Exception exception)
        {
            Config = MasterConfig.CreateDefault();
            skipConfigSave = true;
            ModLog.Warn("Failed to load configuration; using defaults. " + exception.Message, this);
        }
    }

    protected override void OnAllInitialized()
    {
        if (!skipConfigSave)
        {
            ConfigHandler.Save(Config);
        }
    }
}

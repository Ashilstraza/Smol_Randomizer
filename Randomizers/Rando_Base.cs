using System;

using BepInEx.Configuration;

namespace Smol_Randomizer.Randomizers;

internal abstract class Rando_Base
{
    /// <summary>
    /// If the core of the randomizer is enabled
    /// </summary>
    private protected bool coreEnableRandomization;
    public virtual string RandomizerName { get; private protected set; }

    /// <summary>
    /// Initialize the randomizer
    /// </summary>
    private protected virtual void InitRandomizer()
    {
        coreEnableRandomization = Settings.Settings.EnableRandomizer;
        Register();
        InitSettings();
        Settings.Settings.enableRandomizer.SettingChanged += RandoCoreSetting;
    }
    /// <summary>
    /// Registers the various events we want to watch for
    /// </summary>
    private protected abstract void Register();

    /// <summary>
    /// Unregisters the various events we want to watch for
    /// </summary>
    private protected abstract void Unregister();

    /// <summary>
    /// Add the randomizer's settings into the core
    /// </summary>
    private protected abstract void InitSettings();

    /// <summary>
    /// Event hook for when the core enable setting for the randomizer is changed
    /// </summary>
    /// <param name="sender">?</param>
    /// <param name="args">The setting that was changed</param>
    private protected void RandoCoreSetting(object sender, EventArgs args)
    {
        coreEnableRandomization = (bool)((SettingChangedEventArgs)args).ChangedSetting.BoxedValue;

        if (coreEnableRandomization)
        {
            Register();
        }
        else
        {
            Unregister();
        }
    }
}

using System;
using System.Collections.Generic;

using BepInEx.Configuration;

namespace Smol_Randomizer.Randomizers;

internal abstract class Rando_Base
{
    /// <summary>
    /// If the core of the randomizer is enabled
    /// </summary>
    private protected bool coreEnableRandomization;
    /// <summary>
    /// Name of the randomizer
    /// </summary>
    public virtual string RandomizerName { get; private protected set; }
    /// <summary>
    /// Description of the randomizer
    /// </summary>
    public virtual string RandomizerDescription { get; private protected set; }

    /// <summary>
    /// Initialize the randomizer
    /// </summary>
    private protected virtual void InitRandomizer()
    {
        coreEnableRandomization = Settings.Settings.EnableRandomizer;
        Register();
        AddRandoDescription();
        InitSettings();
        Settings.Settings.enableRandomizer.SettingChanged += RandoCoreSetting;
        Settings.Settings.OnSettingsLoaded += OnSettingsLoaded;
        Settings.SettingMenu.OnResetClicked += OnResetClicked;
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
    /// Event hook for when settings are loaded
    /// </summary>
    /// <param name="hasSaveData">If the setting that were loaded had data or not</param>
    private protected void OnSettingsLoaded(bool hasSaveData)
    {
        ResetAllLists();

        Dictionary<string, object> savedData = Settings.Settings.GetSavedData(RandomizerName);

        if (hasSaveData)
        {
            ApplySaveData(savedData);
        }

        SetSaveData(savedData);
    }

    /// <summary>
    /// Clears this Randomizer's lists if the given name matches the RandomizerName
    /// </summary>
    /// <param name="randoName">The randomizer being cleared</param>
    private protected void OnResetClicked(string randoName)
    {
        if (randoName == RandomizerName)
            ResetAllLists();
    }

    /// <summary>
    /// Apply the given per-slot data to the Randomizer's various saved settings
    /// </summary>
    /// <param name="savedData"></param>
    private protected abstract void ApplySaveData(Dictionary<string, object> savedData);

    /// <summary>
    /// Apply references to the various saved Randomizer's per-slot settings to the saveData
    /// </summary>
    /// <param name="savedData"></param>
    private protected abstract void SetSaveData(Dictionary<string, object> savedData);

    /// <summary>
    /// Add the randomizer's settings into the core
    /// </summary>
    private protected abstract void InitSettings();

    /// <summary>
    /// Reset all tracked lists
    /// </summary>
    private protected abstract void ResetAllLists();

    /// <summary>
    /// Add the randomizer's description into the core
    /// </summary>
    private protected void AddRandoDescription()
    {
        Cute_Rando_Core.AddRandoDescription(RandomizerName, RandomizerDescription);
    }

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

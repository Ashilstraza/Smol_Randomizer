using System;
#if TESTING
using System.Collections.Generic;
#endif

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
        Settings.RandoPerSaveData.OnSettingsLoaded += OnSettingsLoaded;
        Settings.SettingMenu.OnResetClicked += OnResetClicked;
#if TESTING // Enable Saving Data
        Settings.RandoPerSaveData.OnSettingsSaved += OnSettingsSaved;
#endif
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

#if TESTING // Enable Saving Data
        Dictionary<string, object> savedData = Settings.Settings.SaveData.GetSavedData(RandomizerName);

        if (hasSaveData)
        {
            ApplySaveData(savedData);
        }

        SetSaveData(savedData);
#endif
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

#if TESTING // Enable Saving Data
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
    /// Event hook for when settings are saved
    /// </summary>
    private protected abstract void OnSettingsSaved();
#endif

    /// <summary>
    /// Add the randomizer's settings into the core
    /// </summary>
    private protected abstract void InitSettings();

    /// <summary>
    /// Event Hook for when registered settings are updated
    /// </summary>
    /// <param name="sender">?</param>
    /// <param name="args">The setting that was changed</param>
    private protected abstract void OnSettingsUpdated(object sender, EventArgs args);

    /// <summary>
    /// Reset all tracked lists
    /// </summary>
    private protected abstract void ResetAllLists();

    /// <summary>
    /// Add the randomizer's description into the core
    /// </summary>
    private protected void AddRandoDescription()
    {
        CuteRandoCore.AddRandoDescription(RandomizerName, RandomizerDescription);
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

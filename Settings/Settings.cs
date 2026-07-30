using System;
#if TESTING
using System.Collections.Generic;
#endif
using BepInEx.Configuration;

using Newtonsoft.Json;

using UnityEngine;

namespace Smol_Randomizer.Settings;

/// <summary>
/// Handles the various settings <see href="https://github.com/BepInEx/BepInEx.ConfigurationManager/blob/master/README.md"/>
/// </summary>
public static class Settings
{
    #region Settings
#if TESTING
    /// <summary>
    /// If we want to test new things
    /// </summary>
    public static bool TestNewThings
    {
        get => testNewThings.Value;
        internal set => testNewThings.Value = value;
    }
    private static ConfigEntry<bool> testNewThings;
    /// <summary>
    /// Default for if we want to test new things
    /// </summary>
    public const bool defaultTestNewThings = false;
    /// <summary>
#endif
    /// Enables the randomization of the various things
    /// </summary>
    public static bool EnableRandomizer
    {
        get => enableRandomizer.Value;
        internal set => enableRandomizer.Value = value;
    }
    internal static ConfigEntry<bool> enableRandomizer;
    /// <summary>
    /// Default if we want to randomize the various things
    /// </summary>
    public const bool defaultEnableRandomizer = true;
    /// <summary>
    /// Changes the menu button colors of the enabled/disabled randomizers
    /// </summary>
    public static RandomizerColors EnabledRandomizerColors
    {
        get => enabledRandomizerColors.Value;
        internal set => enabledRandomizerColors.Value = value;
    }
    private static ConfigEntry<RandomizerColors> enabledRandomizerColors;
    /// <summary>
    /// Default color of the randomizer menu buttons
    /// </summary>
    public const RandomizerColors defaultEnabledRandomizerColors = RandomizerColors.GreenRed;

    /// <summary>
    /// Reference to the randomizer's config file to allow adding settings.
    /// </summary>
    public static ConfigFile ConfigFile { get; private set; }

    /// <summary>
    /// Initialize the various settings.
    /// </summary>
    /// <remarks> Order is backwards for some reason?</remarks>
    /// <param name="config"></param>
    public static void Init(ConfigFile config)
    {
        ConfigFile = config;
        TomlTypeConverter.AddConverter(typeof(FloatRange), FloatRangeConverter);
        TomlTypeConverter.AddConverter(typeof(IntRange), IntRangeConverter);

        enableRandomizer = config.Bind(
            "Main Settings",
            "Enable Randomizer",
            defaultEnableRandomizer,
            new ConfigDescription(
                "Enable Randomization.",
                null,
                new ConfigurationManagerAttributes
                {
                    Order = 1
                }));

        enabledRandomizerColors = config.Bind(
            "Main Settings",
            "Enabled/Disabled Colors",
            defaultEnabledRandomizerColors,
            new ConfigDescription(
                "Changes the colors of the enabled/disabled randomizers.",
                null,
                new ConfigurationManagerAttributes
                {
                    Order = 3
                }));
#if TESTING
        testNewThings = config.Bind(
            "Testing",
            "Test new things",
            defaultTestNewThings,
            new ConfigDescription(
                "Test new things"));
#endif
        enableRandomizer.SettingChanged += CuteRandoCore.UpdateSettings;
        enabledRandomizerColors.SettingChanged += SettingMenu.OnEnabledRandomizerColorChanged;
        SettingMenu.ChangeColors(EnabledRandomizerColors);
    }
    #endregion

    #region BepInEx_Setting_Stuff
    /// <summary>
    /// Max Slider Percentage
    /// </summary>
    public const int maxSliderPercent = 300;

    /// <summary>
    /// Max Slider Value
    /// </summary>
    public const int maxSliderValue = 100;

    /// <summary>
    /// Custom Drawer for entering Ranges
    /// </summary>
    /// <param name="entry">The entry to draw.</param>
    public static void RangeDrawer(ConfigEntryBase entry)
    {
        Type entryType = entry.SettingType;
        if (entryType == typeof(FloatRange))
        {
            FloatRange value = (FloatRange)entry.BoxedValue;
            string min = (value.Min * 100).ToString();
            string max = (value.Max * 100).ToString();
            TextRange(ref min, ref max, RangeType.Percent, entry.Description.AcceptableValues);
            try
            {
                entry.BoxedValue = new FloatRange(float.Parse(min) / 100, float.Parse(max) / 100);
            }
            catch (ArgumentException e)
            {
                Console.Error.WriteLine(e.Message);
            }
        }
        else if (entryType == typeof(IntRange))
        {
            IntRange value = (IntRange)entry.BoxedValue;
            string min = value.Min.ToString();
            string max = value.Max.ToString();
            TextRange(ref min, ref max, RangeType.Value, entry.Description.AcceptableValues);
            try
            {
                entry.BoxedValue = new IntRange((int)Math.Round(float.Parse(min)), (int)Math.Round(float.Parse(max)));
            }
            catch (ArgumentException e)
            {
                Console.Error.WriteLine(e.Message);
            }
        }
        else
        {
            Console.Error.WriteLine("Unimplimented entryType");
        }
    }

    /// <summary>
    /// Range UI Control
    /// </summary>
    /// <param name="min">Minimum Value.</param>
    /// <param name="max">Maximum Value.</param>
    /// <param name="rangeType">Type of range the values are.</param>
    private static void TextRange(ref string min, ref string max, RangeType rangeType, AcceptableValueBase? acceptableRange = null)
    {
        int minVal = 0;
        int maxVal = rangeType.Equals(RangeType.Percent) ? maxSliderPercent : maxSliderValue;

        if (acceptableRange is AcceptableRangeforIntRange iRange)
        {
            minVal = iRange.MinValue;
            maxVal = iRange.MaxValue;
        }
        else if (acceptableRange is AcceptableRangeforFloatRange fRange)
        {
            minVal = (int)(fRange.MinValue * 100);
            maxVal = (int)(fRange.MaxValue * 100);
        }

        using GUILayout.VerticalScope verticalGroup = new("box");
        using (GUILayout.HorizontalScope horizontalGroup = new("box"))
        {
            GUILayout.Label($"Minimum {(rangeType.Equals(RangeType.Percent) ? "Percent" : "Value")}");
            min = GUILayout.TextField(min, GUILayout.Width(30));
            min = GUILayout.HorizontalSlider((float)Math.Round(float.Parse(min)), minVal, (float)Math.Round(float.Parse(max)), GUILayout.Width(100)).ToString();

        }

        using (GUILayout.HorizontalScope horizontalGroup = new("box"))
        {
            GUILayout.Label($"Maximum {(rangeType.Equals(RangeType.Percent) ? "Percent" : "Value")}");
            max = GUILayout.TextField(max, GUILayout.Width(30));
            max = GUILayout.HorizontalSlider((float)Math.Round(float.Parse(max)), (float)Math.Round(float.Parse(min)), maxVal, GUILayout.Width(100)).ToString();
        }
    }

    /// <summary>
    /// Converter for BepInEx to convert the FloatRange into something savable then back again
    /// </summary>
    private static readonly TypeConverter FloatRangeConverter = new()
    {
        ConvertToString = (obj, type) => obj.ToString(),
        ConvertToObject = (str, type) => FloatRange.Parse(str)
    };

    /// <summary>
    /// Converter for BepInEx to convert the IntRange into something savable then back again
    /// </summary>
    private static readonly TypeConverter IntRangeConverter = new()
    {
        ConvertToString = (obj, type) => obj.ToString(),
        ConvertToObject = (str, type) => IntRange.Parse(str)
    };

    /// <summary>
    /// Randomizer Range Types
    /// </summary>
    public enum RangeType
    {
        Percent,
        Value
    }
    #endregion

    #region Silksong.DataManager_Stuff
    /// <summary>
    /// Contains references to all the various data that we want to save per-save slot
    /// </summary>
    public static RandoPerSaveData SaveData
    {
        get
        {
            if (saveData == null)
            {
                saveData ??= new();
                RandoPerSaveData.Saving();
            }

            return saveData;
        }
        set
        {
            saveData = value;
            saveData.Load();
        }
    }
    private static RandoPerSaveData saveData;

    public static bool Loaded
    {
        get
        {
            if (loading)
            {
                if (GameManager.instance.sm.sceneType == GlobalEnums.SceneType.GAMEPLAY)
                    loaded = true;
                else loaded = false;
                loading = false;
            }
            return loaded;
        }
    }

    private static bool loaded = false;

    internal static bool loading = false;
    #endregion
}

/// <summary>
/// Used for referencing save data
/// </summary>
public class RandoPerSaveData
{
#if TESTING // Enable Saving Data
    /// <summary>
    /// Dictionary containing references to all the data we want to save per-save slot
    /// </summary>
    [JsonProperty]
    public Dictionary<string, Dictionary<string, object>> SmolSaveDictionary
    // <Randomizer, <Randomizer Dictionary Name, Saved Object>>
    {
        get;
        internal set;
    }
#endif

    /// <summary>
    /// The seed for the save
    /// </summary>
    [JsonIgnore]
    public int SaveSeed
    {
        get
        {
            if (saveSeed == int.MinValue) saveSeed = CuteRandoCore.GetNewSaveSeed();
            return saveSeed;
        }
    }

    [JsonProperty]
    private int saveSeed = int.MinValue;

    /// <summary>
    /// Called on the data being loaded
    /// </summary>
    internal static event Action<bool> OnSettingsLoaded;
    /// <summary>
    /// Called on the data being saved
    /// </summary>
    internal static event Action OnSettingsSaved;

    /// <summary>
    /// Loads the saved data into the various randomizers that are listening for the load.
    /// </summary>
    internal void Load()
    {
        Settings.loading = true;
#if TESTING // Enable Saving Data
        try
        {
            OnSettingsLoaded?.Invoke(!(SmolSaveDictionary == null));
        }
        catch (Exception ex)
        {
            Console.Error.WriteLineAsync($"[{CuteRandoCore.MODNAME}] Exception encountered when invoking OnSettingsLoaded()\n" + ex.Message);
        }
#endif
    }

    /// <summary>
    /// Called when saving is occuring
    /// </summary>
    internal static void Saving()
    {
        OnSettingsSaved?.Invoke();
    }

#if TESTING // Enable Saving Data
    /// <summary>
    /// Returns a dictionary containing all the saved data for the given randomizer. If it does not exists, just returns an empty dictionary.
    /// </summary>
    /// <param name="randomizer">The randomizer we are requesting</param>
    /// <returns>The saved data for the given randomizer</returns>
    internal Dictionary<string, object> GetSavedData(string randomizer)
    {
        SmolSaveDictionary ??= [];
        if (!SmolSaveDictionary.TryGetValue(randomizer, out Dictionary<string, object> dictionary))
        {
            dictionary = [];
            SmolSaveDictionary[randomizer] = dictionary;
        }

        return dictionary;
    }

    /// <summary>
    /// Sets the saved data of the given randomizer
    /// </summary>
    /// <param name="randomizer">The randomizer to set the data of</param>
    /// <param name="dictionary">Dictionary containing the data to set</param>
    internal void SetSavedData(string randomizer, Dictionary<string, object> dictionary)
    {
        SmolSaveDictionary ??= [];
        SmolSaveDictionary[randomizer] = dictionary;
    }
#endif

    /// <summary>
    /// Rerolls the seed.
    /// </summary>
    internal void RerollSeed()
    {
        saveSeed = CuteRandoCore.GetNewSaveSeed();
    }

    /// <summary>
    /// Sets the seed to the given value.
    /// </summary>
    /// <param name="seed">The new seed</param>
    internal void SetSeed(int seed)
    {
        saveSeed = seed;
    }
}

/// <summary>
/// Range Randomize Types
/// </summary>
public enum RandomizeByRangeTypes
{
    Disabled,
    Percent,
    Value
}

/// <summary>
/// Flat Amount Randomize Types
/// </summary>
internal enum RandomizeByFlatAmount
{
    Disabled,
    Shift,
    Range
}

/// <summary>
/// Randomizer Consistency Types; None, EnemyType, and Scene
/// </summary>
public enum RandomizerConsistencyA
{
    None,
    Scene,
    EnemyType
}

/// <summary>
/// Randomizer Consistency Types; None, Scene, PerSave
/// </summary>
public enum RandomizerConsistencyB
{
    None,
    Scene,
    PerSave
}

/// <summary>
/// Randomizer Consistency Types; OnDamageTaken, OnSceneTransition, Never
/// </summary>
public enum RandomizerConsistencyC
{
    OnDamageTaken,
    OnSceneTransition,
    PerSaveFile
}

/// <summary>
/// Enemy Type Flags
/// </summary>
[Flags]
public enum RandomizerEnemyTypeFlags
{
    None,
    Enemy,
    Boss,
    Both
}

/// <summary>
/// Colors for Enabled/Disabled Randomizers
/// </summary>
public enum RandomizerColors
{
    GreenRed,
    BlueYellow,
    PurpleOrange
}

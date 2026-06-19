using System;
using System.Collections.Generic;

using BepInEx.Configuration;

using Smol_Randomizer.Settings;

using HarmonyLib;

using UnityEngine.SceneManagement;

namespace Smol_Randomizer.Randomizers;

internal class World_Currency_Drop_Rando : Rando_Base
{
    /// <summary>
    /// We make a singleton of this rando
    /// </summary>
    private static readonly Lazy<World_Currency_Drop_Rando> instance = new(() => new World_Currency_Drop_Rando());
    /// <summary>
    /// Externally visible instance of this rando
    /// </summary>
    public static World_Currency_Drop_Rando Instance => instance.Value;

    /// <summary>
    /// Dictionary of multipliers per room
    /// </summary>
    private readonly Dictionary<string, float> sceneMultiplier = [];
    /// <summary>
    /// Dictionary of Architect Crest multipliers per room
    /// </summary>
    private readonly Dictionary<string, float> sceneACMultiplier = [];
    /// <summary>
    /// Shard multiplier when enemy consistancy is enabled (all regions drop same amount)
    /// </summary>
    private float consistantMultiplier;
    /// <summary>
    /// Architect multiplier when enemy consistancy is enabled (all regions drop same amount)
    /// </summary>
    private float consistantACMultiplier;
    /// <summary>
    /// The current scene we are in
    /// </summary>
    private string currentScene = "";

    #region Randomizer_Info
    /// <summary>
    /// Used for registering this randomizer in the core for first frame to update currency regions
    /// </summary>
    private Randomizer_Info eventActiveLimitRegion;
    /// <summary>
    /// Used for registering this randomizer in the core for when the scene loads
    /// </summary>
    private Randomizer_Info eventOnSceneLoad;
    #endregion

    /// <summary>
    /// Constructor for this singleton
    /// </summary>
    private World_Currency_Drop_Rando() => InitRandomizer();

    private protected override void InitRandomizer()
    {
        RandomizerName = "World Currency Drop Randomizer";

        eventActiveLimitRegion = new(
            RandomizerName,
            RandomizerEventType.ActiveLimitRegion,
            AccessTools.Method(
                typeof(World_Currency_Drop_Rando),
                nameof(SetCurrency)),
            this);

        eventOnSceneLoad = new(
            RandomizerName,
            RandomizerEventType.OnSceneLoad,
            AccessTools.Method(
                typeof(World_Currency_Drop_Rando),
                nameof(OnSceneLoad)),
            this);

        base.InitRandomizer();

        if (ConsistencySetting == RandomizerConsistency3.Scene)
        {
            shardChanceChanging = true;
            architectCrestChanging = true;
            UpdateConsistantMultipliers();
        }
    }

    private protected override void Register()
    {
        Cute_Rando_Core.RegisterRandomizer(eventActiveLimitRegion);
        Cute_Rando_Core.RegisterRandomizer(eventOnSceneLoad);
    }

    private protected override void Unregister()
    {
        Cute_Rando_Core.UnregisterRandomizer(eventActiveLimitRegion);
        Cute_Rando_Core.UnregisterRandomizer(eventOnSceneLoad);
    }

    /// <summary>
    /// On Scene Load, save current loading scene
    /// </summary>
    /// <param name="scene">The new scene that is loading</param>
    /// <param name="mode">?</param>
    private void OnSceneLoad(Scene scene, LoadSceneMode _) => currentScene = scene.name;

    /// <summary>
    /// Updates world currency drops with new currency values
    /// </summary>
    /// <param name="region">The region to adjust</param>
    /// <exception cref="NotImplementedException">Thrown if there is an unimplemented randomizer type.</exception>
    private void SetCurrency(ICurrencyLimitRegion region)
    {
        if (!coreEnableRandomization || region == null || (!ArchitectChanceEnable && !ShardChanceEnable)) 
            return;

        float multiplier;
        float multiplierAC;

        switch (ConsistencySetting)
        {
            case RandomizerConsistency3.Save:
                multiplier = consistantMultiplier;
                multiplierAC = consistantACMultiplier;
                break;
            case RandomizerConsistency3.Scene:
                if (!sceneMultiplier.TryGetValue(currentScene, out multiplier))
                {
                    multiplier = Cute_Rando_Core.TupleRandoHelper(ShardChanceMultiplier.AsTuple());
                    sceneMultiplier[currentScene] = multiplier;
                }

                if (!sceneACMultiplier.TryGetValue(currentScene, out multiplierAC))
                {
                    multiplierAC = Cute_Rando_Core.TupleRandoHelper(ArchitectCrestMultiplier.AsTuple());
                    sceneACMultiplier[currentScene] = multiplierAC;
                }

                break;
            case RandomizerConsistency3.None:
                multiplier = Cute_Rando_Core.TupleRandoHelper(ShardChanceMultiplier.AsTuple());
                multiplierAC = Cute_Rando_Core.TupleRandoHelper(ArchitectCrestMultiplier.AsTuple());
                break;
            default:
                throw new NotImplementedException();
        }

        Array dropChances = (Array)Cute_Rando_Core.TraverseHelper(region, "dropChances").GetValue();
        Traverse architectProbabilities = Cute_Rando_Core.TraverseHelper(region, "architectProbabilities");
        float[] newArchitectProbabilities = new float[dropChances.Length];

        int i = 0;

        foreach (object o in dropChances)
        {
            float num = (float)Cute_Rando_Core.TraverseHelper(o, "Probability").GetValue();

            if ((int)Cute_Rando_Core.TraverseHelper(o, "dropAmount").GetValue() > 0)
            {
                if (ShardChanceEnable)
                {
                    num *= multiplier;
                    Cute_Rando_Core.TraverseHelper(o, "Probability").SetValue(num);
                }

                if (ArchitectChanceEnable)
                    newArchitectProbabilities[i] = num * multiplierAC;
            }
            else if (ArchitectChanceEnable)
                newArchitectProbabilities[i] = num;

            i++;
        }

        if (ArchitectChanceEnable)
            architectProbabilities.SetValue(newArchitectProbabilities);
    }

    #region Settings
    /// <summary>
    /// Setting for how consistant the drop chances are
    /// </summary>
    public RandomizerConsistency3 ConsistencySetting
    {
        get => consistencySetting.Value;
        internal set => consistencySetting.Value = value;
    }
    private ConfigEntry<RandomizerConsistency3> consistencySetting;
    /// <summary>
    /// Default setting for how consistant the drop chances are
    /// </summary>
    public readonly RandomizerConsistency3 defaultConsistencySetting = RandomizerConsistency3.None;
    /// <summary>
    /// Randomize shard drop chance from hitting specific walls
    /// </summary>
    public bool ShardChanceEnable
    {
        get => shardChanceEnable.Value;
        internal set => shardChanceEnable.Value = value;
    }
    private ConfigEntry<bool> shardChanceEnable;
    /// <summary>
    /// Default choice for wall shard drop chance randomizer
    /// </summary>
    public readonly bool defaultShardChanceEnable = false;
    /// <summary>
    /// Percent range for regular shard drop chance multiplier
    /// </summary>
    public FloatRange ShardChanceMultiplier
    {
        get => shardChanceMultiplier.Value;
        internal set => shardChanceMultiplier.Value = value;
    }
    private ConfigEntry<FloatRange> shardChanceMultiplier;
    /// <summary>
    /// Default percent range for regular shard drop chance multiplier
    /// </summary>
    public readonly FloatRange defaultShardChanceMultiplier = new(1f, 3f);
    /// <summary>
    /// Randomize architect crest shard drop chance
    /// </summary>
    public bool ArchitectChanceEnable
    {
        get => architectChanceEnable.Value;
        internal set => architectChanceEnable.Value = value;
    }
    private ConfigEntry<bool> architectChanceEnable;
    /// <summary>
    /// Default choice for architect crest shard drop chance randomizer
    /// </summary>
    public readonly bool defaultArchitectChanceEnable = false;
    /// <summary>
    /// Percent range for architect crest multiplier
    /// </summary>
    public FloatRange ArchitectCrestMultiplier
    {
        get => architectCrestMultiplier.Value;
        internal set => architectCrestMultiplier.Value = value;
    }
    private ConfigEntry<FloatRange> architectCrestMultiplier;
    /// <summary>
    /// Default percent range for architect crest multiplier
    /// </summary>
    public readonly FloatRange defaultArchitectCrestMultiplier = new(1f, 3f);

    // Used for determining if we need to update and clear the dictionaries
    private FloatRange currentArchitectCrestSetting;
    private bool architectCrestChanging = false;
    private FloatRange currentShardChanceSetting;
    private bool shardChanceChanging = false;

    private protected override void InitSettings()
    {
        ConfigFile config = Settings.Settings.ConfigFile;

        consistencySetting = config.Bind(
            section: RandomizerName,
            key: "Drop Chance Consistancy",
            defaultValue: defaultConsistencySetting,
            configDescription: new ConfigDescription(
                description: "Sets how consistant the chance for world drops are.",
                tags: new ConfigurationManagerAttributes
                {
                    Order = 4
                }));
        shardChanceEnable = config.Bind(
            section: RandomizerName,
            key: "Randomize Wall Shard Drop Chance",
            defaultValue: defaultShardChanceEnable,
            configDescription: new ConfigDescription(
                description: "Enable wall shard drop chance multiplier.",
                tags: new ConfigurationManagerAttributes
                {
                    Order = 3
                }));
        shardChanceMultiplier = config.Bind(
            section: RandomizerName,
            key: "Drop Chance Multiplier",
            defaultValue: defaultShardChanceMultiplier,
            configDescription: new ConfigDescription(
                description: "Wall shard drop chance multiplier.",
                tags: new ConfigurationManagerAttributes
                {
                    Order = 2,
                    CustomDrawer = Settings.Settings.RangeDrawer
                }));
        architectChanceEnable = config.Bind(
            section: RandomizerName,
            key: "Randomize Architect Shard Drop Chance",
            defaultValue: defaultArchitectChanceEnable,
            configDescription: new ConfigDescription(
                description: "Enable Architect Crest wall shard drop chance multiplier.",
                tags: new ConfigurationManagerAttributes
                {
                    Order = 1
                }));
        architectCrestMultiplier = config.Bind(
            section: RandomizerName,
            key: "Architect Crest Multiplier",
            defaultValue: defaultArchitectCrestMultiplier,
            configDescription: new ConfigDescription(
                description: "Architect Crest wall shard multiplier.",
                tags: new ConfigurationManagerAttributes
                {
                    Order = 0,
                    CustomDrawer = Settings.Settings.RangeDrawer
                }));

        currentArchitectCrestSetting = ArchitectCrestMultiplier;
        currentShardChanceSetting = ShardChanceMultiplier;

        consistencySetting.SettingChanged += OnRandoConsistancyUpdated;
        shardChanceMultiplier.SettingChanged += OnShardChanceMultiplierUpdated;
        architectCrestMultiplier.SettingChanged += OnArchitectCrestMultiplierUpdated;

        shardChanceEnable.SettingChanged += ChanceEnable;
        architectChanceEnable.SettingChanged += ChanceEnable;
    }

    /// <summary>
    /// Event hook for when the architect crest chance multiplier changes
    /// </summary>
    /// <param name="sender">?</param>
    /// <param name="args">The setting that was changed</param>
    private void OnArchitectCrestMultiplierUpdated(object sender, EventArgs args)
    {
        FloatRange architectCrestFloat = (FloatRange)((SettingChangedEventArgs)args).ChangedSetting.BoxedValue;

        if (!currentArchitectCrestSetting.Equals(architectCrestFloat))
        {
            architectCrestChanging = true;
            currentArchitectCrestSetting = architectCrestFloat;

            if (ConsistencySetting == RandomizerConsistency3.Save)
                UpdateConsistantMultipliers();
            else if (ConsistencySetting == RandomizerConsistency3.Scene)
                sceneACMultiplier.Clear();
        }
    }

    /// <summary>
    /// Event hook for when the shard chance multiplier changes
    /// </summary>
    /// <param name="sender">?</param>
    /// <param name="args">The setting that was changed</param>
    private void OnShardChanceMultiplierUpdated(object sender, EventArgs args)
    {
        FloatRange shardChanceFloat = (FloatRange)((SettingChangedEventArgs)args).ChangedSetting.BoxedValue;

        if (!currentShardChanceSetting.Equals(shardChanceFloat))
        {
            currentShardChanceSetting = shardChanceFloat;

            if (ConsistencySetting == RandomizerConsistency3.Save)
            {
                shardChanceChanging = true;
                UpdateConsistantMultipliers();
            }
            else if (ConsistencySetting == RandomizerConsistency3.Scene)
                sceneMultiplier.Clear();
        }
    }

    /// <summary>
    /// Event hook for when the randomizer consistancy setting is updated
    /// </summary>
    /// <param name="sender">?</param>
    /// <param name="args">The setting that was changed</param>
    private void OnRandoConsistancyUpdated(object sender, EventArgs args)
    {
        shardChanceChanging = true;
        architectCrestChanging = true;
        UpdateConsistantMultipliers();
        sceneMultiplier.Clear();
        sceneACMultiplier.Clear();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="args"></param>
    private void ChanceEnable(object sender, EventArgs args)
    {
        if (!shardChanceEnable.Value && !architectChanceEnable.Value) Unregister();
        if (shardChanceEnable.Value || architectChanceEnable.Value) Register();
    }

    /// <summary>
    /// Reroll the multipliers when called, must have changing bools set to true to change their respective multipliers
    /// </summary>
    private void UpdateConsistantMultipliers()
    {
        if (shardChanceChanging)
            consistantMultiplier = Cute_Rando_Core.TupleRandoHelper(ShardChanceMultiplier.AsTuple());

        if (architectCrestChanging)
            consistantACMultiplier = Cute_Rando_Core.TupleRandoHelper(ArchitectCrestMultiplier.AsTuple());

        architectCrestChanging = false;
        shardChanceChanging = false;
    }
    #endregion
}

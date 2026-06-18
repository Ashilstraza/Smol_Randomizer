using System;
using System.Collections.Generic;

using BepInEx.Configuration;

using Smol_Randomizer.Settings;

using GlobalSettings;

using HarmonyLib;

namespace Smol_Randomizer.Randomizers;

/// <summary>
/// Randomizer for Enemy Currency Drops
/// </summary>
internal class Enemy_Currency_Rando : Rando_Base
{
    /// <summary>
    /// We make a singleton of this rando
    /// </summary>
    private static readonly Lazy<Enemy_Currency_Rando> instance = new(() => new Enemy_Currency_Rando());
    /// <summary>
    /// Externally visible instance of this rando
    /// </summary>
    public static Enemy_Currency_Rando Instance => instance.Value;

    /// <summary>
    /// Set of HealthManagers that we have touched in this scene.
    /// </summary>
    private readonly HashSet<HealthManager> currentEnemyHealthManagers = [];
    /// <summary>
    /// Dictionary of randomized geo sets for a given enemy type
    /// </summary>
    private readonly Dictionary<string, RandomizedGeoSet> enemyGeoSets = [];
    /// <summary>
    /// Dictionary of randomized geo sets per room for a given enemy type
    /// </summary>
    private readonly Dictionary<string, Dictionary<string, RandomizedGeoSet>> sceneGeoSets = [];
    /// <summary>
    /// Dictionary of randomized shards for a given enemy type
    /// </summary>
    private readonly Dictionary<string, int> enemyShards = [];
    /// <summary>
    /// Dictionary of randomized shards per room for a given enemy type
    /// </summary>
    private readonly Dictionary<string, Dictionary<string, int>> sceneShards = [];

    /// <summary>
    /// Small Rosary Value
    /// </summary>
    internal static int smallGeoValue = 0; // 1
    /// <summary>
    /// Medium Rosary Value
    /// </summary>
    internal static int mediumGeoValue = 0; // 5
    /// <summary>
    /// Large Rosary Value
    /// </summary>
    internal static int largeGeoValue = 0; // 15

    #region Randomizer_Info
    /// <summary>
    /// Used for registering this randomizer in the core for when enemies activate
    /// </summary>
    private Randomizer_Info eventActiveEnemy;
    /// <summary>
    /// Used for registering this randomizer in the core for when a scene loads
    /// </summary>
    private Randomizer_Info eventOnFirstSceneFrame;
    #endregion

    /// <summary>
    /// Constructor for this singleton
    /// </summary>
    private Enemy_Currency_Rando() : base()
    {
        InitRandomizer();
    }

    private protected override void InitRandomizer()
    {
        RandomizerName = "Enemy Currency Randomizer";

        if (!Cute_Rando_Core.RegisterRandomizer(new(
            RandomizerName,
            RandomizerEventType.GameStartup,
            AccessTools.Method(
                typeof(Enemy_Currency_Rando),
                nameof(GameStartup)),
            this))) return;

        eventActiveEnemy = new(
            RandomizerName,
            RandomizerEventType.ActiveEnemy,
            AccessTools.Method(
                typeof(Enemy_Currency_Rando),
                nameof(SetCurrency)),
            this);
        eventOnFirstSceneFrame = new(
            RandomizerName,
            RandomizerEventType.OnFirstSceneFrame,
            AccessTools.Method(
                typeof(Enemy_Currency_Rando),
                nameof(OnFirstSceneFrame)),
            this);

        smallGeoValue = Gameplay.SmallGeoValue.Value;
        mediumGeoValue = Gameplay.MediumGeoValue.Value;
        largeGeoValue = Gameplay.LargeGeoValue.Value;

        base.InitRandomizer();
    }

    private protected override void Register()
    {
        Cute_Rando_Core.RegisterRandomizer(eventActiveEnemy);
        Cute_Rando_Core.RegisterRandomizer(eventOnFirstSceneFrame);
    }

    private protected override void Unregister()
    {
        Cute_Rando_Core.UnregisterRandomizer(eventActiveEnemy);
        Cute_Rando_Core.UnregisterRandomizer(eventOnFirstSceneFrame);
    }

    /// <summary>
    /// Patch HealthManager.OnEnable on game startup
    /// </summary>
    private void GameStartup()
    {
        Cute_Rando_Core.harmony.Patch(
            AccessTools.Method(typeof(HealthManager), "OnEnable"),
            postfix: new HarmonyMethod(typeof(Enemy_Currency_Rando), nameof(HealthManagerOnEnablePostfix)));
    }

    /// <summary>
    /// On First Frame, clean the active health manager list. This is done on the first frame since some health managers get added before the scene is loaded, and some afterwards.
    /// </summary>
    private void OnFirstSceneFrame() => CleanCurrentHealthManagerList();

    /// <summary>
    /// Patch that hooks the end of OnEnable of objects that have a HealthManager to adjust their currency
    /// </summary>
    /// <param name="__instance">The HealthManager that we want to adjust</param>
    /// <param name="___smallGeoDrops">Private field for smallGeoDrops</param>
    /// <param name="___mediumGeoDrops">Private field for mediumGeoDrops</param>
    /// <param name="___largeGeoDrops">Private field for largeGeoDrops</param>
    /// <param name="___shellShardDrops">Private field for shellShardDrops</param>
    private static void HealthManagerOnEnablePostfix(
        ref HealthManager __instance,
        ref int ___smallGeoDrops,
        ref int ___mediumGeoDrops,
        ref int ___largeGeoDrops,
        ref int ___shellShardDrops)
    {
        if (!Instance.coreEnableRandomization) return;

        if (Instance.currentEnemyHealthManagers.Add(__instance))
            Instance.SetCurrency(__instance,
                ref ___smallGeoDrops,
                ref ___mediumGeoDrops,
                ref ___largeGeoDrops,
                ref ___shellShardDrops);
    }

    /// <summary>
    /// Updates an enemy with new currency values
    /// </summary>
    /// <param name="thing">The HealthManager to adjust values in</param>
    /// <param name="smallGeoDrops">Small rosary drop quantitiy</param>
    /// <param name="mediumGeoDrops">Medium rosary drop quantitiy</param>
    /// <param name="largeGeoDrops">Large rosary drop quantitiy</param>
    /// <param name="shellShardDrops">Shell shard drop quantitiy</param>
    private void SetCurrency(
        HealthManager thing,
        ref int smallGeoDrops,
        ref int mediumGeoDrops,
        ref int largeGeoDrops,
        ref int shellShardDrops)
    {
        if (thing == null) return;

        if (RosaryRandomizerType != RandomizeByRangeTypes.Disabled)
        {
            RandomizeGeo(thing, out RandomizedGeoSet geoSet);

            smallGeoDrops = geoSet.SmallGeo;
            mediumGeoDrops = geoSet.MediumGeo;
            largeGeoDrops = geoSet.LargeGeo;
        }

        if (ShardRandomizerType != RandomizeByRangeTypes.Disabled)
            shellShardDrops = RandomizeShards(thing, ref shellShardDrops);
    }

    /// <summary>
    /// Randomizes shards based on consistency settings, followed by randomizer type
    /// </summary>
    /// <param name="thing">HealthManager of the enemy</param>
    /// <param name="shellShardDrops">Shell shard drop quantitiy</param>
    /// <returns>Nukber of shards for enemy to drop</returns>
    /// <exception cref="NotImplementedException">Thrown if there is an unimplemented randomizer type.</exception>
    private int RandomizeShards(HealthManager thing, ref int shellShardDrops)
    {
        string operatingScene = thing.gameObject.scene.name;

        int shards;
        string name = thing.name;

        int cullIndex = name.IndexOf('(') - 1;
        if (cullIndex > 0) name = name[..cullIndex];

        switch (RandomizerConsistency)
        {
            case RandomizerConsistency4.EnemyType:
                if (!enemyShards.TryGetValue(name, out shards))
                {
                    shards = GetRandoTypeShards(ref shellShardDrops);
                    enemyShards[name] = shards;
                }

                return shards;
            case RandomizerConsistency4.Scene:

                if (!sceneShards.TryGetValue(operatingScene, out Dictionary<string, int> shardSet))
                {
                    shards = GetRandoTypeShards(ref shellShardDrops);
                    sceneShards[operatingScene] = new() { { name, shards } };
                }
                else
                {
                    if (!shardSet.TryGetValue(name, out shards))
                    {
                        shards = GetRandoTypeShards(ref shellShardDrops);
                        shardSet[name] = shards;
                    }
                }

                return shards;
            case RandomizerConsistency4.None:
                return GetRandoTypeShards(ref shellShardDrops);
            default:
                throw new NotImplementedException();
        }

        // Helper to randomize shards
        int GetRandoTypeShards(ref int shellShardDrops)
        {
            return ShardRandomizerType == RandomizeByRangeTypes.Percent
                ? (int)Math.Round(shellShardDrops * Cute_Rando_Core.TupleRandoHelper(ShardPercentDropRange.AsTuple()))
                : ShardRandomizerType == RandomizeByRangeTypes.Value
                    ? Cute_Rando_Core.TupleRandoHelper(ShardValueDropRange.AsTuple())
                    : 0;
        }
    }

    /// <summary>
    /// Randomizes rosaries based on consistancy settings, followed by randomizer type
    /// </summary>
    /// <param name="thing">HealthManager of the enemy</param>
    /// <param name="geoSet">The geoSet of the enemy we are randomizing</param>
    /// <exception cref="NotImplementedException">Thrown if there is an unimplemented randomizer type.</exception>
    private void RandomizeGeo(HealthManager thing, out RandomizedGeoSet geoSet)
    {
        string name = thing.name;

        int cullIndex = name.IndexOf('(') - 1;
        if (cullIndex > 0) name = name[..cullIndex];

        switch (RandomizerConsistency)
        {
            case RandomizerConsistency4.EnemyType:
                if (!enemyGeoSets.TryGetValue(name, out geoSet))
                {
                    geoSet = GetRandoTypeGeo(thing);
                    enemyGeoSets[name] = geoSet;
                }

                break;
            case RandomizerConsistency4.Scene:
                string operatingScene = thing.gameObject.scene.name;

                if (!sceneGeoSets.TryGetValue(operatingScene, out Dictionary<string, RandomizedGeoSet> geoSets))
                {
                    geoSet = GetRandoTypeGeo(thing);
                    sceneGeoSets[operatingScene] = new() { { name, geoSet } };
                }
                else
                {
                    if (!geoSets.TryGetValue(name, out geoSet))
                    {
                        geoSet = GetRandoTypeGeo(thing);
                        geoSets[name] = geoSet;
                    }
                }

                break;
            case RandomizerConsistency4.None:
                geoSet = GetRandoTypeGeo(thing);
                break;
            default:
                throw new NotImplementedException();
        }

        RandomizedGeoSet GetRandoTypeGeo(HealthManager thing)
        {
            RandomizedGeoSet geoSet = new(thing);

            if (RosaryRandomizerType == RandomizeByRangeTypes.Percent)
                geoSet.MultiplyGeo(Cute_Rando_Core.TupleRandoHelper(RosaryPercentDropRange.AsTuple()));
            else if (RosaryRandomizerType == RandomizeByRangeTypes.Value)
                geoSet.SetGeoQuantity(Cute_Rando_Core.TupleRandoHelper(RosaryValueDropRange.AsTuple()));

            return geoSet;
        }
    }

    /// <summary>
    /// Cleans the current scene's HealthManager list
    /// </summary>
    private void CleanCurrentHealthManagerList() => currentEnemyHealthManagers.RemoveWhere(x => x == null);

    /// <summary>
    /// Reset all tracked lists
    /// </summary>
    private void ResetAllLists()
    {
        currentEnemyHealthManagers.Clear();
        enemyGeoSets.Clear();
        enemyShards.Clear();
        sceneGeoSets.Clear();
        sceneShards.Clear();
    }

    #region Settings
    /// <summary>
    /// Setting for how consistant the currency drops should be
    /// </summary>
    public RandomizerConsistency4 RandomizerConsistency
    {
        get => randomizerConsistency.Value;
        internal set => randomizerConsistency.Value = value;
    }
    private ConfigEntry<RandomizerConsistency4> randomizerConsistency;
    /// <summary>
    /// Default setting for how consistant the currency drops should be
    /// </summary>
    public readonly RandomizerConsistency4 defaultRandomizerConsistency = RandomizerConsistency4.None;
    /// <summary>
    /// Randomize quantity of rosaries dropped
    /// </summary>
    public RandomizeByRangeTypes RosaryRandomizerType
    {
        get => rosaryRandomizerType.Value;
        internal set => rosaryRandomizerType.Value = value;
    }
    private ConfigEntry<RandomizeByRangeTypes> rosaryRandomizerType;
    /// <summary>
    /// Default choice for rosary quantity randomizer
    /// </summary>
    public readonly RandomizeByRangeTypes defaultRosaryRandomizerType = RandomizeByRangeTypes.Disabled;
    /// <summary>
    /// Percent range for rosary drops
    /// </summary>
    public FloatRange RosaryPercentDropRange
    {
        get => rosaryPercentDropRange.Value;
        internal set => rosaryPercentDropRange.Value = value;
    }
    private ConfigEntry<FloatRange> rosaryPercentDropRange;
    /// <summary>
    /// Default percent range for rosary drops
    /// </summary>
    public readonly FloatRange defaultRosaryPercentDropRange = new(0.5f, 2.0f);
    /// <summary>
    /// Value range for rosary drops
    /// </summary>
    public IntRange RosaryValueDropRange
    {
        get => rosaryValueDropRange.Value;
        internal set => rosaryValueDropRange.Value = value;
    }
    private ConfigEntry<IntRange> rosaryValueDropRange;
    /// <summary>
    /// Default value range for rosary drops
    /// </summary>
    public readonly IntRange defaultRosaryValueDropRange = new(0, 15);
    /// <summary>
    /// Randomize quantity of shards dropped
    /// </summary>
    public RandomizeByRangeTypes ShardRandomizerType
    {
        get => shardRandomizerType.Value;
        internal set => shardRandomizerType.Value = value;
    }
    private ConfigEntry<RandomizeByRangeTypes> shardRandomizerType;
    /// <summary>
    /// Default choice for shard quantity randomizer
    /// </summary>
    public readonly RandomizeByRangeTypes defaultShardRandomizerType = RandomizeByRangeTypes.Disabled;
    /// <summary>
    /// Percent range for shard drops
    /// </summary>
    public FloatRange ShardPercentDropRange
    {
        get => shardPercentDropRange.Value;
        internal set => shardPercentDropRange.Value = value;
    }
    private ConfigEntry<FloatRange> shardPercentDropRange;
    /// <summary>
    /// Default percent range for shard drops
    /// </summary>
    public readonly FloatRange defaultShardPercentDropRange = new(0.5f, 2.0f);
    /// <summary>
    /// Value range for shard drops
    /// </summary>
    public IntRange ShardValueDropRange
    {
        get => shardValueDropRange.Value;
        internal set => shardValueDropRange.Value = value;
    }
    private ConfigEntry<IntRange> shardValueDropRange;
    /// <summary>
    /// Default value range for shard drops
    /// </summary>
    public readonly IntRange defaultShardValueDropRange = new(0, 15);

    // Used for determining if we need to update and clear the dictionaries
    private RandomizeByRangeTypes currentRosarySetting;
    private FloatRange currentRosaryFloatRange;
    private IntRange currentRosaryIntRange;
    private RandomizeByRangeTypes currentShardSetting;
    private FloatRange currentShardFloatRange;
    private IntRange currentShardIntRange;

    private protected override void InitSettings()
    {
        ConfigFile config = Settings.Settings.ConfigFile;

        rosaryRandomizerType = config.Bind(
            section: RandomizerName,
            key: "Rosary Quantity Randomizer",
            defaultValue: defaultRosaryRandomizerType,
            configDescription: new ConfigDescription(
                description: "Randomize rosary quantities dropped from enemies.",
                tags: new ConfigurationManagerAttributes
                {
                    Order = 6
                }));
        rosaryPercentDropRange = config.Bind(
            section: RandomizerName,
            key: "Rosary Percent Drop Range",
            defaultValue: defaultRosaryPercentDropRange,
            configDescription: new ConfigDescription(
                description: "Randomize the rosary drops as a percentage.",
                tags: new ConfigurationManagerAttributes
                {
                    Order = 5,
                    CustomDrawer = Settings.Settings.RangeDrawer
                }));
        rosaryValueDropRange = config.Bind(
            section: RandomizerName,
            key: "Rosary Value Drop Range",
            defaultValue: defaultRosaryValueDropRange,
            configDescription: new ConfigDescription(
                description: "Randomize the rosary drops as a flat amount.",
                tags: new ConfigurationManagerAttributes
                {
                    Order = 4,
                    CustomDrawer = Settings.Settings.RangeDrawer
                }));

        shardRandomizerType = config.Bind(
            section: RandomizerName,
            key: "Shard Quantity Randomizer",
            defaultValue: defaultShardRandomizerType,
            configDescription: new ConfigDescription(
                description: "Randomize shard quantities dropped from enemies.",
                tags: new ConfigurationManagerAttributes
                {
                    Order = 3
                }));
        shardPercentDropRange = config.Bind(
            section: RandomizerName,
            key: "Shard Percent Drop Range",
            defaultValue: defaultShardPercentDropRange,
            configDescription: new ConfigDescription(
                description: "Randomize the shard drops as a percentage.",
                tags: new ConfigurationManagerAttributes
                {
                    Order = 2,
                    CustomDrawer = Settings.Settings.RangeDrawer
                }));
        shardValueDropRange = config.Bind(
            section: RandomizerName,
            key: "Shard Value Drop Range",
            defaultValue: defaultShardValueDropRange,
            configDescription: new ConfigDescription(
                description: "Randomize the shard drops as a flat amount.",
                tags: new ConfigurationManagerAttributes
                {
                    Order = 1,
                    CustomDrawer = Settings.Settings.RangeDrawer
                }));
        randomizerConsistency = config.Bind(
            section: RandomizerName,
            key: "Currency Randomizer Consistancy",
            defaultValue: defaultRandomizerConsistency,
            configDescription: new ConfigDescription(
                description: "Setting for if the drops should be consistent per enemy or room.",
                tags: new ConfigurationManagerAttributes
                {
                    Order = 0
                }));

        currentRosarySetting = rosaryRandomizerType.Value;
        currentRosaryFloatRange = rosaryPercentDropRange.Value;
        currentRosaryIntRange = rosaryValueDropRange.Value;

        currentShardSetting = shardRandomizerType.Value;
        currentShardFloatRange = shardPercentDropRange.Value;
        currentShardIntRange = shardValueDropRange.Value;

        randomizerConsistency.SettingChanged += OnRandoConsistancyUpdated;

        rosaryRandomizerType.SettingChanged += OnRosarySettingsUpdated;
        rosaryPercentDropRange.SettingChanged += OnRosarySettingsUpdated;
        rosaryValueDropRange.SettingChanged += OnRosarySettingsUpdated;

        shardRandomizerType.SettingChanged += OnShardSettingsUpdated;
        shardPercentDropRange.SettingChanged += OnShardSettingsUpdated;
        shardValueDropRange.SettingChanged += OnShardSettingsUpdated;
    }

    /// <summary>
    /// Clears the rosary lists if needed
    /// </summary>
    /// <param name="sender">?</param>
    /// <param name="args">The setting that was changed</param>
    private void OnRosarySettingsUpdated(object sender, EventArgs args)
    {
        if (((SettingChangedEventArgs)args).ChangedSetting.BoxedValue is RandomizeByRangeTypes rvt && !rvt.Equals(currentRosarySetting))
        {
            if (currentShardSetting.Equals(RandomizeByRangeTypes.Disabled))
            {
                if (rvt.Equals(RandomizeByRangeTypes.Disabled) && !currentRosarySetting.Equals(RandomizeByRangeTypes.Disabled))
                    Unregister();

                if (currentRosarySetting.Equals(RandomizeByRangeTypes.Disabled) && !rvt.Equals(RandomizeByRangeTypes.Disabled))
                    Register();
            }

            currentRosarySetting = rvt;
            enemyGeoSets.Clear();
            sceneGeoSets.Clear();
        }
        else if (((SettingChangedEventArgs)args).ChangedSetting.BoxedValue is FloatRange floatRange && !floatRange.Equals(currentRosaryFloatRange))
        {
            currentRosaryFloatRange = floatRange;
            enemyGeoSets.Clear();
            sceneGeoSets.Clear();
        }
        else if (((SettingChangedEventArgs)args).ChangedSetting.BoxedValue is IntRange intRange && !intRange.Equals(currentRosaryIntRange))
        {
            currentRosaryIntRange = intRange;
            enemyGeoSets.Clear();
            sceneGeoSets.Clear();
        }
    }

    /// <summary>
    /// Clears the shard lists if needed
    /// </summary>
    /// <param name="sender">?</param>
    /// <param name="args">The setting that was changed</param>
    private void OnShardSettingsUpdated(object sender, EventArgs args)
    {
        if (((SettingChangedEventArgs)args).ChangedSetting.BoxedValue is RandomizeByRangeTypes rvt && !rvt.Equals(currentShardSetting))
        {
            if (currentRosarySetting.Equals(RandomizeByRangeTypes.Disabled))
            {
                if (rvt.Equals(RandomizeByRangeTypes.Disabled) && !currentShardSetting.Equals(RandomizeByRangeTypes.Disabled))
                    Unregister();

                if (currentShardSetting.Equals(RandomizeByRangeTypes.Disabled) && !rvt.Equals(RandomizeByRangeTypes.Disabled))
                    Register();
            }

            currentShardSetting = rvt;
            enemyGeoSets.Clear();
            sceneGeoSets.Clear();
        }
        else if (((SettingChangedEventArgs)args).ChangedSetting.BoxedValue is FloatRange fr && !fr.Equals(currentShardFloatRange))
        {
            currentShardFloatRange = fr;
            enemyGeoSets.Clear();
            sceneGeoSets.Clear();
        }
        else if (((SettingChangedEventArgs)args).ChangedSetting.BoxedValue is IntRange ir && !ir.Equals(currentShardIntRange))
        {
            currentShardIntRange = ir;
            enemyGeoSets.Clear();
            sceneGeoSets.Clear();
        }
    }

    /// <summary>
    /// Event hook for when the randomizer consistancy setting is updated
    /// </summary>
    /// <param name="sender">?</param>
    /// <param name="args">The setting that was changed</param>
    private void OnRandoConsistancyUpdated(object sender, EventArgs args) => ResetAllLists();
    #endregion
}

/// <summary>
/// Helper class to encapulate an enemies' geo drops
/// </summary>
/// <param name="SmallGeo"> Small geo to drop </param>
/// <param name="MediumGeo"> Medium geo to drop </param>
/// <param name="LargeGeo"> Large geo to drop </param>
internal record RandomizedGeoSet(int SmallGeo = 0, int MediumGeo = 0, int LargeGeo = 0)
{
    /// <summary>
    /// Small geo to drop
    /// </summary>
    public int SmallGeo { get; private set; } = SmallGeo;

    /// <summary>
    /// Medium geo to drop
    /// </summary>
    public int MediumGeo { get; private set; } = MediumGeo;

    /// <summary>
    /// Large geo to drop
    /// </summary>
    public int LargeGeo { get; private set; } = LargeGeo;

    /// <summary>
    /// Create a new geo set from the given HealthManager
    /// </summary>
    /// <param name="thing">HealthManager to extract the geo amounts from</param>
    public RandomizedGeoSet(HealthManager thing)
        : this((int)Cute_Rando_Core.TraverseHelper(thing, "smallGeoDrops").GetValue(), 
              (int)Cute_Rando_Core.TraverseHelper(thing, "mediumGeoDrops").GetValue(), 
              (int)Cute_Rando_Core.TraverseHelper(thing, "largeGeoDrops").GetValue())
    {
    }

    /// <summary>
    /// Multiplies the held geo amounts by a given float
    /// </summary>
    /// <param name="multiplier">amount to multiply by</param>
    public void MultiplyGeo(float multiplier)
    {
        SmallGeo = (int)Math.Round(SmallGeo * multiplier);
        MediumGeo = (int)Math.Round(MediumGeo * multiplier);
        LargeGeo = (int)Math.Round(LargeGeo * multiplier);
    }

    /// <summary>
    /// Sets the geo amount to drop to the given quantity, automatically separates value into their respective sizes
    /// </summary>
    /// <param name="quantity">amount of geo</param>
    public void SetGeoQuantity(int quantity)
    {
        LargeGeo = quantity / Enemy_Currency_Rando.largeGeoValue;
        MediumGeo = quantity % Enemy_Currency_Rando.largeGeoValue / Enemy_Currency_Rando.mediumGeoValue;
        SmallGeo = quantity % Enemy_Currency_Rando.largeGeoValue % Enemy_Currency_Rando.mediumGeoValue / Enemy_Currency_Rando.smallGeoValue;
    }
}

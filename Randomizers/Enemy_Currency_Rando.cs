using System;
using System.Collections.Generic;

using BepInEx.Configuration;

using GlobalSettings;

using HarmonyLib;

#if TESTING
using MonoMod.Utils;
#endif

using Newtonsoft.Json;

using Smol_Randomizer.Settings;

using UnityEngine.SceneManagement;

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
        RandomizerDescription = "Randomizes the quantity of Rosaries and Shards dropped by enemies.";

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

    protected override void Register()
    {
        CuteRandoCore.RegisterRandomizer(eventActiveEnemy);
        CuteRandoCore.RegisterRandomizer(eventOnFirstSceneFrame);
    }

    protected override void Unregister()
    {
        CuteRandoCore.UnregisterRandomizer(eventActiveEnemy);
        CuteRandoCore.UnregisterRandomizer(eventOnFirstSceneFrame);
    }

    // Unused as we don't need
    protected override void OnLoaded() { }
    protected override void OnUnload() { }

#if TESTING // Enable Saving Data
    protected override void ApplySaveData(Dictionary<string, object> savedData)
    {
        if (savedData.TryGetValue(nameof(enemyGeoSets), out object tempDict))
            enemyGeoSets.AddRange(JsonConvert.DeserializeObject<Dictionary<string, RandomizedGeoSet>>(tempDict.ToString()));
        if (savedData.TryGetValue(nameof(sceneGeoSets), out tempDict))
            sceneGeoSets.AddRange(JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, RandomizedGeoSet>>>(tempDict.ToString()));
        if (savedData.TryGetValue(nameof(enemyShards), out tempDict))
            enemyShards.AddRange(JsonConvert.DeserializeObject<Dictionary<string, int>>(tempDict.ToString()));
        if (savedData.TryGetValue(nameof(sceneShards), out tempDict))
            sceneShards.AddRange(JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, int>>>(tempDict.ToString()));
    }

    protected override void SetSaveData(Dictionary<string, object> savedData)
    {
        savedData[nameof(enemyGeoSets)] = enemyGeoSets;
        savedData[nameof(sceneGeoSets)] = sceneGeoSets;
        savedData[nameof(enemyShards)] = enemyShards;
        savedData[nameof(sceneShards)] = sceneShards;
    }

    // Unneeded for this Randomizer
    protected override void OnSettingsSaved() { }
#endif

    /// <summary>
    /// On First Frame, clean the active health manager list. This is done on the first frame since some health managers get added before the scene is loaded, and some afterwards.
    /// </summary>
    /// <param name="scene">The scene we are in</param>
    private void OnFirstSceneFrame(Scene scene)
    {
        currentEnemyHealthManagers.RemoveWhere(x => x == null);
    }

    /// <summary>
    /// Updates an enemy with new currency values
    /// </summary>
    /// <param name="thing">The HealthManager to adjust values in</param>
    private void SetCurrency(HealthManager thing)
    {
        if (thing == null || !Instance.currentEnemyHealthManagers.Add(thing)) return;

        if (RosaryRandomizerType != RandomizeByRangeTypes.Disabled)
        {
            RandomizeGeo(thing, out RandomizedGeoSet geoSet);

            CuteRandoCore.TraverseCreator(thing, "smallGeoDrops").SetValue(geoSet.SmallGeo);
            CuteRandoCore.TraverseCreator(thing, "mediumGeoDrops").SetValue(geoSet.MediumGeo);
            CuteRandoCore.TraverseCreator(thing, "largeGeoDrops").SetValue(geoSet.LargeGeo);
        }

        if (ShardRandomizerType != RandomizeByRangeTypes.Disabled)
            RandomizeShards(thing);
    }

    /// <summary>
    /// Randomizes shards based on consistency settings, followed by randomizer type
    /// </summary>
    /// <param name="thing">HealthManager of the enemy</param>
    /// <param name="shellShardDrops">Shell shard drop quantitiy</param>
    /// <returns>Nukber of shards for enemy to drop</returns>
    /// <exception cref="NotImplementedException">Thrown if there is an unimplemented randomizer type.</exception>
    private void RandomizeShards(HealthManager thing)
    {
        string operatingScene = thing.gameObject.scene.name;

        Traverse shellShardDropTraverse = CuteRandoCore.TraverseCreator(thing, "shellShardDrops");
        int initialShellShardDrops = (int)shellShardDropTraverse.GetValue();

        int shards;
        string name = thing.name;

        int cullIndex = name.IndexOf('(') - 1;
        if (cullIndex > 0) name = name[..cullIndex];

        switch (RandomizerConsistency)
        {
            case RandomizerConsistencyA.EnemyType:
                if (!enemyShards.TryGetValue(name, out shards))
                {
                    shards = GetRandoTypeShards(initialShellShardDrops, CuteRandoCore.RNGSeed(name));
                    enemyShards[name] = shards;
                }

                shellShardDropTraverse.SetValue(shards);

                return;
            case RandomizerConsistencyA.Scene:

                if (!sceneShards.TryGetValue(operatingScene, out Dictionary<string, int> shardSet))
                {
                    shards = GetRandoTypeShards(initialShellShardDrops, CuteRandoCore.RNGSeed(name + operatingScene));
                    sceneShards[operatingScene] = new() { { name, shards } };
                }
                else
                {
                    if (!shardSet.TryGetValue(name, out shards))
                    {
                        shards = GetRandoTypeShards(initialShellShardDrops, CuteRandoCore.RNGSeed(name + operatingScene));
                        shardSet[name] = shards;
                    }
                }

                shellShardDropTraverse.SetValue(shards);

                return;
            case RandomizerConsistencyA.None:
                shellShardDropTraverse.SetValue(GetRandoTypeShards(initialShellShardDrops));
                return;
            default:
                throw new NotImplementedException();
        }

        // Helper to randomize shards
        int GetRandoTypeShards(int shellShardDrops, int seed = int.MinValue)
        {
            int shards = 0;
            float shardMultiplier;
            if (ShardRandomizerType == RandomizeByRangeTypes.Percent)
            {
                shardMultiplier = (float)CuteRandoCore.RandomFloat(ShardPercentDropRange.AsTuple(), seed);
                shards = (int)Math.Round(shardMultiplier * shellShardDrops);
            }
            else if (ShardRandomizerType == RandomizeByRangeTypes.Value)
                shards = CuteRandoCore.RandomInt(ShardValueDropRange.AsTuple(), seed);
            return shards;
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
            case RandomizerConsistencyA.EnemyType:
                if (!enemyGeoSets.TryGetValue(name, out geoSet))
                {
                    geoSet = GetRandoTypeGeo(thing, CuteRandoCore.RNGSeed(name));
                    enemyGeoSets[name] = geoSet;
                }

                break;
            case RandomizerConsistencyA.Scene:
                string operatingScene = thing.gameObject.scene.name;

                if (!sceneGeoSets.TryGetValue(operatingScene, out Dictionary<string, RandomizedGeoSet> geoSets))
                {
                    geoSet = GetRandoTypeGeo(thing, CuteRandoCore.RNGSeed(name + operatingScene));
                    sceneGeoSets[operatingScene] = new() { { name, geoSet } };
                }
                else
                {
                    if (!geoSets.TryGetValue(name, out geoSet))
                    {
                        geoSet = GetRandoTypeGeo(thing, CuteRandoCore.RNGSeed(name + operatingScene));
                        geoSets[name] = geoSet;
                    }
                }

                break;
            case RandomizerConsistencyA.None:
                geoSet = GetRandoTypeGeo(thing);
                break;
            default:
                throw new NotImplementedException();
        }

        RandomizedGeoSet GetRandoTypeGeo(HealthManager thing, int seed = int.MinValue)
        {
            RandomizedGeoSet geoSet = new(thing);

            if (RosaryRandomizerType == RandomizeByRangeTypes.Percent)
                geoSet.MultiplyGeo(CuteRandoCore.RandomFloat(RosaryPercentDropRange.AsTuple(), seed));
            else if (RosaryRandomizerType == RandomizeByRangeTypes.Value)
                geoSet.SetGeoQuantity(CuteRandoCore.RandomInt(RosaryValueDropRange.AsTuple(), seed));

            return geoSet;
        }
    }

    protected override void ResetAllLists()
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
    public RandomizerConsistencyA RandomizerConsistency
    {
        get => randomizerConsistency.Value;
        internal set => randomizerConsistency.Value = value;
    }
    private ConfigEntry<RandomizerConsistencyA> randomizerConsistency;
    /// <summary>
    /// Default setting for how consistant the currency drops should be
    /// </summary>
    public const RandomizerConsistencyA defaultRandomizerConsistency = RandomizerConsistencyA.None;
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
    public const RandomizeByRangeTypes defaultRosaryRandomizerType = RandomizeByRangeTypes.Disabled;
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
    public static readonly FloatRange defaultRosaryPercentDropRange = new(0.5f, 2.0f);
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
    public static readonly IntRange defaultRosaryValueDropRange = new(0, 15);
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
    public const RandomizeByRangeTypes defaultShardRandomizerType = RandomizeByRangeTypes.Disabled;
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
    public static readonly FloatRange defaultShardPercentDropRange = new(0.5f, 2.0f);
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
    public static readonly IntRange defaultShardValueDropRange = new(0, 15);

    // Used for determining if we need to update
    private RandomizeByRangeTypes currentRosarySetting;
    private RandomizeByRangeTypes currentShardSetting;

    protected override void InitSettings()
    {
        ConfigFile config = Settings.Settings.ConfigFile;

        randomizerConsistency = config.Bind(
            section: RandomizerName,
            key: "Currency Consistancy",
            defaultValue: defaultRandomizerConsistency,
            configDescription: new ConfigDescription(
                description: "Setting for if the drops should be consistent per enemy or room.",
                tags: new ConfigurationManagerAttributes
                {
                    Order = 6
                }));
        rosaryRandomizerType = config.Bind(
            section: RandomizerName,
            key: "Rosary Quantity",
            defaultValue: defaultRosaryRandomizerType,
            configDescription: new ConfigDescription(
                description: "Randomize rosary quantities dropped from enemies.",
                tags: new ConfigurationManagerAttributes
                {
                    Order = 5
                }));
        rosaryPercentDropRange = config.Bind(
            section: RandomizerName,
            key: "Rosary Percent Drop Range",
            defaultValue: defaultRosaryPercentDropRange,
            configDescription: new ConfigDescription(
                description: "Randomize the rosary drops as a percentage.",
                acceptableValues: new AcceptableRangeforFloatRange(0f, 3f),
                tags: new ConfigurationManagerAttributes
                {
                    Order = 4,
                    CustomDrawer = Settings.Settings.RangeDrawer
                }));
        rosaryValueDropRange = config.Bind(
            section: RandomizerName,
            key: "Rosary Value Drop Range",
            defaultValue: defaultRosaryValueDropRange,
            configDescription: new ConfigDescription(
                description: "Randomize the rosary drops as a flat amount.",
                acceptableValues: new AcceptableRangeforIntRange(0, 100),
                tags: new ConfigurationManagerAttributes
                {
                    Order = 3,
                    CustomDrawer = Settings.Settings.RangeDrawer
                }));

        shardRandomizerType = config.Bind(
            section: RandomizerName,
            key: "Shard Quantity",
            defaultValue: defaultShardRandomizerType,
            configDescription: new ConfigDescription(
                description: "Randomize shard quantities dropped from enemies.",
                tags: new ConfigurationManagerAttributes
                {
                    Order = 2
                }));
        shardPercentDropRange = config.Bind(
            section: RandomizerName,
            key: "Shard Percent Drop Range",
            defaultValue: defaultShardPercentDropRange,
            configDescription: new ConfigDescription(
                description: "Randomize the shard drops as a percentage.",
                acceptableValues: new AcceptableRangeforFloatRange(0f, 3f),
                tags: new ConfigurationManagerAttributes
                {
                    Order = 1,
                    CustomDrawer = Settings.Settings.RangeDrawer
                }));
        shardValueDropRange = config.Bind(
            section: RandomizerName,
            key: "Shard Value Drop Range",
            defaultValue: defaultShardValueDropRange,
            configDescription: new ConfigDescription(
                description: "Randomize the shard drops as a flat amount.",
                acceptableValues: new AcceptableRangeforIntRange(0, 50),
                tags: new ConfigurationManagerAttributes
                {
                    Order = 0,
                    CustomDrawer = Settings.Settings.RangeDrawer
                }));

        currentRosarySetting = rosaryRandomizerType.Value;

        currentShardSetting = shardRandomizerType.Value;

        rosaryRandomizerType.SettingChanged += OnRosarySettingsUpdated;
        rosaryPercentDropRange.SettingChanged += OnRosarySettingsUpdated;
        rosaryValueDropRange.SettingChanged += OnRosarySettingsUpdated;

        shardRandomizerType.SettingChanged += OnShardSettingsUpdated;
        shardPercentDropRange.SettingChanged += OnShardSettingsUpdated;
        shardValueDropRange.SettingChanged += OnShardSettingsUpdated;

        if (rosaryRandomizerType.Value.Equals(RandomizeByRangeTypes.Disabled) && shardRandomizerType.Value.Equals(RandomizeByRangeTypes.Disabled))
            SettingMenu.UpdateSubMenuColor(rosaryRandomizerType);
        else
        {
            if (!rosaryRandomizerType.Value.Equals(RandomizeByRangeTypes.Disabled))
                SettingMenu.UpdateSubMenuColor(rosaryRandomizerType);
            else
                SettingMenu.UpdateSubMenuColor(shardRandomizerType);
        }
    }

    // Unused, using separate ones for rosaries and shards
    protected override void OnSettingsUpdated(object sender, EventArgs args) { }

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
                {
                    Unregister();
                    SettingMenu.UpdateSubMenuColor(rosaryRandomizerType);
                }

                if (currentRosarySetting.Equals(RandomizeByRangeTypes.Disabled) && !rvt.Equals(RandomizeByRangeTypes.Disabled))
                {
                    Register();
                    SettingMenu.UpdateSubMenuColor(rosaryRandomizerType);
                }
            }

            currentRosarySetting = rvt;
        }

        enemyGeoSets.Clear();
        sceneGeoSets.Clear();
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
                {
                    Unregister();
                    SettingMenu.UpdateSubMenuColor(shardRandomizerType);
                }

                if (currentShardSetting.Equals(RandomizeByRangeTypes.Disabled) && !rvt.Equals(RandomizeByRangeTypes.Disabled))
                {
                    Register();
                    SettingMenu.UpdateSubMenuColor(shardRandomizerType);
                }
            }

            currentShardSetting = rvt;
        }

        enemyShards.Clear();
        sceneShards.Clear();
    }
    #endregion
}

/// <summary>
/// Helper class to encapulate an enemies' geo drops
/// </summary>
/// <param name="SmallGeo"> Small geo to drop </param>
/// <param name="MediumGeo"> Medium geo to drop </param>
/// <param name="LargeGeo"> Large geo to drop </param>
internal record RandomizedGeoSet
{
    /// <summary>
    /// Small geo to drop
    /// </summary>
    public int SmallGeo { get; private set; }

    /// <summary>
    /// Medium geo to drop
    /// </summary>
    public int MediumGeo { get; private set; }

    /// <summary>
    /// Large geo to drop
    /// </summary>
    public int LargeGeo { get; private set; }

    [JsonConstructor]
    public RandomizedGeoSet(int SmallGeo = 0, int MediumGeo = 0, int LargeGeo = 0)
    {
        this.SmallGeo = SmallGeo;
        this.MediumGeo = MediumGeo;
        this.LargeGeo = LargeGeo;
    }

    /// <summary>
    /// Create a new geo set from the given HealthManager
    /// </summary>
    /// <param name="thing">HealthManager to extract the geo amounts from</param>
    public RandomizedGeoSet(HealthManager thing)
        : this((int)CuteRandoCore.TraverseCreator(thing, "smallGeoDrops").GetValue(),
              (int)CuteRandoCore.TraverseCreator(thing, "mediumGeoDrops").GetValue(),
              (int)CuteRandoCore.TraverseCreator(thing, "largeGeoDrops").GetValue())
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

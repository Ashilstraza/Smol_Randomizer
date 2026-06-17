using BepInEx.Configuration;
using Cute_Randomizer.Settings;
using GlobalSettings;
using HarmonyLib;
using System;
using System.Collections.Generic;

namespace Cute_Randomizer.Randomizers
{
    /// <summary>
    /// Randomizer for Enemy Currency Drops
    /// </summary>
    internal class Enemy_Currency_Rando
    {
        /// <summary>
        /// Set of HealthManagers that we have touched in this scene.
        /// </summary>
        private static readonly HashSet<HealthManager> currentEnemyHealthManagers = [];
        /// <summary>
        /// Dictionary of randomized geo sets for a given enemy type
        /// </summary>
        private static readonly Dictionary<string, RandomizedGeoSet> enemyGeoSets = [];
        /// <summary>
        /// Dictionary of randomized geo sets per room for a given enemy type
        /// </summary>
        private static readonly Dictionary<string, Dictionary<string, RandomizedGeoSet>> sceneGeoSets = [];
        /// <summary>
        /// Dictionary of randomized shards for a given enemy type
        /// </summary>
        private static readonly Dictionary<string, int> enemyShards = [];
        /// <summary>
        /// Dictionary of randomized shards per room for a given enemy type
        /// </summary>
        private static readonly Dictionary<string, Dictionary<string, int>> sceneShards = [];

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

        /// <summary>
        /// If the core of the randomizer is enabled
        /// </summary>
        private static bool coreEnableRandomization = false;

        // Used for determining if we need to update and clear the dictionaries
        private static RandomizeByRangeTypes currentRosarySetting;
        private static FloatRange currentRosaryFloatRange;
        private static IntRange currentRosaryIntRange;
        private static RandomizeByRangeTypes currentShardSetting;
        private static FloatRange currentShardFloatRange;
        private static IntRange currentShardIntRange;

        #region Randomizer_Info
        /// <summary>
        /// Used for registering this randomizer in the core for when enemies activate
        /// </summary>
        private static readonly Randomizer_Info randomizerCurrency = new(
            "Enemy Currency Randomizer",
            RandomizerEventType.ActiveEnemy,
            AccessTools.Method(typeof(Enemy_Currency_Rando),
                nameof(SetCurrency)));
        /// <summary>
        /// Used for registering this randomizer in the core for first frame after a scene loads
        /// </summary>
        private static readonly Randomizer_Info randomizerOnFirstSceneFrame = new(
            "Enemy Currency Randomizer",
            RandomizerEventType.OnFirstSceneFrame,
            AccessTools.Method(typeof(Enemy_Currency_Rando),
                nameof(OnFirstSceneFrame)));
        /// <summary>
        /// Used for registering this randomizer in the core for when the game starts up
        /// </summary>
        private static readonly Randomizer_Info randomizerGameStartup = new(
            "Enemy Currency Randomizer",
            RandomizerEventType.GameStartup,
            AccessTools.Method(typeof(Enemy_Currency_Rando),
                nameof(GameStartup)));
        #endregion

        /// <summary>
        /// Initialize Enemy Currency Randomizer
        /// </summary>
        internal static void InitRandomizer()
        {
            if (!Cute_Rando_Core.RegisterRandomizer(randomizerCurrency)) return;
            if (!Cute_Rando_Core.RegisterRandomizer(randomizerOnFirstSceneFrame)) return;
            if (!Cute_Rando_Core.RegisterRandomizer(randomizerGameStartup)) return;

            smallGeoValue = Gameplay.SmallGeoValue.Value;
            mediumGeoValue = Gameplay.MediumGeoValue.Value;
            largeGeoValue = Gameplay.LargeGeoValue.Value;

            InitSettings();

            coreEnableRandomization = Settings.Settings.EnableRandomizer;
            currentRosarySetting = RosaryRandomizerType;
            currentRosaryFloatRange = RosaryPercentDropRange;
            currentRosaryIntRange = RosaryValueDropRange;
            currentShardSetting = ShardRandomizerType;
            currentShardFloatRange = ShardPercentDropRange;
            currentShardIntRange = ShardValueDropRange;
        }

        /// <summary>
        /// Patch HealthManager.OnEnable on game startup
        /// </summary>
        private static void GameStartup()
        {
            Cute_Rando_Core.harmony.Patch(
                AccessTools.Method(typeof(HealthManager), "OnEnable"),
                postfix: new HarmonyMethod(typeof(Enemy_Currency_Rando), nameof(HealthManagerOnEnablePostfix)));
        }

        /// <summary>
        /// On First Frame, clean the active health manager list. This is done on the first frame since some health managers get added before the scene is loaded, and some afterwards.
        /// </summary>
        private static void OnFirstSceneFrame()
        {
            CleanCurrentHealthManagerList();
        }

        /// <summary>
        /// Patch that hooks the end of OnEnable of objects that have a HealthManager to adjust their currency
        /// </summary>
        /// <param name="__instance">The HealthManager that we want to adjust</param>
        private static void HealthManagerOnEnablePostfix(
            ref HealthManager __instance,
            ref int ___smallGeoDrops,
            ref int ___mediumGeoDrops,
            ref int ___largeGeoDrops,
            ref int ___shellShardDrops)
        {
            if (!coreEnableRandomization) return;
            if (currentEnemyHealthManagers.Add(__instance))
            {
                SetCurrency(__instance,
                    ref ___smallGeoDrops,
                    ref ___mediumGeoDrops,
                    ref ___largeGeoDrops,
                    ref ___shellShardDrops);
            }
        }

        /// <summary>
        /// Updates an enemy with new currency values
        /// </summary>
        /// <param name="thing">the HealthManager to adjust values in</param>
        /// <exception cref="NotImplementedException">Thrown if there is an unimplemented randomizer type.</exception>
        private static void SetCurrency(
            HealthManager thing,
            ref int smallGeoDrops,
            ref int mediumGeoDrops,
            ref int largeGeoDrops,
            ref int shellShardDrops)
        {
            if (thing == null) return;

            if (RosaryRandomizerType != RandomizeByRangeTypes.Disabled)
            {
                RandomizeRosary(thing, out RandomizedGeoSet geoSet);

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
        /// <returns>shards for enemy to drop</returns>
        /// <exception cref="NotImplementedException">Thrown if there is an unimplemented randomizer type.</exception>
        private static int RandomizeShards(HealthManager thing, ref int shellShardDrops)
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

            static int GetRandoTypeShards(ref int shellShardDrops)
            {
                if (ShardRandomizerType == RandomizeByRangeTypes.Percent)
                    return (int)Math.Round(shellShardDrops * Cute_Rando_Core.TupleRandoHelper(ShardPercentDropRange.AsTuple()));
                else if (ShardRandomizerType == RandomizeByRangeTypes.Value)
                    return Cute_Rando_Core.TupleRandoHelper(ShardValueDropRange.AsTuple());
                else
                    return 0;
            }
        }

        /// <summary>
        /// Randomizes rosaries based on consistancy settings, followed by randomizer type
        /// </summary>
        /// <param name="thing">HealthManager of the enemy</param>
        /// <param name="geoSet">The geoSet of the enemy we are randomizing</param>
        /// <exception cref="NotImplementedException">Thrown if there is an unimplemented randomizer type.</exception>
        private static void RandomizeRosary(HealthManager thing, out RandomizedGeoSet geoSet)
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

            static RandomizedGeoSet GetRandoTypeGeo(HealthManager thing)
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
        private static void CleanCurrentHealthManagerList()
        {
            currentEnemyHealthManagers.RemoveWhere(x => x == null);
        }

        /// <summary>
        /// Reset all tracked lists
        /// </summary>
        private static void ResetAllLists()
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
        public static RandomizerConsistency4 RandomizerConsistency
        {
            get => randomizerConsistency.Value;
            internal set => randomizerConsistency.Value = value;
        }
        private static ConfigEntry<RandomizerConsistency4> randomizerConsistency;
        /// <summary>
        /// Default setting for how consistant the currency drops should be
        /// </summary>
        public static readonly RandomizerConsistency4 defaultRandomizerConsistency = RandomizerConsistency4.None;
        /// <summary>
        /// Randomize quantity of rosaries dropped
        /// </summary>
        public static RandomizeByRangeTypes RosaryRandomizerType
        {
            get => rosaryRandomizerType.Value;
            internal set => rosaryRandomizerType.Value = value;
        }
        private static ConfigEntry<RandomizeByRangeTypes> rosaryRandomizerType;
        /// <summary>
        /// Default choice for rosary quantity randomizer
        /// </summary>
        public static readonly RandomizeByRangeTypes defaultRosaryRandomizerType = RandomizeByRangeTypes.Disabled;
        /// <summary>
        /// Percent range for rosary drops
        /// </summary>
        public static FloatRange RosaryPercentDropRange
        {
            get => rosaryPercentDropRange.Value;
            internal set => rosaryPercentDropRange.Value = value;
        }
        private static ConfigEntry<FloatRange> rosaryPercentDropRange;
        /// <summary>
        /// Default percent range for rosary drops
        /// </summary>
        public static readonly FloatRange defaultRosaryPercentDropRange = new(0.5f, 2.0f);
        /// <summary>
        /// Value range for rosary drops
        /// </summary>
        public static IntRange RosaryValueDropRange
        {
            get => rosaryValueDropRange.Value;
            internal set => rosaryValueDropRange.Value = value;
        }
        private static ConfigEntry<IntRange> rosaryValueDropRange;
        /// <summary>
        /// Default value range for rosary drops
        /// </summary>
        public static readonly IntRange defaultRosaryValueDropRange = new(0, 15);
        /// <summary>
        /// Randomize quantity of shards dropped
        /// </summary>
        public static RandomizeByRangeTypes ShardRandomizerType
        {
            get => shardRandomizerType.Value;
            internal set => shardRandomizerType.Value = value;
        }
        private static ConfigEntry<RandomizeByRangeTypes> shardRandomizerType;
        /// <summary>
        /// Default choice for shard quantity randomizer
        /// </summary>
        public static readonly RandomizeByRangeTypes defaultShardRandomizerType = RandomizeByRangeTypes.Disabled;
        /// <summary>
        /// Percent range for shard drops
        /// </summary>
        public static FloatRange ShardPercentDropRange
        {
            get => shardPercentDropRange.Value;
            internal set => shardPercentDropRange.Value = value;
        }
        private static ConfigEntry<FloatRange> shardPercentDropRange;
        /// <summary>
        /// Default percent range for shard drops
        /// </summary>
        public static readonly FloatRange defaultShardPercentDropRange = new(0.5f, 2.0f);
        /// <summary>
        /// Value range for shard drops
        /// </summary>
        public static IntRange ShardValueDropRange
        {
            get => shardValueDropRange.Value;
            internal set => shardValueDropRange.Value = value;
        }
        private static ConfigEntry<IntRange> shardValueDropRange;
        /// <summary>
        /// Default value range for shard drops
        /// </summary>
        public static readonly IntRange defaultShardValueDropRange = new(0, 15);

        /// <summary>
        /// Config Section Name
        /// </summary>
        private static readonly string configSection = "Enemy Currency";

        /// <summary>
        /// Add the enemy currency randomizer's settings into the core
        /// </summary>
        private static void InitSettings()
        {
            var config = Settings.Settings.ConfigFile;

            rosaryRandomizerType = config.Bind(
                section: configSection,
                key: "Rosary Quantity Randomizer",
                defaultValue: defaultRosaryRandomizerType,
                configDescription: new ConfigDescription(
                    description: "Randomize rosary quantities dropped from enemies.",
                    tags: new ConfigurationManagerAttributes
                    {
                        Order = 6
                    }));
            rosaryPercentDropRange = config.Bind(
                section: configSection,
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
                section: configSection,
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
                section: configSection,
                key: "Shard Quantity Randomizer",
                defaultValue: defaultShardRandomizerType,
                configDescription: new ConfigDescription(
                    description: "Randomize shard quantities dropped from enemies.",
                    tags: new ConfigurationManagerAttributes
                    {
                        Order = 3
                    }));
            shardPercentDropRange = config.Bind(
                section: configSection,
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
                section: configSection,
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
                section: configSection,
                key: "Currency Randomizer Consistancy",
                defaultValue: defaultRandomizerConsistency,
                configDescription: new ConfigDescription(
                    description: "Setting for if the drops should be consistent per enemy or room.",
                    tags: new ConfigurationManagerAttributes
                    {
                        Order = 0
                    }));

            Settings.Settings.enableRandomizer.SettingChanged += RandoCoreSetting;
            
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
        /// Event hook for when the core enable setting for the randomizer is changed
        /// </summary>
        /// <param name="sender">?</param>
        /// <param name="args">The setting that was changed</param>
        private static void RandoCoreSetting(object sender, EventArgs args)
        {
            coreEnableRandomization = (bool)((SettingChangedEventArgs)args).ChangedSetting.BoxedValue;
        }

        /// <summary>
        /// Clears the rosary lists if needed
        /// </summary>
        /// <param name="sender">?</param>
        /// <param name="args">The setting that was changed</param>
        private static void OnRosarySettingsUpdated(object sender, EventArgs args)
        {
            if (((SettingChangedEventArgs)args).ChangedSetting.BoxedValue is RandomizeByRangeTypes rvt && !rvt.Equals(currentRosarySetting))
            {
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
        private static void OnShardSettingsUpdated(object sender, EventArgs args)
        {
            if (((SettingChangedEventArgs)args).ChangedSetting.BoxedValue is RandomizeByRangeTypes rvt && !rvt.Equals(currentShardSetting))
            {
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
        private static void OnRandoConsistancyUpdated(object sender, EventArgs args)
        {
            ResetAllLists();
        }
        #endregion
    }

    /// <summary>
    /// Helper class to encapulate an enemies' geo drops
    /// </summary>
    internal class RandomizedGeoSet
    {
        /// <summary>
        /// Small geo to drop
        /// </summary>
        public int SmallGeo => smallGeo;
        private int smallGeo;
        /// <summary>
        /// Medium geo to drop
        /// </summary>
        public int MediumGeo => mediumGeo;
        private int mediumGeo;
        /// <summary>
        /// Large geo to drop
        /// </summary>
        public int LargeGeo => largeGeo;
        private int largeGeo;

        /// <summary>
        /// Create a new geo set with the given amounts
        /// </summary>
        /// <param name="smallGeo">small geo to drop</param>
        /// <param name="mediumGeo">medium geo to drop</param>
        /// <param name="largeGeo">large geo to drop</param>
        public RandomizedGeoSet(int smallGeo = 0, int mediumGeo = 0, int largeGeo = 0)
        {
            this.smallGeo = smallGeo;
            this.mediumGeo = mediumGeo;
            this.largeGeo = largeGeo;

        }

        /// <summary>
        /// Create a new geo set from the given HealthManager
        /// </summary>
        /// <param name="thing">HealthManager to extract the geo amounts from</param>
        public RandomizedGeoSet(HealthManager thing)
        {
            smallGeo = (int)Cute_Rando_Core.TraverseHelper(thing, "smallGeoDrops").GetValue();
            mediumGeo = (int)Cute_Rando_Core.TraverseHelper(thing, "mediumGeoDrops").GetValue();
            largeGeo = (int)Cute_Rando_Core.TraverseHelper(thing, "largeGeoDrops").GetValue();
        }

        /// <summary>
        /// Multiplies the held geo amounts by a given float
        /// </summary>
        /// <param name="multiplier">amount to multiply by</param>
        public void MultiplyGeo(float multiplier)
        {
            smallGeo = (int)Math.Round(smallGeo * multiplier);
            mediumGeo = (int)Math.Round(mediumGeo * multiplier);
            largeGeo = (int)Math.Round(largeGeo * multiplier);
        }

        /// <summary>
        /// Sets the geo amount to drop to the given quantity, automatically separates value into their respective sizes
        /// </summary>
        /// <param name="quantity">amount of geo</param>
        public void SetGeoQuantity(int quantity)
        {
            largeGeo = quantity / Enemy_Currency_Rando.largeGeoValue;
            mediumGeo = (quantity % Enemy_Currency_Rando.largeGeoValue) / Enemy_Currency_Rando.mediumGeoValue;
            smallGeo = ((quantity % Enemy_Currency_Rando.largeGeoValue) % Enemy_Currency_Rando.mediumGeoValue) / Enemy_Currency_Rando.smallGeoValue;
        }
    }
}

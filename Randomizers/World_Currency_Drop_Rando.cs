using BepInEx.Configuration;
using Cute_Randomizer.Settings;
using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace Cute_Randomizer.Randomizers
{
    internal class World_Currency_Drop_Rando
    {
        /// <summary>
        /// Dictionary of multipliers per room
        /// </summary>
        private static readonly Dictionary<string, float> sceneMultiplier = [];
        /// <summary>
        /// Dictionary of Architect Crest multipliers per room
        /// </summary>
        private static readonly Dictionary<string, float> sceneACMultiplier = [];
        /// <summary>
        /// Shard multiplier when enemy consistancy is enabled (all regions drop same amount)
        /// </summary>
        private static float consistantMultiplier;
        /// <summary>
        /// Architect multiplier when enemy consistancy is enabled (all regions drop same amount)
        /// </summary>
        private static float consistantACMultiplier;

        /// <summary>
        /// If the core of the randomizer is enabled
        /// </summary>
        private static bool coreEnableRandomization = false;

        // Used for determining if we need to update and clear the dictionaries
        private static string currentScene = "";
        private static FloatRange currentArchitectCrestSetting;
        private static bool architectCrestChanging = false;
        private static FloatRange currentShardChanceSetting;
        private static bool shardChanceChanging = false;

        #region Randomizer_Info
        /// <summary>
        /// Used for registering this randomizer in the core for first frame to update currency regions
        /// </summary>
        private static readonly Randomizer_Info randomizerWorldCurrency = new(
            "World Currency Drop Randomizer",
            RandomizerEventType.ActiveLimitRegion,
            AccessTools.Method(
                typeof(World_Currency_Drop_Rando),
                nameof(SetCurrency)));
        /// <summary>
        /// Used for registering this randomizer in the core for when the scene loads
        /// </summary>
        private static readonly Randomizer_Info randomizerOnSceneLoad = new(
            "World Currency Drop Randomizer",
            RandomizerEventType.OnSceneLoad,
            AccessTools.Method(
                typeof(World_Currency_Drop_Rando),
                nameof(OnSceneLoad)));
        #endregion

        /// <summary>
        /// Initialize World Currency Drop Randomizer
        /// </summary>
        internal static void InitRandomizer()
        {
            if (!Cute_Rando_Core.RegisterRandomizer(randomizerWorldCurrency)) return;
            if (!Cute_Rando_Core.RegisterRandomizer(randomizerOnSceneLoad)) return;

            InitSettings();

            coreEnableRandomization = Settings.Settings.EnableRandomizer;
            currentShardChanceSetting = ShardChanceMultiplier;
            currentArchitectCrestSetting = ArchitectCrestMultiplier;

            if (ConsistencySetting == RandomizerConsistency3.Scene)
            {
                shardChanceChanging = true;
                architectCrestChanging = true;
                UpdateConsistantMultipliers();
            }
        }

        /// <summary>
        /// On Scene Load, save current loading scene
        /// </summary>
        /// <param name="scene">The new scene that is loading</param>
        /// <param name="mode">?</param>
        private static void OnSceneLoad(Scene scene, LoadSceneMode _)
        {
            currentScene = scene.name;
        }

        /// <summary>
        /// Updates world currency drops with new currency values
        /// </summary>
        /// <param name="region">The region to adjust</param>
        /// <exception cref="NotImplementedException">Thrown if there is an unimplemented randomizer type.</exception>
        private static void SetCurrency(ICurrencyLimitRegion region)
        {
            if (!coreEnableRandomization || region == null || (!ArchitectChanceEnable && !ShardChanceEnable)) return;

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
            float[] newArchitectProbabilities = new float[(dropChances).Length];

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
                    if (ArchitectChanceEnable) newArchitectProbabilities[i] = num * multiplierAC;
                }
                else if (ArchitectChanceEnable) newArchitectProbabilities[i] = num;
                i++;
            }

            if (ArchitectChanceEnable) architectProbabilities.SetValue(newArchitectProbabilities);
        }

        #region Settings
        /// <summary>
        /// Setting for how consistant the drop chances are
        /// </summary>
        public static RandomizerConsistency3 ConsistencySetting
        {
            get => consistencySetting.Value;
            internal set => consistencySetting.Value = value;
        }
        private static ConfigEntry<RandomizerConsistency3> consistencySetting;
        /// <summary>
        /// Default setting for how consistant the drop chances are
        /// </summary>
        public static readonly RandomizerConsistency3 defaultConsistencySetting = RandomizerConsistency3.None;
        /// <summary>
        /// Randomize shard drop chance from hitting specific walls
        /// </summary>
        public static bool ShardChanceEnable
        {
            get => shardChanceEnable.Value;
            internal set => shardChanceEnable.Value = value;
        }
        private static ConfigEntry<bool> shardChanceEnable;
        /// <summary>
        /// Default choice for wall shard drop chance randomizer
        /// </summary>
        public static readonly bool defaultShardChanceEnable = false;
        /// <summary>
        /// Percent range for regular shard drop chance multiplier
        /// </summary>
        public static FloatRange ShardChanceMultiplier
        {
            get => shardChanceMultiplier.Value;
            internal set => shardChanceMultiplier.Value = value;
        }
        private static ConfigEntry<FloatRange> shardChanceMultiplier;
        /// <summary>
        /// Default percent range for regular shard drop chance multiplier
        /// </summary>
        public static readonly FloatRange defaultShardChanceMultiplier = new(1f, 3f);
        /// <summary>
        /// Randomize architect crest shard drop chance
        /// </summary>
        public static bool ArchitectChanceEnable
        {
            get => architectChanceEnable.Value;
            internal set => architectChanceEnable.Value = value;
        }
        private static ConfigEntry<bool> architectChanceEnable;
        /// <summary>
        /// Default choice for architect crest shard drop chance randomizer
        /// </summary>
        public static readonly bool defaultArchitectChanceEnable = false;
        /// <summary>
        /// Percent range for architect crest multiplier
        /// </summary>
        public static FloatRange ArchitectCrestMultiplier
        {
            get => architectCrestMultiplier.Value;
            internal set => architectCrestMultiplier.Value = value;
        }
        private static ConfigEntry<FloatRange> architectCrestMultiplier;
        /// <summary>
        /// Default percent range for architect crest multiplier
        /// </summary>
        public static readonly FloatRange defaultArchitectCrestMultiplier = new(1f, 3f);

        /// <summary>
        /// Config Section Name
        /// </summary>
        private static readonly string configSection = "World Currency";

        /// <summary>
        /// Add the world currency drop randomizer's settings into the core
        /// </summary>
        private static void InitSettings()
        {
            ConfigFile config = Settings.Settings.ConfigFile;

            shardChanceEnable = config.Bind(
                section: configSection,
                key: "Wall Shard Drop Chance Randomizer",
                defaultValue: defaultShardChanceEnable,
                configDescription: new ConfigDescription(
                    description: "Enable wall shard drop chance multiplier.",
                    tags: new ConfigurationManagerAttributes
                    {
                        Order = 4
                    }));
            shardChanceMultiplier = config.Bind(
                section: configSection,
                key: "Wall Shard Drop Chance Multiplier",
                defaultValue: defaultShardChanceMultiplier,
                configDescription: new ConfigDescription(
                    description: "Wall shard drop chance multiplier.",
                    tags: new ConfigurationManagerAttributes
                    {
                        Order = 3,
                        CustomDrawer = Settings.Settings.RangeDrawer
                    }));
            architectChanceEnable = config.Bind(
                section: configSection,
                key: "Wall Shard Architect Crest Randomizer",
                defaultValue: defaultArchitectChanceEnable,
                configDescription: new ConfigDescription(
                    description: "Enable Architect Crest wall shard drop chance multiplier.",
                    tags: new ConfigurationManagerAttributes
                    {
                        Order = 2
                    }));
            architectCrestMultiplier = config.Bind(
                section: configSection,
                key: "Wall Shard Architect Crest Multiplier",
                defaultValue: defaultArchitectCrestMultiplier,
                configDescription: new ConfigDescription(
                    description: "Architect Crest wall shard multiplier.",
                    tags: new ConfigurationManagerAttributes
                    {
                        Order = 1,
                        CustomDrawer = Settings.Settings.RangeDrawer
                    }));
            consistencySetting = config.Bind(
                section: configSection,
                key: "Chance Consistancy",
                defaultValue: defaultConsistencySetting,
                configDescription: new ConfigDescription(
                    description: "Sets how consistant the chance for world drops are.",
                    tags: new ConfigurationManagerAttributes
                    {
                        Order = 0
                    }));

            Settings.Settings.enableRandomizer.SettingChanged += RandoCoreSetting;

            consistencySetting.SettingChanged += OnRandoConsistancyUpdated;
            shardChanceMultiplier.SettingChanged += OnShardChanceMultiplierUpdated;
            architectCrestMultiplier.SettingChanged += OnArchitectCrestMultiplierUpdated;
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
        /// Event hook for when the architect crest chance multiplier changes
        /// </summary>
        /// <param name="sender">?</param>
        /// <param name="args">The setting that was changed</param>
        private static void OnArchitectCrestMultiplierUpdated(object sender, EventArgs args)
        {
            FloatRange architectCrestFloat = (FloatRange)((SettingChangedEventArgs)args).ChangedSetting.BoxedValue;
            if (!currentArchitectCrestSetting.Equals(architectCrestFloat))
            {
                architectCrestChanging = true;
                currentArchitectCrestSetting = architectCrestFloat;
                if (ConsistencySetting == RandomizerConsistency3.Save)
                {
                    UpdateConsistantMultipliers();
                }
                else if (ConsistencySetting == RandomizerConsistency3.Scene) sceneACMultiplier.Clear();
            }
        }

        /// <summary>
        /// Event hook for when the shard chance multiplier changes
        /// </summary>
        /// <param name="sender">?</param>
        /// <param name="args">The setting that was changed</param>
        private static void OnShardChanceMultiplierUpdated(object sender, EventArgs args)
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
                else if (ConsistencySetting == RandomizerConsistency3.Scene) sceneMultiplier.Clear();
            }
        }

        /// <summary>
        /// Event hook for when the randomizer consistancy setting is updated
        /// </summary>
        /// <param name="sender">?</param>
        /// <param name="args">The setting that was changed</param>
        private static void OnRandoConsistancyUpdated(object sender, EventArgs args)
        {
            shardChanceChanging = true;
            architectCrestChanging = true;
            UpdateConsistantMultipliers();
            sceneMultiplier.Clear();
            sceneACMultiplier.Clear();
        }

        /// <summary>
        /// Reroll the multipliers when called, must have changing bools set to true to change their respective multipliers
        /// </summary>
        private static void UpdateConsistantMultipliers()
        {
            if (shardChanceChanging)
            {
                consistantMultiplier = Cute_Rando_Core.TupleRandoHelper(ShardChanceMultiplier.AsTuple());
            }

            if (architectCrestChanging)
            {
                consistantACMultiplier = Cute_Rando_Core.TupleRandoHelper(ArchitectCrestMultiplier.AsTuple());
            }

            architectCrestChanging = false;
            shardChanceChanging = false;
        }
        #endregion
    }
}

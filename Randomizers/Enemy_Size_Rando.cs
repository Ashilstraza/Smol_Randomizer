using BepInEx.Configuration;
using Cute_Randomizer.Settings;
using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cute_Randomizer.Randomizers
{
    /// <summary>
    /// Randomizer for Enemy Sizes
    /// </summary>
    internal class Enemy_Size_Rando
    {
        /// <summary>
        /// Dictionary of the enemy sizes in current game instance
        /// </summary>
        private static readonly Dictionary<string, float> enemySizes = [];
        /// <summary>
        /// Dictionary of the scene enemy sizes in current game instance
        /// </summary>
        private static readonly Dictionary<string, Dictionary<string, float>> sceneEnemySizes = [];
        /// <summary>
        /// Set of enemies that we have touched in this scene
        /// </summary>
        private static readonly HashSet<HealthManager> currentEnemyHealthManagers = [];

        /// <summary>
        /// If the core of the randomizer is enabled
        /// </summary>
        private static bool coreEnableRandomization = false;

        // Used for determining if we need to update and clear the dictionaries
        private static RandomizerEnemyTypeFlags currentSizeRandomizerSetting;
        private static FloatRange currentEnemySizePercentage;
        private static FloatRange currentBossSizePercentage;

        #region Randomizer_Info
        /// <summary>
        /// Used for registering this randomizer in the core for when enemies activate
        /// </summary>
        private static readonly Randomizer_Info randomizerActiveEnemy = new(
            "Enemy Size Randomizer",
            RandomizerEventType.ActiveEnemy,
            AccessTools.Method(
                typeof(Enemy_Size_Rando),
                nameof(SetSize)));
        /// <summary>
        /// Used for registering this randomizer in the core for when a scene loads
        /// </summary>
        private static readonly Randomizer_Info randomizerOnSceneLoad = new(
            "Enemy Size Randomizer",
            RandomizerEventType.OnFirstSceneFrame,
            AccessTools.Method(
                typeof(Enemy_Size_Rando),
                nameof(OnFirstSceneFrame)));
        /// <summary>
        /// Used for registering this randomizer in the core for when game starts up
        /// </summary>
        private static readonly Randomizer_Info randomizerGameStartup = new(
            "Enemy Size Randomizer",
            RandomizerEventType.GameStartup,
            AccessTools.Method(
                typeof(Enemy_Size_Rando),
                nameof(GameStartup)));
        #endregion

        /// <summary>
        /// Initialize Enemy Size Randomizer
        /// </summary>
        internal static void InitRandomizer()
        {
            if (!Cute_Rando_Core.RegisterRandomizer(randomizerActiveEnemy)) return;
            if (!Cute_Rando_Core.RegisterRandomizer(randomizerOnSceneLoad)) return;
            if (!Cute_Rando_Core.RegisterRandomizer(randomizerGameStartup)) return;

            InitSettings();

            coreEnableRandomization = Settings.Settings.EnableRandomizer;
        }

        /// <summary>
        /// Patch HealthManager.OnEnable on game startup
        /// </summary>
        private static void GameStartup()
        {

            Cute_Rando_Core.harmony.Patch(AccessTools.Method(
                typeof(HealthManager), "OnEnable"),
                postfix: new HarmonyMethod(typeof(Enemy_Size_Rando), nameof(HealthManagerOnEnablePostfix)));
            return;
        }

        /// <summary>
        /// On First Frame, clean the active health manager list. This is done on the first frame since some health managers get added before the scene is loaded, and some afterwards.
        /// </summary>
        private static void OnFirstSceneFrame()
        {
            CleanCurrentHealthManagerList();
        }

        /// <summary>
        /// Patch that hooks the end of OnEnable of objects that have a HealthManager to adjust their size
        /// </summary>
        /// <param name="__instance">The HealthManager of the enemy that we want to adjust the size of</param>
        internal static void HealthManagerOnEnablePostfix(ref HealthManager __instance)
        {
            if (!coreEnableRandomization || EnemySizeRandomizerSetting == RandomizerEnemyTypeFlags.None) return;
            if (currentEnemyHealthManagers.Add(__instance)) SetSize(__instance);
        }

        /// <summary>
        /// Updates an enemy with a new size
        /// </summary>
        /// <param name="thing">The health manager of the enemy we want to change</param>
        /// <exception cref="NotImplementedException">Thrown if there is an unimplemented randomizer type.</exception>
        private static void SetSize(HealthManager thing)
        {
            if (thing == null || thing.transform == null) return;

            bool boss = Cute_Rando_Core.IsBoss(thing);

            if (boss && !EnemySizeRandomizerSetting.HasFlag(RandomizerEnemyTypeFlags.Boss)) return;

            string operatingScene = thing.gameObject.scene.name;

            Walker walker = thing.gameObject.GetComponent<Walker>();
            Transform thingTransform = thing.transform;
            float tempMultiplier;
            string name = thing.name;

            int cullIndex = name.IndexOf('(') - 1;
            if (cullIndex > 0) name = name[..cullIndex];

            switch (RandomizerConsistency)
            {
                case RandomizerConsistency4.EnemyType:
                    if (enemySizes.TryGetValue(name, out tempMultiplier))
                    {
                        ApplySize(thingTransform, tempMultiplier, walker);
                    }
                    else
                    {
                        enemySizes.Add(name, RandomizeSize(boss, thingTransform, walker));
                    }
                    break;
                case RandomizerConsistency4.Scene:
                    if (sceneEnemySizes.TryGetValue(name, out Dictionary<string, float> enemySizeSet))
                    {
                        if (enemySizeSet.TryGetValue(name, out tempMultiplier))
                        {
                            ApplySize(thingTransform, tempMultiplier, walker);
                        }
                        else
                        {
                            enemySizeSet[name] = RandomizeSize(boss, thingTransform, walker);
                        }
                    }
                    else
                    {
                        sceneEnemySizes[operatingScene] = new() { { name, RandomizeSize(boss, thingTransform, walker) } };
                    }
                    break;
                case RandomizerConsistency4.None:
                    RandomizeSize(boss, thingTransform, walker);
                    break;
                default:
                    throw new NotImplementedException();
            }

            static float RandomizeSize(bool boss, Transform transform, Walker walker)
            {
                float multiplier = Cute_Rando_Core.TupleRandoHelper(boss ? BossSizePercentRange.AsTuple() : EnemySizePercentRange.AsTuple());
                ApplySize(transform, multiplier, walker);
                return multiplier;
            }

            static void ApplySize(Transform transform, float multiplier, Walker walker)
            {
                transform.localScale *= multiplier;

                if (walker != null)
                {
                    Traverse rightScale = Cute_Rando_Core.TraverseHelper(walker, "rightScale");
                    int direction = (float)rightScale.GetValue() < 0 ? -1 : 1;
                    rightScale.SetValue(transform.localScale.x * direction);
                }
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
            enemySizes.Clear();
            sceneEnemySizes.Clear();
        }

        #region Settings
        /// <summary>
        /// Setting for how consistant the enemy size should be
        /// </summary>
        public static RandomizerConsistency4 RandomizerConsistency
        {
            get => randomizerConsistency.Value;
            internal set => randomizerConsistency.Value = value;
        }
        private static ConfigEntry<RandomizerConsistency4> randomizerConsistency;
        /// <summary>
        /// Default setting for how consistant the enemy size should be
        /// </summary>
        public static readonly RandomizerConsistency4 defaultRandomizerConsistency = RandomizerConsistency4.None;
        /// <summary>
        /// Randomize the size of enemies
        /// </summary>
        public static RandomizerEnemyTypeFlags EnemySizeRandomizerSetting
        {
            get => enemySizeRandomizerSetting.Value;
            internal set => enemySizeRandomizerSetting.Value = value;
        }
        private static ConfigEntry<RandomizerEnemyTypeFlags> enemySizeRandomizerSetting;
        /// <summary>
        /// Default choice for the size randomizer
        /// </summary>
        public static readonly RandomizerEnemyTypeFlags defaultEnemySizeRandomizerSetting = RandomizerEnemyTypeFlags.None;
        /// <summary>
        /// Randomize the size of normal enemies
        /// </summary>
        public static FloatRange EnemySizePercentRange
        {
            get => enemySizePercentRange.Value;
            internal set => enemySizePercentRange.Value = value;
        }
        private static ConfigEntry<FloatRange> enemySizePercentRange;
        /// <summary>
        /// Default choice for enemy size randomizer
        /// </summary>
        public static readonly FloatRange defaultEnemySizePercentRange = new(0.35f, 1.6f);
        /// <summary>
        /// Randomize the size of boss enemies
        /// </summary>
        public static FloatRange BossSizePercentRange
        {
            get => bossSizePercentRange.Value;
            internal set => bossSizePercentRange.Value = value;
        }
        private static ConfigEntry<FloatRange> bossSizePercentRange;
        /// <summary>
        /// Default choice for boss size randomizer
        /// </summary>
        public static readonly FloatRange defaultBossSizePercentRange = new(0.85f, 1.25f);

        /// <summary>
        /// Config Section Name
        /// </summary>
        private static readonly string configSection = "Enemy Size";

        /// <summary>
        /// Adds the enemy size randomizer's settings into the core
        /// </summary>
        private static void InitSettings()
        {
            ConfigFile config = Settings.Settings.ConfigFile;
            enemySizeRandomizerSetting = config.Bind(
                section: configSection,
                key: "Enemy Size Randomizer",
                defaultValue: defaultEnemySizeRandomizerSetting,
                configDescription: new ConfigDescription(
                    description: "Allow randomization of enemy and/or boss size.",
                    tags: new ConfigurationManagerAttributes
                    {
                        Order = 3
                    }));
            enemySizePercentRange = config.Bind(
                section: configSection,
                key: "Enemy Randomizer",
                defaultValue: defaultEnemySizePercentRange,
                configDescription: new ConfigDescription(
                    description: "Randomize regular enemy size.",
                    tags: new ConfigurationManagerAttributes
                    {
                        Order = 2,
                        CustomDrawer = Settings.Settings.RangeDrawer
                    }));
            bossSizePercentRange = config.Bind(
                section: configSection,
                key: "Boss Randomizer",
                defaultValue: defaultBossSizePercentRange,
                configDescription: new ConfigDescription(
                    description: "Randomize boss size.",
                    tags: new ConfigurationManagerAttributes
                    {
                        Order = 1,
                        CustomDrawer = Settings.Settings.RangeDrawer
                    }));
            randomizerConsistency = config.Bind(
                section: configSection,
                key: "Enemy Size Randomizer Consistancy",
                defaultValue: defaultRandomizerConsistency,
                configDescription: new ConfigDescription(
                    description: "Setting for if the enemy size should be consistent per enemy type or room.",
                    tags: new ConfigurationManagerAttributes
                    {
                        Order = 0
                    }));

            Settings.Settings.enableRandomizer.SettingChanged += RandoCoreSetting;
            currentBossSizePercentage = bossSizePercentRange.Value;
            currentEnemySizePercentage = enemySizePercentRange.Value;
            currentSizeRandomizerSetting = enemySizeRandomizerSetting.Value;

            randomizerConsistency.SettingChanged += OnRandoConsistancyUpdated;
            enemySizeRandomizerSetting.SettingChanged += OnSizeRandoSettingUpdated;
            enemySizePercentRange.SettingChanged += OnEnemySizeSettingUpdated;
            bossSizePercentRange.SettingChanged += OnBossSizeSettingUpdated;

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
        /// Event hook for when the boss size setting is updated
        /// </summary>
        /// <param name="sender">?</param>
        /// <param name="args">The setting that was changed</param>
        private static void OnBossSizeSettingUpdated(object sender, EventArgs args)
        {
            if (((SettingChangedEventArgs)args).ChangedSetting.BoxedValue is FloatRange fr && !fr.Equals(currentBossSizePercentage))
            {
                currentBossSizePercentage = fr;
                ResetAllLists();
            }
        }

        /// <summary>
        /// Event hook for when the enemy health setting is updated
        /// </summary>
        /// <param name="sender">?</param>
        /// <param name="args">The setting that was changed</param>
        private static void OnEnemySizeSettingUpdated(object sender, EventArgs args)
        {
            if (((SettingChangedEventArgs)args).ChangedSetting.BoxedValue is FloatRange fr && !fr.Equals(currentEnemySizePercentage))
            {
                currentEnemySizePercentage = fr;
                ResetAllLists();
            }
        }

        /// <summary>
        /// Event hook for when the health randomizer setting is updated
        /// </summary>
        /// <param name="sender">?</param>
        /// <param name="args">The setting that was changed</param>
        private static void OnSizeRandoSettingUpdated(object sender, EventArgs args)
        {
            if (((SettingChangedEventArgs)args).ChangedSetting.BoxedValue is RandomizerEnemyTypeFlags ehr && !ehr.Equals(currentSizeRandomizerSetting))
            {
                bool x = ehr.HasFlag(RandomizerEnemyTypeFlags.None);
                bool y = currentSizeRandomizerSetting.HasFlag(RandomizerEnemyTypeFlags.None);

                if (ehr.Equals(RandomizerEnemyTypeFlags.None) && !currentSizeRandomizerSetting.Equals(RandomizerEnemyTypeFlags.None))
                {
                    Cute_Rando_Core.UnregisterRandomizer(randomizerActiveEnemy);
                    Cute_Rando_Core.UnregisterRandomizer(randomizerOnSceneLoad);
                }
                if (currentSizeRandomizerSetting.Equals(RandomizerEnemyTypeFlags.None) && !ehr.Equals(RandomizerEnemyTypeFlags.None))
                {
                    Cute_Rando_Core.RegisterRandomizer(randomizerActiveEnemy);
                    Cute_Rando_Core.RegisterRandomizer(randomizerOnSceneLoad);
                }

                currentSizeRandomizerSetting = ehr;
                ResetAllLists();
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
}

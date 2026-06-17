using BepInEx.Configuration;
using Cute_Randomizer.Settings;
using HarmonyLib;
using System;
using System.Collections.Generic;

namespace Cute_Randomizer.Randomizers
{
    /// <summary>
    /// Randomizer for Enemy Damage
    /// </summary>
    internal class Enemy_Damage_Rando
    {
        /// <summary>
        /// Dictionary of the various enemy damage numbers
        /// </summary>
        private static readonly Dictionary<string, int> enemyDamageNumbers = [];
        /// <summary>
        /// Dictionary containing the damage numbers by scene
        /// </summary>
        private static readonly Dictionary<string, Dictionary<string, int>> sceneDamageNumbers = [];
        /// <summary>
        /// Set of the currently set hero damagers
        /// </summary>
        private static readonly HashSet<DamageHero> currentHeroDamagers = [];

        /// <summary>
        /// If the core of the randomizer is enabled
        /// </summary>
        private static bool coreEnableRandomization = false;

        // Used for determining if we need to update and clear the dictionaries
        private static RandomizeByFlatAmount currentDamageModifierType;
        private static int currentDamageShift;
        private static IntRange currentDamageRange;

        #region Randomizer_Info
        /// <summary>
        /// Used for registering this randomizer in the core for when the hero damager gets enabled
        /// </summary>
        private static readonly Randomizer_Info randomizerEnemyDamage = new(
            "Enemy Damage Randomizer",
            RandomizerEventType.ActiveHeroDamager,
            AccessTools.Method(
                typeof(Enemy_Damage_Rando),
                nameof(SetDamage)));
        /// <summary>
        /// Used for registering this randomizer in the core for when game starts up
        /// </summary>
        private static readonly Randomizer_Info randomizerGameStartup = new(
            "Enemy Damage Randomizer",
            RandomizerEventType.GameStartup,
            AccessTools.Method(
                typeof(Enemy_Damage_Rando),
                nameof(GameStartup)));
        /// <summary>
        /// Used for registering this randomizer in the core for first frame after a scene loads
        /// </summary>
        private static readonly Randomizer_Info randomizerOnSceneLoad = new(
            "Enemy Damage Randomizer",
            RandomizerEventType.OnFirstSceneFrame,
            AccessTools.Method(
                typeof(Enemy_Damage_Rando),
                nameof(OnFirstSceneFrame)));
        #endregion

        /// <summary>
        /// Initialize the enemy damage randomizer
        /// </summary>
        internal static void InitRandomizer()
        {
            if (!Cute_Rando_Core.RegisterRandomizer(randomizerEnemyDamage)) return;
            if (!Cute_Rando_Core.RegisterRandomizer(randomizerGameStartup)) return;
            if (!Cute_Rando_Core.RegisterRandomizer(randomizerOnSceneLoad)) return;

            InitSettings();

            coreEnableRandomization = Settings.Settings.EnableRandomizer;
            currentDamageModifierType = DamageModifierType;
            currentDamageRange = DamageRange;
            currentDamageShift = DamageShift;
        }

        /// <summary>
        /// Patch DamageHero.OnEnable on game startup
        /// </summary>
        private static void GameStartup()
        {
            Cute_Rando_Core.harmony.Patch(AccessTools.Method(typeof(DamageHero), "OnEnable"), postfix: new HarmonyMethod(typeof(Enemy_Damage_Rando), nameof(DamageHeroOnEnablePostfix)));
        }

        /// <summary>
        /// On first frame, clean the hero damagers list. This is done the first frame because some of them activate before the scene is loaded, and some afterward
        /// </summary>
        private static void OnFirstSceneFrame()
        {
            CleanCurrentHeroDamagers();
        }

        /// <summary>
        /// Patch that hooks the end of OnEnable of damage hero objects to adjust how much damage they do
        /// </summary>
        /// <param name="__instance"></param>
        private static void DamageHeroOnEnablePostfix(ref DamageHero __instance)
        {
            if (!coreEnableRandomization || DamageModifierType == RandomizeByFlatAmount.Disabled) return;
            if (currentHeroDamagers.Add(__instance))
            {
                SetDamage(__instance);
            }
        }

        /// <summary>
        /// Updates the damager with a new value
        /// </summary>
        /// <param name="damager">The damager to be adjusted</param>
        /// <exception cref="NotImplementedException">Thrown if there is an unimplemented randomizer type.</exception>
        private static void SetDamage(DamageHero damager)
        {
            if (damager == null || damager.hazardType != GlobalEnums.HazardType.ENEMY) return;

            string operatingScene = damager.gameObject.scene.name;

            int damageValue = 0;
            string name = damager.name;

            int cullIndex = name.IndexOf('(');
            if (cullIndex > 0)
            {
                name = name[..cullIndex].TrimEnd(' ');
            }

            switch (RandomizerConsistency)
            {
                case RandomizerConsistency4.EnemyType:
                    if (!enemyDamageNumbers.TryGetValue(name, out damageValue))
                    {
                        DamageSetter(ref damageValue);
                        enemyDamageNumbers[name] = damageValue;
                    }

                    damager.damageDealt = damageValue;
                    break;
                case RandomizerConsistency4.Scene:
                    if (!sceneDamageNumbers.TryGetValue(operatingScene, out Dictionary<string, int> damageNumbersSet))
                    {
                        DamageSetter(ref damageValue);
                        sceneDamageNumbers[operatingScene] = new() { { name, damageValue } };
                    }
                    else
                    {
                        if (!damageNumbersSet.TryGetValue(name, out damageValue))
                        {
                            DamageSetter(ref damageValue);
                            damageNumbersSet[name] = damageValue;
                        }
                    }
                    break;
                case RandomizerConsistency4.None:
                    DamageSetter(ref damager.damageDealt);
                    break;
                default:
                    throw new NotImplementedException();
            }

            static void DamageSetter(ref int damage)
            {
                if (DamageModifierType == RandomizeByFlatAmount.Shift)
                {
                    damage += UnityEngine.Random.Range(DamageShift * (-1), DamageShift);
                }
                else if (DamageModifierType == RandomizeByFlatAmount.Range)
                {
                    damage = Cute_Rando_Core.TupleRandoHelper(DamageRange.AsTuple());
                }
                if (damage <= 0 && EnemyDamageMinimum) damage = 1;
                else if (damage < 0) damage = 0;
            }
        }

        /// <summary>
        /// Cleans the currentHeroDamagers
        /// </summary>
        private static void CleanCurrentHeroDamagers()
        {
            currentHeroDamagers.RemoveWhere(x => x == null);
        }

        /// <summary>
        /// Resets the various lists
        /// </summary>
        private static void ResetHeroDamagers()
        {
            currentHeroDamagers.Clear();
            enemyDamageNumbers.Clear();
            sceneDamageNumbers.Clear();
        }

        #region Settings
        /// <summary>
        /// Setting for how consistant the enemy damage should be
        /// </summary>
        public static RandomizerConsistency4 RandomizerConsistency
        {
            get => randomizerConsistency.Value;
            internal set => randomizerConsistency.Value = value;
        }
        private static ConfigEntry<RandomizerConsistency4> randomizerConsistency;
        /// <summary>
        /// Default setting for how consistant the enemy damage should be
        /// </summary>
        public static readonly RandomizerConsistency4 defaultRandomizerConsistency = RandomizerConsistency4.None;
        /// <summary>
        /// Randomize the damage of enemies
        /// </summary>
        public static RandomizeByFlatAmount DamageModifierType
        {
            get => damageModifierType.Value;
            internal set => damageModifierType.Value = value;
        }
        private static ConfigEntry<RandomizeByFlatAmount> damageModifierType;
        /// <summary>
        /// Default choice for the damage randomizer
        /// </summary>
        public static readonly RandomizeByFlatAmount defaultDamageModifierType = RandomizeByFlatAmount.Disabled;
        /// <summary>
        /// Shift the damage that enemies do by +X or -X
        /// </summary>
        public static int DamageShift
        {
            get => damageShift.Value;
            internal set => damageShift.Value = value;
        }
        private static ConfigEntry<int> damageShift;
        /// <summary>
        /// Default choice for the shift amount
        /// </summary>
        public static readonly int defaultDamageShift = 1;
        /// <summary>
        /// Randomizes the damage between a range of values
        /// </summary>
        public static IntRange DamageRange
        {
            get => damageRange.Value;
            internal set => damageRange.Value = value;
        }
        private static ConfigEntry<IntRange> damageRange;
        /// <summary>
        /// Default range for the damage
        /// </summary>
        public static readonly IntRange defaultDamageRange = new(0, 3);
        /// <summary>
        /// Locks minimum damage for an enemy to 1
        /// </summary>
        public static bool EnemyDamageMinimum
        {
            get => enemyDamageMinimum.Value;
            internal set => enemyDamageMinimum.Value = value;
        }
        private static ConfigEntry<bool> enemyDamageMinimum;
        /// <summary>
        /// Default setting if minimum damage should be enabled
        /// </summary>
        public static readonly bool defaultEnemyDamageMinimum = false;

        /// <summary>
        /// Config Section Name
        /// </summary>
        private static readonly string configSection = "Enemy Damage";

        /// <summary>
        /// Adds the enemy damage radomizer's settings into the core
        /// </summary>
        private static void InitSettings()
        {
            ConfigFile config = Settings.Settings.ConfigFile;
            damageModifierType = config.Bind(
                section: configSection,
                key: "Enemy Damage Modifier Type",
                defaultValue: defaultDamageModifierType,
                configDescription: new ConfigDescription(
                    description: "Set damage modifier type.",
                    tags: new ConfigurationManagerAttributes
                    {
                        Order = 4
                    }));
            damageShift = config.Bind(
                section: configSection,
                key: "Enemy Damage Shift",
                defaultValue: defaultDamageShift,
                configDescription: new ConfigDescription(
                    description: "Set damage shift amount. Acceptable values range from 0 to 10.",
                    acceptableValues: new AcceptableValueRange<int>(0, 10),
                    tags: new ConfigurationManagerAttributes
                    {
                        Order = 3
                    }));
            damageRange = config.Bind(
                section: configSection,
                key: "Enemy Damage Range",
                defaultValue: defaultDamageRange,
                configDescription: new ConfigDescription(
                    description: "Set damage range.",
                    tags: new ConfigurationManagerAttributes
                    {
                        Order = 2,
                        CustomDrawer = Settings.Settings.RangeDrawer
                    }));
            enemyDamageMinimum = config.Bind(
                section: configSection,
                key: "Enemy Damage Minimum",
                defaultValue: defaultEnemyDamageMinimum,
                configDescription: new ConfigDescription(
                    description: "Enforces a minimum damage for enemies to be 1. Overrides set values.",
                    tags: new ConfigurationManagerAttributes
                    {
                        Order = 1
                    }));
            randomizerConsistency = config.Bind(
                section: configSection,
                key: "Enemy Damage Randomizer Consistancy",
                defaultValue: defaultRandomizerConsistency,
                configDescription: new ConfigDescription(
                    description: "Setting for if the enemy damage should be consistent per enemy type or room.",
                    tags: new ConfigurationManagerAttributes
                    {
                        Order = 0
                    }));

            Settings.Settings.enableRandomizer.SettingChanged += RandoCoreSetting;
            randomizerConsistency.SettingChanged += OnRandoConsistancyUpdated;
            damageModifierType.SettingChanged += OnDamageSettingsUpdated;
            damageShift.SettingChanged += OnDamageSettingsUpdated;
            damageRange.SettingChanged += OnDamageSettingsUpdated;
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
        /// Event hook for when the randomizer consistancy setting is updated
        /// </summary>
        /// <param name="sender">?</param>
        /// <param name="args">The setting that was changed</param>
        private static void OnRandoConsistancyUpdated(object sender, EventArgs args)
        {
            ResetHeroDamagers();
        }

        /// <summary>
        /// Event hook for when the damage settings are updated
        /// </summary>
        /// <param name="sender">?</param>
        /// <param name="args">The setting that was changed</param>
        private static void OnDamageSettingsUpdated(object sender, EventArgs args)
        {
            if (((SettingChangedEventArgs)args).ChangedSetting.BoxedValue is RandomizeByFlatAmount dm && !dm.Equals(currentDamageModifierType))
            {
                currentDamageModifierType = dm;
                ResetHeroDamagers();
            }
            else if (((SettingChangedEventArgs)args).ChangedSetting.BoxedValue is int i && !i.Equals(currentDamageShift))
            {
                currentDamageShift = i;
                ResetHeroDamagers();
            }
            else if (((SettingChangedEventArgs)args).ChangedSetting.BoxedValue is IntRange ir && !ir.Equals(currentDamageRange))
            {
                currentDamageRange = ir;
                ResetHeroDamagers();
            }
        }
        #endregion
    }
}

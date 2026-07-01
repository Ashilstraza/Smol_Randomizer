using System;
using System.Collections.Generic;

using BepInEx.Configuration;

using HarmonyLib;

using MonoMod.Utils;

using Newtonsoft.Json;

using Smol_Randomizer.Settings;

namespace Smol_Randomizer.Randomizers;

/// <summary>
/// Randomizer for Enemy Damage
/// </summary>
internal class Enemy_Damage_Rando : Rando_Base
{
    /// <summary>
    /// We make a singleton of this rando
    /// </summary>
    private static readonly Lazy<Enemy_Damage_Rando> instance = new(() => new Enemy_Damage_Rando());
    /// <summary>
    /// Externally visible instance of this rando
    /// </summary>
    public static Enemy_Damage_Rando Instance => instance.Value;

    /// <summary>
    /// Dictionary of the various enemy damage numbers
    /// </summary>
    private readonly Dictionary<string, int> enemyDamageNumbers = [];
    /// <summary>
    /// Dictionary containing the damage numbers by scene
    /// </summary>
    private readonly Dictionary<string, Dictionary<string, int>> sceneDamageNumbers = [];
    /// <summary>
    /// Set of the currently set hero damagers
    /// </summary>
    private readonly HashSet<DamageHero> currentHeroDamagers = [];

    #region Randomizer_Info
    /// <summary>
    /// Used for registering this randomizer in the core for when the hero damager gets enabled
    /// </summary>
    private Randomizer_Info eventActiveHeroDamager;
    /// <summary>
    /// Used for registering this randomizer in the core for when a scene loads
    /// </summary>
    private Randomizer_Info eventOnFirstSceneFrame;
    #endregion

    /// <summary>
    /// Constructor for this singleton
    /// </summary>
    private Enemy_Damage_Rando()
    {
        InitRandomizer();
    }

    private protected override void InitRandomizer()
    {
        RandomizerName = "Enemy Damage Randomizer";
        RandomizerDescription = "Randomizes the damage delt to Hornet by enemies and bosses.";

        if (!Cute_Rando_Core.RegisterRandomizer(new(
            RandomizerName,
            RandomizerEventType.GameStartup,
            AccessTools.Method(
                typeof(Enemy_Damage_Rando),
                nameof(GameStartup)),
            this))) return;

        eventActiveHeroDamager = new(
            RandomizerName,
            RandomizerEventType.ActiveHeroDamager,
            AccessTools.Method(
                typeof(Enemy_Damage_Rando),
                nameof(SetDamage)),
            this);
        eventOnFirstSceneFrame = new(
            RandomizerName,
            RandomizerEventType.OnFirstSceneFrame,
            AccessTools.Method(
                typeof(Enemy_Damage_Rando),
                nameof(OnFirstSceneFrame)),
            this);

        base.InitRandomizer();
    }

    private protected override void Register()
    {
        Cute_Rando_Core.RegisterRandomizer(eventActiveHeroDamager);
        Cute_Rando_Core.RegisterRandomizer(eventOnFirstSceneFrame);
    }

    private protected override void Unregister()
    {
        Cute_Rando_Core.UnregisterRandomizer(eventActiveHeroDamager);
        Cute_Rando_Core.UnregisterRandomizer(eventOnFirstSceneFrame);
    }

    private protected override void ApplySaveData(Dictionary<string, object> savedData)
    {
        if (savedData.TryGetValue(nameof(enemyDamageNumbers), out object tempDict))
            enemyDamageNumbers.AddRange(JsonConvert.DeserializeObject<Dictionary<string, int>>(tempDict.ToString()));
        if (savedData.TryGetValue(nameof(sceneDamageNumbers), out tempDict))
            sceneDamageNumbers.AddRange(JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, int>>>(tempDict.ToString()));
    }

    private protected override void SetSaveData(Dictionary<string, object> savedData)
    {
        savedData[nameof(enemyDamageNumbers)] = enemyDamageNumbers;
        savedData[nameof(sceneDamageNumbers)] = sceneDamageNumbers;
    }

    /// <summary>
    /// Patch DamageHero.OnEnable on game startup
    /// </summary>
    private void GameStartup()
    {
        Cute_Rando_Core.harmony.Patch(
            AccessTools.Method(typeof(DamageHero), "OnEnable"),
            postfix: new HarmonyMethod(typeof(Enemy_Damage_Rando), nameof(DamageHeroOnEnablePostfix)));
    }

    /// <summary>
    /// On first frame, clean the hero damagers list. This is done the first frame because some of them activate before the scene is loaded, and some afterward
    /// </summary>
    private void OnFirstSceneFrame()
    {
        CleanCurrentHeroDamagers();
    }

    /// <summary>
    /// Patch that hooks the end of OnEnable of damage hero objects to adjust how much damage they do
    /// </summary>
    /// <param name="__instance"></param>
    private static void DamageHeroOnEnablePostfix(ref DamageHero __instance)
    {
        if (!Instance.coreEnableRandomization || Instance.DamageModifierType == RandomizeByFlatAmount.Disabled)
            return;

        if (Instance.currentHeroDamagers.Add(__instance))
            Instance.SetDamage(__instance);
    }

    /// <summary>
    /// Updates the damager with a new value
    /// </summary>
    /// <param name="damager">The damager to be adjusted</param>
    /// <exception cref="NotImplementedException">Thrown if there is an unimplemented randomizer type.</exception>
    private void SetDamage(DamageHero damager)
    {
        if (damager == null || damager.hazardType != GlobalEnums.HazardType.ENEMY) return;

        string operatingScene = damager.gameObject.scene.name;

        int damageValue = damager.damageDealt;
        string name = damager.name;

        int cullIndex = name.IndexOf('(');
        if (cullIndex > 0) name = name[..cullIndex].TrimEnd(' ');

        switch (RandomizerConsistency)
        {
            case RandomizerConsistencyA.EnemyType:
                if (!enemyDamageNumbers.TryGetValue(name, out damageValue))
                {
                    DamageSetter(ref damageValue);
                    enemyDamageNumbers[name] = damageValue;
                }

                damager.damageDealt = damageValue;
                break;
            case RandomizerConsistencyA.Scene:
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
                damager.damageDealt = damageValue;

                break;
            case RandomizerConsistencyA.None:
                DamageSetter(ref damager.damageDealt);
                break;
            default:
                throw new NotImplementedException();
        }

        void DamageSetter(ref int damage)
        {
            if (DamageModifierType == RandomizeByFlatAmount.Shift)
                damage += UnityEngine.Random.Range(DamageShift * (-1), DamageShift);
            else if (DamageModifierType == RandomizeByFlatAmount.Range)
                damage = Cute_Rando_Core.TupleRandoHelper(DamageRange.AsTuple());

            if (damage <= 0 && EnemyDamageMinimum) damage = 1;
            else if (damage < 0) damage = 0;
        }
    }

    /// <summary>
    /// Cleans the currentHeroDamagers
    /// </summary>
    private void CleanCurrentHeroDamagers()
    {
        currentHeroDamagers.RemoveWhere(x => x == null);
    }

    private protected override void ResetAllLists()
    {
        currentHeroDamagers.Clear();
        enemyDamageNumbers.Clear();
        sceneDamageNumbers.Clear();
    }

    #region Settings
    /// <summary>
    /// Setting for how consistant the enemy damage should be
    /// </summary>
    public RandomizerConsistencyA RandomizerConsistency
    {
        get => randomizerConsistency.Value;
        internal set => randomizerConsistency.Value = value;
    }
    private ConfigEntry<RandomizerConsistencyA> randomizerConsistency;
    /// <summary>
    /// Default setting for how consistant the enemy damage should be
    /// </summary>
    public readonly RandomizerConsistencyA defaultRandomizerConsistency = RandomizerConsistencyA.None;
    /// <summary>
    /// Randomize the damage of enemies
    /// </summary>
    public RandomizeByFlatAmount DamageModifierType
    {
        get => damageModifierType.Value;
        internal set => damageModifierType.Value = value;
    }
    private ConfigEntry<RandomizeByFlatAmount> damageModifierType;
    /// <summary>
    /// Default choice for the damage randomizer
    /// </summary>
    public readonly RandomizeByFlatAmount defaultDamageModifierType = RandomizeByFlatAmount.Disabled;
    /// <summary>
    /// Shift the damage that enemies do by +X or -X
    /// </summary>
    public int DamageShift
    {
        get => damageShift.Value;
        internal set => damageShift.Value = value;
    }
    private ConfigEntry<int> damageShift;
    /// <summary>
    /// Default choice for the shift amount
    /// </summary>
    public readonly int defaultDamageShift = 1;
    /// <summary>
    /// Randomizes the damage between a range of values
    /// </summary>
    public IntRange DamageRange
    {
        get => damageRange.Value;
        internal set => damageRange.Value = value;
    }
    private ConfigEntry<IntRange> damageRange;
    /// <summary>
    /// Default range for the damage
    /// </summary>
    public readonly IntRange defaultDamageRange = new(0, 3);
    /// <summary>
    /// Locks minimum damage for an enemy to 1
    /// </summary>
    public bool EnemyDamageMinimum
    {
        get => enemyDamageMinimum.Value;
        internal set => enemyDamageMinimum.Value = value;
    }
    private ConfigEntry<bool> enemyDamageMinimum;
    /// <summary>
    /// Default setting if minimum damage should be enabled
    /// </summary>
    public readonly bool defaultEnemyDamageMinimum = false;

    // Used for determining if we need to update and clear the dictionaries
    private RandomizeByFlatAmount currentDamageModifierType;

    private protected override void InitSettings()
    {
        ConfigFile config = Settings.Settings.ConfigFile;
        damageModifierType = config.Bind(
            section: RandomizerName,
            key: "Enemy Damage Modifier Type",
            defaultValue: defaultDamageModifierType,
            configDescription: new ConfigDescription(
                description: "Damage modifier type. Shift adjusts by a random amount. Range randomizes within a range.",
                tags: new ConfigurationManagerAttributes
                {
                    Order = 4
                }));
        randomizerConsistency = config.Bind(
            section: RandomizerName,
            key: "Damage Consistancy",
            defaultValue: defaultRandomizerConsistency,
            configDescription: new ConfigDescription(
                description: "Setting for if the enemy damage should be consistent per enemy type or room.",
                tags: new ConfigurationManagerAttributes
                {
                    Order = 3
                }));
        damageShift = config.Bind(
            section: RandomizerName,
            key: "Damage Shift",
            defaultValue: defaultDamageShift,
            configDescription: new ConfigDescription(
                description: "Set damage shift amount. Acceptable values range from 0 to 10.",
                acceptableValues: new AcceptableValueRange<int>(0, 10),
                tags: new ConfigurationManagerAttributes
                {
                    Order = 2
                }));
        damageRange = config.Bind(
            section: RandomizerName,
            key: "Damage Range",
            defaultValue: defaultDamageRange,
            configDescription: new ConfigDescription(
                description: "Set damage range.",
                acceptableValues: new AcceptableRangeforIntRange(0, 10),
                tags: new ConfigurationManagerAttributes
                {
                    Order = 1,
                    CustomDrawer = Settings.Settings.RangeDrawer
                }));
        enemyDamageMinimum = config.Bind(
            section: RandomizerName,
            key: "Enemy Damage Minimum",
            defaultValue: defaultEnemyDamageMinimum,
            configDescription: new ConfigDescription(
                description: "Enforces a minimum damage for enemies to be 1. Overrides set values.",
                tags: new ConfigurationManagerAttributes
                {
                    Order = 0
                }));

        currentDamageModifierType = damageModifierType.Value;

        damageModifierType.SettingChanged += OnDamageSettingsUpdated;
        damageShift.SettingChanged += OnDamageSettingsUpdated;
        damageRange.SettingChanged += OnDamageSettingsUpdated;

        damageModifierType.SettingChanged += SettingMenu.OnRandomizerEnable;
        SettingMenu.UpdateSubMenuColor(damageModifierType);
    }

    /// <summary>
    /// Event hook for when the damage settings are updated
    /// </summary>
    /// <param name="sender">?</param>
    /// <param name="args">The setting that was changed</param>
    private void OnDamageSettingsUpdated(object sender, EventArgs args)
    {
        if (((SettingChangedEventArgs)args).ChangedSetting.BoxedValue is RandomizeByFlatAmount dm && !dm.Equals(currentDamageModifierType))
        {
            if (dm.Equals(RandomizeByFlatAmount.Disabled) && !currentDamageModifierType.Equals(RandomizeByFlatAmount.Disabled))
                Unregister();

            if (currentDamageModifierType.Equals(RandomizeByFlatAmount.Disabled) && !dm.Equals(RandomizeByFlatAmount.Disabled))
                Register();

            currentDamageModifierType = dm;
        }
    }
    #endregion
}

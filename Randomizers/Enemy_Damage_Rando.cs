using System;
using System.Collections.Generic;

using BepInEx.Configuration;

using HarmonyLib;

#if TESTING
using MonoMod.Utils;

using Newtonsoft.Json;
#endif
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

        if (!CuteRandoCore.RegisterRandomizer(new(
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

    protected override void Register()
    {
        CuteRandoCore.RegisterRandomizer(eventActiveHeroDamager);
        CuteRandoCore.RegisterRandomizer(eventOnFirstSceneFrame);
    }

    protected override void Unregister()
    {
        CuteRandoCore.UnregisterRandomizer(eventActiveHeroDamager);
        CuteRandoCore.UnregisterRandomizer(eventOnFirstSceneFrame);
    }

    // Unused as we don't need
    protected override void OnLoaded() { }
    protected override void OnUnload() { }

#if TESTING // Enable Saving Data
    protected override void ApplySaveData(Dictionary<string, object> savedData)
    {
        if (savedData.TryGetValue(nameof(enemyDamageNumbers), out object tempDict))
            enemyDamageNumbers.AddRange(JsonConvert.DeserializeObject<Dictionary<string, int>>(tempDict.ToString()));
        if (savedData.TryGetValue(nameof(sceneDamageNumbers), out tempDict))
            sceneDamageNumbers.AddRange(JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, int>>>(tempDict.ToString()));
    }

    protected override void SetSaveData(Dictionary<string, object> savedData)
    {
        savedData[nameof(enemyDamageNumbers)] = enemyDamageNumbers;
        savedData[nameof(sceneDamageNumbers)] = sceneDamageNumbers;
    }

    // Unneeded for this Randomizer
    protected override void OnSettingsSaved() { }
#endif

    /// <summary>
    /// Patch DamageHero.OnEnable on game startup
    /// </summary>
    private void GameStartup()
    {
        CuteRandoCore.harmony.Patch(
            AccessTools.Method(typeof(DamageHero), "OnEnable"),
            postfix: new HarmonyMethod(typeof(Enemy_Damage_Rando), nameof(DamageHero_OnEnable_Postfix)));
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
    private static void DamageHero_OnEnable_Postfix(ref DamageHero __instance, ref HealthManager ___healthManager)
    {
        if (!Instance.coreEnableRandomization || Instance.DamageModifierType == RandomizeByFlatAmount.Disabled)
            return;

        if (Instance.currentHeroDamagers.Add(__instance))
            Instance.SetDamage(__instance, ___healthManager);
    }

    /// <summary>
    /// Updates the damager with a new value
    /// </summary>
    /// <param name="damager">The damager to be adjusted</param>
    /// <param name="enemyHealthManager">The HealthManager of the enemy</param>
    /// <exception cref="NotImplementedException">Thrown if there is an unimplemented randomizer type.</exception>
    private void SetDamage(DamageHero damager, HealthManager enemyHealthManager)
    {
        if (damager == null || damager.hazardType != GlobalEnums.HazardType.ENEMY) return;

        string operatingScene = damager.gameObject.scene.name;

        int damageValue = damager.damageDealt;
        string name = damager.name;

        // Name of attack
        int cullIndex = name.IndexOf('(');
        if (cullIndex > 0) name = name[..cullIndex].TrimEnd(' ');

        // Name of enemy
        string enemyName;
        if (EnemyAttackConsistancy && enemyHealthManager != null)
        {
            enemyName = enemyHealthManager.name;
            cullIndex = enemyName.IndexOf('(');
            if (cullIndex > 0) enemyName = enemyName[..cullIndex].TrimEnd(' ');

            if (name != enemyName && char.IsDigit(name, name.Length - 1))
            {
            TheresMore: // Soldier: Dear god -- Spy: There's More -- Soldier: No...
                name = name[..(name.Length - 1)];
                if (char.IsDigit(name, name.Length - 1)) goto TheresMore; // I am not sure if I should hate myself for goto label usage
                if (name.LastIndexOf(' ') == name.Length - 1) name = name[..(name.Length - 1)];
            }
        }
        switch (RandomizerConsistency)
        {
            case RandomizerConsistencyA.EnemyType:
                if (!enemyDamageNumbers.TryGetValue(name, out damageValue))
                {
                    DamageSetter(ref damageValue, CuteRandoCore.RNGSeed(name));
                    enemyDamageNumbers[name] = damageValue;
                }

                damager.damageDealt = damageValue;
                break;
            case RandomizerConsistencyA.Scene:
                if (!sceneDamageNumbers.TryGetValue(operatingScene, out Dictionary<string, int> damageNumbersSet))
                {
                    DamageSetter(ref damageValue, CuteRandoCore.RNGSeed(name + operatingScene));
                    sceneDamageNumbers[operatingScene] = new() { { name, damageValue } };
                }
                else
                {
                    if (!damageNumbersSet.TryGetValue(name, out damageValue))
                    {
                        DamageSetter(ref damageValue, CuteRandoCore.RNGSeed(name + operatingScene));
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

        void DamageSetter(ref int damage, int seed = int.MinValue)
        {
            if (DamageModifierType == RandomizeByFlatAmount.Shift)
                damage += CuteRandoCore.RandomInt(DamageShift * (-1), DamageShift, seed);
            else if (DamageModifierType == RandomizeByFlatAmount.Range)
                damage = CuteRandoCore.RandomInt(DamageRange.AsTuple(), seed);

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

    protected override void ResetAllLists()
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
    public const RandomizerConsistencyA defaultRandomizerConsistency = RandomizerConsistencyA.None;
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
    public const RandomizeByFlatAmount defaultDamageModifierType = RandomizeByFlatAmount.Disabled;
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
    public const int defaultDamageShift = 1;
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
    public static readonly IntRange defaultDamageRange = new(0, 3);
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
    public const bool defaultEnemyDamageMinimum = false;

    /// <summary>
    /// If each part of an attack is the same damage
    /// </summary>
    public bool EnemyAttackConsistancy
    {
        get => enemyAttackConsistancy.Value;
        internal set => enemyAttackConsistancy.Value = value;
    }
    private ConfigEntry<bool> enemyAttackConsistancy;
    /// <summary>
    /// Default settings if each part of an attack is the same damage
    /// </summary>
    public const bool defaultEnemyAttackConsistancy = true;

    // Used for determining if we need to update and clear the dictionaries
    private RandomizeByFlatAmount currentDamageModifierType;

    protected override void InitSettings()
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
                    Order = 5
                }));
        randomizerConsistency = config.Bind(
            section: RandomizerName,
            key: "Damage Consistancy",
            defaultValue: defaultRandomizerConsistency,
            configDescription: new ConfigDescription(
                description: "Setting for if the enemy damage should be consistent per enemy type or room.",
                tags: new ConfigurationManagerAttributes
                {
                    Order = 4
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
                    Order = 3
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
                    Order = 2,
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
                    Order = 1
                }));
        enemyAttackConsistancy = config.Bind(
            section: RandomizerName,
            key: "Enemy Attack Consistancy",
            defaultValue: defaultEnemyAttackConsistancy,
            configDescription: new ConfigDescription(
                description: "Makes each part of an attack do the same damage.",
                tags: new ConfigurationManagerAttributes
                {
                    Order = 0
                }));

        currentDamageModifierType = damageModifierType.Value;

        damageModifierType.SettingChanged += OnSettingsUpdated;
        damageShift.SettingChanged += OnSettingsUpdated;
        damageRange.SettingChanged += OnSettingsUpdated;
        enemyAttackConsistancy.SettingChanged += OnSettingsUpdated;

        damageModifierType.SettingChanged += SettingMenu.OnRandomizerEnable;
        SettingMenu.UpdateSubMenuColor(damageModifierType);
    }

    protected override void OnSettingsUpdated(object sender, EventArgs args)
    {
        if (((SettingChangedEventArgs)args).ChangedSetting.BoxedValue is RandomizeByFlatAmount dm && !dm.Equals(currentDamageModifierType))
        {
            if (dm.Equals(RandomizeByFlatAmount.Disabled) && !currentDamageModifierType.Equals(RandomizeByFlatAmount.Disabled))
                Unregister();

            if (currentDamageModifierType.Equals(RandomizeByFlatAmount.Disabled) && !dm.Equals(RandomizeByFlatAmount.Disabled))
                Register();

            currentDamageModifierType = dm;
        }

        ResetAllLists();
    }
    #endregion
}

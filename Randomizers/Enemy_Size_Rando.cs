using System;
using System.Collections.Generic;

using BepInEx.Configuration;

using Smol_Randomizer.Settings;

using HarmonyLib;

using UnityEngine;

namespace Smol_Randomizer.Randomizers;

/// <summary>
/// Randomizer for Enemy Sizes
/// </summary>
internal sealed class Enemy_Size_Rando : Rando_Base
{
    /// <summary>
    /// We make a singleton of this rando
    /// </summary>
    private static readonly Lazy<Enemy_Size_Rando> instance = new(() => new Enemy_Size_Rando());
    /// <summary>
    /// Externally visible instance of this rando
    /// </summary>
    public static Enemy_Size_Rando Instance => instance.Value;

    /// <summary>
    /// Dictionary of the enemy sizes in current game instance
    /// </summary>
    private readonly Dictionary<string, float> enemySizes = [];
    /// <summary>
    /// Dictionary of the scene enemy sizes in current game instance
    /// </summary>
    private readonly Dictionary<string, Dictionary<string, float>> sceneEnemySizes = [];
    /// <summary>
    /// Set of enemies that we have touched in this scene
    /// </summary>
    private readonly HashSet<HealthManager> currentEnemyHealthManagers = [];

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
    private Enemy_Size_Rando() => InitRandomizer();

    private protected override void InitRandomizer()
    {
        RandomizerName = "Enemy Size Randomizer";

        if (!Cute_Rando_Core.RegisterRandomizer(new(
            RandomizerName,
            RandomizerEventType.GameStartup,
            AccessTools.Method(
                typeof(Enemy_Size_Rando),
                nameof(GameStartup)),
            this))) return;

        eventActiveEnemy = new(
            RandomizerName,
            RandomizerEventType.ActiveEnemy,
            AccessTools.Method(
                typeof(Enemy_Size_Rando),
                nameof(SetSize)),
            this);
        eventOnFirstSceneFrame = new(
            RandomizerName,
            RandomizerEventType.OnFirstSceneFrame,
            AccessTools.Method(
                typeof(Enemy_Size_Rando),
                nameof(OnFirstSceneFrame)),
            this);

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
        Cute_Rando_Core.harmony.Patch(AccessTools.Method(
            typeof(HealthManager), "OnEnable"),
            postfix: new HarmonyMethod(typeof(Enemy_Size_Rando), nameof(HealthManagerOnEnablePostfix)));
        return;
    }

    /// <summary>
    /// On First Frame, clean the active health manager list. This is done on the first frame since some health managers get added before the scene is loaded, and some afterwards.
    /// </summary>
    private void OnFirstSceneFrame() => CleanCurrentHealthManagerList();

    /// <summary>
    /// Patch that hooks the end of OnEnable of objects that have a HealthManager to adjust their size
    /// </summary>
    /// <param name="__instance">The HealthManager of the enemy that we want to adjust the size of</param>
    private static void HealthManagerOnEnablePostfix(ref HealthManager __instance)
    {
        if (!Instance.coreEnableRandomization || Instance.EnemySizeRandomizerSetting == RandomizerEnemyTypeFlags.None)
            return;

        if (Instance.currentEnemyHealthManagers.Add(__instance))
            Instance.SetSize(__instance);
    }

    /// <summary>
    /// Updates an enemy with a new size
    /// </summary>
    /// <param name="thing">The health manager of the enemy we want to change</param>
    /// <exception cref="NotImplementedException">Thrown if there is an unimplemented randomizer type.</exception>
    private void SetSize(HealthManager thing)
    {
        if (thing == null || thing.transform == null)return;

        bool boss = Cute_Rando_Core.IsBoss(thing);

        if (boss && !EnemySizeRandomizerSetting.HasFlag(RandomizerEnemyTypeFlags.Boss))
            return;

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
                    ApplySize(thingTransform, tempMultiplier, walker);
                else
                    enemySizes.Add(name, RandomizeSize(boss, thingTransform, walker));
                break;
            case RandomizerConsistency4.Scene:
                if (sceneEnemySizes.TryGetValue(name, out Dictionary<string, float> enemySizeSet))
                {
                    if (enemySizeSet.TryGetValue(name, out tempMultiplier))
                        ApplySize(thingTransform, tempMultiplier, walker);
                    else
                        enemySizeSet[name] = RandomizeSize(boss, thingTransform, walker);
                }
                else
                    sceneEnemySizes[operatingScene] = new() { { name, RandomizeSize(boss, thingTransform, walker) } };
                break;
            case RandomizerConsistency4.None:
                RandomizeSize(boss, thingTransform, walker);
                break;
            default:
                throw new NotImplementedException();
        }

        float RandomizeSize(bool boss, Transform transform, Walker walker)
        {
            float multiplier = Cute_Rando_Core.TupleRandoHelper(boss ? BossSizePercentRange.AsTuple() : EnemySizePercentRange.AsTuple());
            ApplySize(transform, multiplier, walker);
            return multiplier;
        }

        void ApplySize(Transform transform, float multiplier, Walker walker)
        {
            transform.localScale *= multiplier;

            if (walker != null)
            {
                Traverse rightScale = Cute_Rando_Core.TraverseHelper(walker, "rightScale");
                int direction = (float)rightScale.GetValue() < 0 ? -1 : 1;
                rightScale.SetValue(Math.Abs(transform.localScale.x) * direction);
            }
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
        enemySizes.Clear();
        sceneEnemySizes.Clear();
    }

    #region Settings
    /// <summary>
    /// Setting for how consistent the enemy size should be
    /// </summary>
    public RandomizerConsistency4 RandomizerConsistency
    {
        get => randomizerConsistency.Value;
        internal set => randomizerConsistency.Value = value;
    }
    private ConfigEntry<RandomizerConsistency4> randomizerConsistency;
    /// <summary>
    /// Default setting for how consistent the enemy size should be
    /// </summary>
    public readonly RandomizerConsistency4 defaultRandomizerConsistency = RandomizerConsistency4.None;
    /// <summary>
    /// Randomize the size of enemies
    /// </summary>
    public RandomizerEnemyTypeFlags EnemySizeRandomizerSetting
    {
        get => enemySizeRandomizerSetting.Value;
        internal set => enemySizeRandomizerSetting.Value = value;
    }
    private ConfigEntry<RandomizerEnemyTypeFlags> enemySizeRandomizerSetting;
    /// <summary>
    /// Default choice for the size randomizer
    /// </summary>
    public readonly RandomizerEnemyTypeFlags defaultEnemySizeRandomizerSetting = RandomizerEnemyTypeFlags.None;
    /// <summary>
    /// Randomize the size of normal enemies
    /// </summary>
    public FloatRange EnemySizePercentRange
    {
        get => enemySizePercentRange.Value;
        internal set => enemySizePercentRange.Value = value;
    }
    private ConfigEntry<FloatRange> enemySizePercentRange;
    /// <summary>
    /// Default choice for enemy size randomizer
    /// </summary>
    public readonly FloatRange defaultEnemySizePercentRange = new(0.35f, 1.6f);
    /// <summary>
    /// Randomize the size of boss enemies
    /// </summary>
    public FloatRange BossSizePercentRange
    {
        get => bossSizePercentRange.Value;
        internal set => bossSizePercentRange.Value = value;
    }
    private ConfigEntry<FloatRange> bossSizePercentRange;
    /// <summary>
    /// Default choice for boss size randomizer
    /// </summary>
    public readonly FloatRange defaultBossSizePercentRange = new(0.85f, 1.25f);

    // Used for determining if we need to update and clear the dictionaries
    private RandomizerEnemyTypeFlags currentSizeRandomizerSetting;
    private FloatRange currentEnemySizePercentage;
    private FloatRange currentBossSizePercentage;

    private protected override void InitSettings()
    {
        ConfigFile config = Settings.Settings.ConfigFile;
        enemySizeRandomizerSetting = config.Bind(
            section: RandomizerName,
            key: "Enemy Size Randomizer",
            defaultValue: defaultEnemySizeRandomizerSetting,
            configDescription: new ConfigDescription(
                description: "Allow randomization of enemy and/or boss size.",
                tags: new ConfigurationManagerAttributes
                {
                    Order = 3
                }));
        enemySizePercentRange = config.Bind(
            section: RandomizerName,
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
            section: RandomizerName,
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
            section: RandomizerName,
            key: "Enemy Size Randomizer Consistency",
            defaultValue: defaultRandomizerConsistency,
            configDescription: new ConfigDescription(
                description: "Setting for if the enemy size should be consistent per enemy type or room.",
                tags: new ConfigurationManagerAttributes
                {
                    Order = 0
                }));

        currentBossSizePercentage = bossSizePercentRange.Value;
        currentEnemySizePercentage = enemySizePercentRange.Value;
        currentSizeRandomizerSetting = enemySizeRandomizerSetting.Value;

        randomizerConsistency.SettingChanged += OnRandoConsistencyUpdated;

        enemySizeRandomizerSetting.SettingChanged += OnSizeRandoSettingUpdated;
        enemySizePercentRange.SettingChanged += OnEnemySizeSettingUpdated;
        bossSizePercentRange.SettingChanged += OnBossSizeSettingUpdated;
    }

    /// <summary>
    /// Event hook for when the boss size setting is updated
    /// </summary>
    /// <param name="sender">?</param>
    /// <param name="args">The setting that was changed</param>
    private void OnBossSizeSettingUpdated(object sender, EventArgs args)
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
    private void OnEnemySizeSettingUpdated(object sender, EventArgs args)
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
    private void OnSizeRandoSettingUpdated(object sender, EventArgs args)
    {
        if (((SettingChangedEventArgs)args).ChangedSetting.BoxedValue is RandomizerEnemyTypeFlags ehr && !ehr.Equals(currentSizeRandomizerSetting))
        {
            if (ehr.Equals(RandomizerEnemyTypeFlags.None) && !currentSizeRandomizerSetting.Equals(RandomizerEnemyTypeFlags.None))
            {
                Cute_Rando_Core.UnregisterRandomizer(eventActiveEnemy);
                Cute_Rando_Core.UnregisterRandomizer(eventOnFirstSceneFrame);
            }

            if (currentSizeRandomizerSetting.Equals(RandomizerEnemyTypeFlags.None) && !ehr.Equals(RandomizerEnemyTypeFlags.None))
            {
                Cute_Rando_Core.RegisterRandomizer(eventActiveEnemy);
                Cute_Rando_Core.RegisterRandomizer(eventOnFirstSceneFrame);
            }

            currentSizeRandomizerSetting = ehr;
            ResetAllLists();
        }
    }

    /// <summary>
    /// Event hook for when the randomizer consistency setting is updated
    /// </summary>
    /// <param name="sender">?</param>
    /// <param name="args">The setting that was changed</param>
    private void OnRandoConsistencyUpdated(object sender, EventArgs args) => ResetAllLists();
    #endregion
}

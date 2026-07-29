using System;
using System.Collections.Generic;

using BepInEx.Configuration;

using HarmonyLib;

using HutongGames.PlayMaker.Actions;

using Smol_Randomizer.Settings;

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

    public readonly HashSet<string> excludedEnemies = ["Splinter Queen Spike"];
    private readonly Dictionary<string, Action<HealthManager>> enemyActions = [];

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
    private Enemy_Size_Rando()
    {
        InitRandomizer();
    }

    private protected override void InitRandomizer()
    {
        RandomizerName = "Enemy Size Randomizer";
        RandomizerDescription = "Randomizes the sizes of enemies and bosses.";

        if (!CuteRandoCore.RegisterRandomizer(new(
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

    private static void PatchEnemies()
    {
        PatchSpecificEnemy("Crowman", delegate (HealthManager healthManger)
        {
            PlayMakerFSM fsm = healthManger.gameObject.GetComponent<PlayMakerFSM>();
            foreach (var state in fsm.FsmStates)
            {
                if (state.Name is "Start Rest")
                {
                    foreach (var action in state.Actions)
                    {
                        if (action.GetType() == typeof(RandomFloatEither))
                        {
                            ((RandomFloatEither)action).value1.Value = ((RandomFloatEither)action).value1.Value > 0
                                ? healthManger.transform.localScale.x
                                : healthManger.transform.localScale.x * -1;
                            ((RandomFloatEither)action).value2.Value = ((RandomFloatEither)action).value2.Value > 0
                                ? healthManger.transform.localScale.x
                                : healthManger.transform.localScale.x * -1; ;
                        }
                    }
                }
            }
        });
    }

    public static void PatchSpecificEnemy(string enemyName, Action<HealthManager> patch)
    {
        Instance.enemyActions.Add(enemyName, patch);
    }

    private protected override void Register()
    {
        CuteRandoCore.RegisterRandomizer(eventActiveEnemy);
        CuteRandoCore.RegisterRandomizer(eventOnFirstSceneFrame);
    }

    private protected override void Unregister()
    {
        CuteRandoCore.UnregisterRandomizer(eventActiveEnemy);
        CuteRandoCore.UnregisterRandomizer(eventOnFirstSceneFrame);
    }

#if TESTING // Enable Saving Data
    private protected override void ApplySaveData(Dictionary<string, object> savedData)
    {
        if (savedData.TryGetValue(nameof(enemySizes), out object tempDict))
            enemySizes.AddRange(JsonConvert.DeserializeObject<Dictionary<string, float>>(tempDict.ToString()));
        if (savedData.TryGetValue(nameof(sceneEnemySizes), out tempDict))
            sceneEnemySizes.AddRange(JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, float>>>(tempDict.ToString()));
    }

    private protected override void SetSaveData(Dictionary<string, object> savedData)
    {
        savedData[nameof(enemySizes)] = enemySizes;
        savedData[nameof(sceneEnemySizes)] = sceneEnemySizes;
    }

    // Unneeded for this Randomizer
    private protected override void OnSettingsSaved() { }
#endif

    /// <summary>
    /// Patch HealthManager.OnEnable on game startup
    /// </summary>
    private void GameStartup()
    {
        CuteRandoCore.harmony.Patch(AccessTools.Method(
            typeof(HealthManager), "OnEnable"),
            postfix: new HarmonyMethod(typeof(Enemy_Size_Rando), nameof(HealthManagerOnEnablePostfix)));
        CuteRandoCore.harmony.Patch(AccessTools.Method(
            typeof(SetScale), "DoSetScale"),
            prefix: new HarmonyMethod(typeof(Enemy_Size_Rando), nameof(SetScaleDoSetScalePrefix)));

        PatchEnemies();
    }

    /// <summary>
    /// On First Frame, clean the active health manager list. This is done on the first frame since some health managers get added before the scene is loaded, and some afterwards.
    /// </summary>
    private void OnFirstSceneFrame()
    {
        CleanCurrentHealthManagerList();
    }

    /// <summary>
    /// Patch that hooks the end of OnEnable of objects that have a HealthManager to adjust their size
    /// </summary>
    /// <param name="__instance">The HealthManager of the enemy that we want to adjust the size of</param>
    private static void HealthManagerOnEnablePostfix(ref HealthManager __instance)
    {
        if (__instance == null || !Instance.coreEnableRandomization || Instance.EnemySizeRandomizerSetting == RandomizerEnemyTypeFlags.None)
            return;

        if (Instance.currentEnemyHealthManagers.Add(__instance))
            Instance.SetSize(__instance);
    }

    /// <summary>
    /// Patch that applies the correct x scale on enemies' SetScale FSM Action
    /// </summary>
    /// <param name="__instance">The SetScale Action of the enemy</param>
    /// <returns>always true to continue processing SetScale</returns>
    private static bool SetScaleDoSetScalePrefix(ref SetScale __instance)
    {
        if (__instance.x.Value is not -1f and not 1f) return true;
        if (__instance.Owner != null && Instance.currentEnemyHealthManagers.Contains(__instance.Owner.GetComponent<HealthManager>()))
        {
            __instance.x = __instance.Owner.transform.GetScaleX();
        }
        return true;
    }

    /// <summary>
    /// Updates an enemy with a new size
    /// </summary>
    /// <param name="thing">The health manager of the enemy we want to change</param>
    /// <exception cref="NotImplementedException">Thrown if there is an unimplemented randomizer type.</exception>
    private void SetSize(HealthManager thing)
    {
        if (thing.transform == null) return;

        bool boss = CuteRandoCore.IsBoss(thing);

        if (boss && !EnemySizeRandomizerSetting.HasFlag(RandomizerEnemyTypeFlags.Boss))
            return;
        if (!boss && EnemySizeRandomizerSetting.Equals(RandomizerEnemyTypeFlags.Boss))
            return;

        string operatingScene = thing.gameObject.scene.name;

        Walker walker = thing.gameObject.GetComponent<Walker>();
        Transform thingTransform = thing.transform;
        float tempMultiplier;
        string name = thing.name;

        int cullIndex = name.IndexOf('(') - 1;
        if (cullIndex > 0) name = name[..cullIndex];

        if (Instance.excludedEnemies.Contains(name))
            return;

        switch (RandomizerConsistency)
        {
            case RandomizerConsistencyA.EnemyType:
                if (enemySizes.TryGetValue(name, out tempMultiplier))
                    ApplySize(thingTransform, tempMultiplier, walker);
                else
                    enemySizes.Add(name, RandomizeSize(boss, thingTransform, walker, CuteRandoCore.RNGSeed(name)));
                break;
            case RandomizerConsistencyA.Scene:
                if (sceneEnemySizes.TryGetValue(operatingScene, out Dictionary<string, float> enemySizeSet))
                {
                    if (enemySizeSet.TryGetValue(name, out tempMultiplier))
                        ApplySize(thingTransform, tempMultiplier, walker);
                    else
                        enemySizeSet[name] = RandomizeSize(boss, thingTransform, walker, CuteRandoCore.RNGSeed(name + operatingScene));
                }
                else
                    sceneEnemySizes[operatingScene] = new() { { name, RandomizeSize(boss, thingTransform, walker, CuteRandoCore.RNGSeed(name + operatingScene)) } };
                break;
            case RandomizerConsistencyA.None:
                RandomizeSize(boss, thingTransform, walker);
                break;
            default:
                throw new NotImplementedException();
        }

        if (enemyActions.TryGetValue(name, out Action<HealthManager>? action))
        {
            action.Invoke(thing);
        }

        float RandomizeSize(bool boss, Transform transform, Walker walker, int seed = int.MinValue)
        {
            float multiplier = CuteRandoCore.RandoHelper(boss ? BossSizePercentRange.AsTuple() : EnemySizePercentRange.AsTuple(), seed);
            ApplySize(transform, multiplier, walker);
            return multiplier;
        }

        void ApplySize(Transform transform, float multiplier, Walker walker)
        {
            transform.localScale *= multiplier;

            if (walker != null)
            {
                Traverse rightScale = CuteRandoCore.TraverseHelper(walker, "rightScale");
                int direction = (float)rightScale.GetValue() < 0 ? -1 : 1;
                rightScale.SetValue(Math.Abs(transform.localScale.x) * direction);
            }
        }
    }

    /// <summary>
    /// Cleans the current scene's HealthManager list
    /// </summary>
    private void CleanCurrentHealthManagerList()
    {
        currentEnemyHealthManagers.RemoveWhere(x => x == null);
    }

    private protected override void ResetAllLists()
    {
        currentEnemyHealthManagers.Clear();
        enemySizes.Clear();
        sceneEnemySizes.Clear();
    }

    #region Settings
    /// <summary>
    /// Setting for how consistent the enemy size should be
    /// </summary>
    public RandomizerConsistencyA RandomizerConsistency
    {
        get => randomizerConsistency.Value;
        internal set => randomizerConsistency.Value = value;
    }
    private ConfigEntry<RandomizerConsistencyA> randomizerConsistency;
    /// <summary>
    /// Default setting for how consistent the enemy size should be
    /// </summary>
    public const RandomizerConsistencyA defaultRandomizerConsistency = RandomizerConsistencyA.None;
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
    public const RandomizerEnemyTypeFlags defaultEnemySizeRandomizerSetting = RandomizerEnemyTypeFlags.None;
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
    public static readonly FloatRange defaultEnemySizePercentRange = new(0.35f, 1.6f);
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
    public static readonly FloatRange defaultBossSizePercentRange = new(0.85f, 1.25f);

    // Used for determining if we need to update
    private RandomizerEnemyTypeFlags currentSizeRandomizerSetting;

    private protected override void InitSettings()
    {
        ConfigFile config = Settings.Settings.ConfigFile;
        enemySizeRandomizerSetting = config.Bind(
            section: RandomizerName,
            key: "Randomize Size",
            defaultValue: defaultEnemySizeRandomizerSetting,
            configDescription: new ConfigDescription(
                description: "Allow randomization of enemy and/or boss size.",
                tags: new ConfigurationManagerAttributes
                {
                    Order = 3
                }));
        randomizerConsistency = config.Bind(
            section: RandomizerName,
            key: "Size Consistency",
            defaultValue: defaultRandomizerConsistency,
            configDescription: new ConfigDescription(
                description: "Setting for if the enemy size should be consistent per enemy type or room.",
                tags: new ConfigurationManagerAttributes
                {
                    Order = 2
                }));
        enemySizePercentRange = config.Bind(
            section: RandomizerName,
            key: "Enemy Size Range",
            defaultValue: defaultEnemySizePercentRange,
            configDescription: new ConfigDescription(
                description: "Randomize regular enemy size.",
                tags: new ConfigurationManagerAttributes
                {
                    Order = 1,
                    CustomDrawer = Settings.Settings.RangeDrawer
                }));
        bossSizePercentRange = config.Bind(
            section: RandomizerName,
            key: "Boss Size Range",
            defaultValue: defaultBossSizePercentRange,
            configDescription: new ConfigDescription(
                description: "Randomize boss size.",
                tags: new ConfigurationManagerAttributes
                {
                    Order = 0,
                    CustomDrawer = Settings.Settings.RangeDrawer
                }));

        currentSizeRandomizerSetting = enemySizeRandomizerSetting.Value;

        enemySizeRandomizerSetting.SettingChanged += OnSettingsUpdated;
        enemySizePercentRange.SettingChanged += OnSettingsUpdated;
        bossSizePercentRange.SettingChanged += OnSettingsUpdated;

        enemySizeRandomizerSetting.SettingChanged += SettingMenu.OnRandomizerEnable;
        SettingMenu.UpdateSubMenuColor(enemySizeRandomizerSetting);
    }

    private protected override void OnSettingsUpdated(object sender, EventArgs args)
    {
        if (((SettingChangedEventArgs)args).ChangedSetting.BoxedValue is RandomizerEnemyTypeFlags ehr && !ehr.Equals(currentSizeRandomizerSetting))
        {
            if (ehr.Equals(RandomizerEnemyTypeFlags.None) && !currentSizeRandomizerSetting.Equals(RandomizerEnemyTypeFlags.None))
            {
                CuteRandoCore.UnregisterRandomizer(eventActiveEnemy);
                CuteRandoCore.UnregisterRandomizer(eventOnFirstSceneFrame);
            }

            if (currentSizeRandomizerSetting.Equals(RandomizerEnemyTypeFlags.None) && !ehr.Equals(RandomizerEnemyTypeFlags.None))
            {
                CuteRandoCore.RegisterRandomizer(eventActiveEnemy);
                CuteRandoCore.RegisterRandomizer(eventOnFirstSceneFrame);
            }

            currentSizeRandomizerSetting = ehr;
        }

        ResetAllLists();
    }
    #endregion
}

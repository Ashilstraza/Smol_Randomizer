using System;
using System.Collections.Generic;

using BepInEx.Configuration;

using Smol_Randomizer.Settings;

using HarmonyLib;

namespace Smol_Randomizer.Randomizers;

/// <summary>
/// Randomizer for Enemy Health
/// </summary>
internal class Enemy_Health_Rando : Rando_Base
{
    /// <summary>
    /// We make a singleton of this rando
    /// </summary>
    private static readonly Lazy<Enemy_Health_Rando> instance = new(() => new Enemy_Health_Rando());
    /// <summary>
    /// Externally visible instance of this rando
    /// </summary>
    public static Enemy_Health_Rando Instance => instance.Value;

    /// <summary>
    /// Dictionary of the enemy health numbers in current game instance
    /// </summary>
    private readonly Dictionary<string, int> enemyHealthNumbers = [];
    /// <summary>
    /// Dictionary of the scene health numbers in current game instance
    /// </summary>
    private readonly Dictionary<string, Dictionary<string, int>> sceneHealthNumbers = [];
    /// <summary>
    /// Set of enemy HealthManagers that we have touched in this scene
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
    private Enemy_Health_Rando() => InitRandomizer();

    private protected override void InitRandomizer()
    {
        RandomizerName = "Enemy Health Randomizer";

        if (!Cute_Rando_Core.RegisterRandomizer(new(
            RandomizerName,
            RandomizerEventType.GameStartup,
            AccessTools.Method(
                typeof(Enemy_Health_Rando),
                nameof(GameStartup)),
            this))) return;

        eventActiveEnemy = new(
            RandomizerName,
            RandomizerEventType.ActiveEnemy,
            AccessTools.Method(
                typeof(Enemy_Health_Rando),
                nameof(SetHealth)),
            this);
        eventOnFirstSceneFrame = new(
            RandomizerName,
            RandomizerEventType.OnFirstSceneFrame,
            AccessTools.Method(
                typeof(Enemy_Health_Rando),
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
        Cute_Rando_Core.harmony.Patch(
            AccessTools.Method(typeof(HealthManager), "OnEnable"),
            postfix: new HarmonyMethod(typeof(Enemy_Health_Rando), nameof(HealthManagerOnEnablePostfix)));
    }

    /// <summary>
    /// On First Frame, clean the active health manager list. This is done on the first frame since some health managers get added before the scene is loaded, and some afterwards.
    /// </summary>
    private void OnFirstSceneFrame() => CleanCurrentHealthManagerList();

    /// <summary>
    /// Patch that hooks the end of OnEnable of objects that have a HealthManager to adjust their HP
    /// </summary>
    /// <param name="__instance">The HealthManager that we want to adjust</param>
    /// <param name="___initHp">Private field for initHp</param>
    /// <param name="___hp">Private field for hp</param>
    internal static void HealthManagerOnEnablePostfix(
        ref HealthManager __instance,
        ref int ___initHp,
        ref int ___hp)
    {
        if (!Instance.coreEnableRandomization || Instance.EnemyHealthRandomizerSetting == RandomizerEnemyTypeFlags.None)
            return;

        if (Instance.currentEnemyHealthManagers.Add(__instance))
            Instance.SetHealth(__instance, ref ___initHp, ref ___hp);
    }

    /// <summary>
    /// Updates an enemy with a new health value
    /// </summary>
    /// <param name="thing">The HealthManager to adjust hp within</param>
    /// <param name="initHp">Initial hp</param>
    /// <param name="hp">Current hp (should be same as initial hp when called)</param>
    /// <exception cref="NotImplementedException"></exception>
    private void SetHealth(HealthManager thing, ref int initHp, ref int hp)
    {
        if (thing == null) return;

        bool boss = Cute_Rando_Core.IsBoss(thing);

        if (boss && !EnemyHealthRandomizerSetting.HasFlag(RandomizerEnemyTypeFlags.Boss)) return;

        string operatingScene = thing.gameObject.scene.name;

        int tempHp;
        string name = thing.name;

        int cullIndex = name.IndexOf('(') - 1;
        if (cullIndex > 0) name = name[..cullIndex];

        switch (RandomizerConsistency)
        {
            case RandomizerConsistency4.EnemyType:
                if (enemyHealthNumbers.TryGetValue(name, out tempHp))
                    hp = initHp = tempHp;
                else
                    enemyHealthNumbers.Add(name, RandomizeHp(boss, ref initHp, ref hp));

                break;
            case RandomizerConsistency4.Scene:
                if (sceneHealthNumbers.TryGetValue(operatingScene, out Dictionary<string, int> healthManagerSet))
                {
                    if (healthManagerSet.TryGetValue(name, out tempHp))
                        hp = initHp = tempHp;
                    else
                        healthManagerSet[name] = RandomizeHp(boss, ref initHp, ref hp);
                }
                else
                    sceneHealthNumbers[operatingScene] = new() { { name, RandomizeHp(boss, ref initHp, ref hp) } };

                break;
            case RandomizerConsistency4.None:
                RandomizeHp(boss, ref initHp, ref hp);
                break;
            default:
                throw new NotImplementedException();
        }

        // Helper to randomize hp
        int RandomizeHp(bool boss, ref int initHp, ref int hp)
        {
            float randFloat = Cute_Rando_Core.TupleRandoHelper(boss ? BossHealthPercentRange.AsTuple() : EnemyHealthPercentRange.AsTuple());
            int tempHp;

            if (initHp <= 0)
            {
                tempHp = (int)Math.Round(hp * randFloat);
                hp = initHp = tempHp;
            }
            else
            {
                tempHp = (int)Math.Round(initHp * randFloat);
                hp = initHp = tempHp;
            }

            return tempHp;
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
        enemyHealthNumbers.Clear();
        sceneHealthNumbers.Clear();
    }

    #region Settings
    /// <summary>
    /// Setting for how consistent the enemy health should be
    /// </summary>
    public RandomizerConsistency4 RandomizerConsistency
    {
        get => randomizerConsistency.Value;
        internal set => randomizerConsistency.Value = value;
    }
    private ConfigEntry<RandomizerConsistency4> randomizerConsistency;
    /// <summary>
    /// Default setting for how consistent the enemy health should be
    /// </summary>
    public readonly RandomizerConsistency4 defaultRandomizerConsistency = RandomizerConsistency4.None;
    /// <summary>
    /// Randomize the health of enemies
    /// </summary>
    public RandomizerEnemyTypeFlags EnemyHealthRandomizerSetting
    {
        get => enemyHealthRandomizerSetting.Value;
        internal set => enemyHealthRandomizerSetting.Value = value;
    }
    private ConfigEntry<RandomizerEnemyTypeFlags> enemyHealthRandomizerSetting;
    /// <summary>
    /// Default choice for the health randomizer
    /// </summary>
    public readonly RandomizerEnemyTypeFlags defaultEnemyHealthRandomizerSetting = RandomizerEnemyTypeFlags.None;
    /// <summary>
    /// Randomize the health of normal enemies
    /// </summary>
    public FloatRange EnemyHealthPercentRange
    {
        get => enemyHealthPercentRange.Value;
        internal set => enemyHealthPercentRange.Value = value;
    }
    private ConfigEntry<FloatRange> enemyHealthPercentRange;
    /// <summary>
    /// Default choice for enemy health randomizer
    /// </summary>
    public readonly FloatRange defaultEnemyHealthPercentRange = new(0.25f, 3.0f);
    /// <summary>
    /// Randomize the health of boss enemies
    /// </summary>
    public FloatRange BossHealthPercentRange
    {
        get => bossHealthPercentRange.Value;
        internal set => bossHealthPercentRange.Value = value;
    }
    private ConfigEntry<FloatRange> bossHealthPercentRange;
    /// <summary>
    /// Default choice for boss health randomizer
    /// </summary>
    public readonly FloatRange defaultBossHealthPercentRange = new(0.75f, 1.25f);

    // Used for determining if we need to update and clear the dictionaries
    private RandomizerEnemyTypeFlags currentHealthRandomizerSetting;
    private FloatRange currentEnemyHealthPercentage;
    private FloatRange currentBossHealthPercentage;

    private protected override void InitSettings()
    {
        ConfigFile config = Settings.Settings.ConfigFile;
        enemyHealthRandomizerSetting = config.Bind(
            section: RandomizerName,
            key: "Randomize Health",
            defaultValue: defaultEnemyHealthRandomizerSetting,
            configDescription: new ConfigDescription(
                description: "Allow randomization of enemy and/or boss health.",
                tags: new ConfigurationManagerAttributes
                {
                    Order = 3
                }));
        randomizerConsistency = config.Bind(
            section: RandomizerName,
            key: "Health Consistency",
            defaultValue: defaultRandomizerConsistency,
            configDescription: new ConfigDescription(
                description: "Setting for if the enemy health should be consistent per enemy type or room.",
                tags: new ConfigurationManagerAttributes
                {
                    Order = 2
                }));
        enemyHealthPercentRange = config.Bind(
            section: RandomizerName,
            key: "Enemy Health Range",
            defaultValue: defaultEnemyHealthPercentRange,
            configDescription: new ConfigDescription(
                description: "Randomize regular enemy health.",
                tags: new ConfigurationManagerAttributes
                {
                    Order = 1,
                    CustomDrawer = Settings.Settings.RangeDrawer
                }));
        bossHealthPercentRange = config.Bind(
            section: RandomizerName,
            key: "Boss Health Range",
            defaultValue: defaultBossHealthPercentRange,
            configDescription: new ConfigDescription(
                description: "Randomize boss health.",
                tags: new ConfigurationManagerAttributes
                {
                    Order = 0,
                    CustomDrawer = Settings.Settings.RangeDrawer
                }));

        currentBossHealthPercentage = bossHealthPercentRange.Value;
        currentEnemyHealthPercentage = bossHealthPercentRange.Value;
        currentHealthRandomizerSetting = enemyHealthRandomizerSetting.Value;

        randomizerConsistency.SettingChanged += OnRandoConsistencyUpdated;
        enemyHealthRandomizerSetting.SettingChanged += OnHealthRandoSettingUpdated;
        enemyHealthPercentRange.SettingChanged += OnEnemyHealthSettingUpdated;
        bossHealthPercentRange.SettingChanged += OnBossHealthSettingUpdated;
    }

    /// <summary>
    /// Event hook for when the boss health setting is updated
    /// </summary>
    /// <param name="sender">?</param>
    /// <param name="args">The setting that was changed</param>
    private void OnBossHealthSettingUpdated(object sender, EventArgs args)
    {
        if (((SettingChangedEventArgs)args).ChangedSetting.BoxedValue is FloatRange fr && !fr.Equals(currentBossHealthPercentage))
        {
            currentBossHealthPercentage = fr;
            ResetAllLists();
        }
    }

    /// <summary>
    /// Event hook for when the enemy health setting is updated
    /// </summary>
    /// <param name="sender">?</param>
    /// <param name="args">The setting that was changed</param>
    private void OnEnemyHealthSettingUpdated(object sender, EventArgs args)
    {
        if (((SettingChangedEventArgs)args).ChangedSetting.BoxedValue is FloatRange fr && !fr.Equals(currentEnemyHealthPercentage))
        {
            currentEnemyHealthPercentage = fr;
            ResetAllLists();
        }
    }

    /// <summary>
    /// Event hook for when the health randomizer setting is updated
    /// </summary>
    /// <param name="sender">?</param>
    /// <param name="args">The setting that was changed</param>
    private void OnHealthRandoSettingUpdated(object sender, EventArgs args)
    {
        if (((SettingChangedEventArgs)args).ChangedSetting.BoxedValue is RandomizerEnemyTypeFlags ehr && !ehr.Equals(currentHealthRandomizerSetting))
        {
            if (ehr.Equals(RandomizerEnemyTypeFlags.None) && !currentHealthRandomizerSetting.Equals(RandomizerEnemyTypeFlags.None))
                Unregister();

            if (currentHealthRandomizerSetting.Equals(RandomizerEnemyTypeFlags.None) && !ehr.Equals(RandomizerEnemyTypeFlags.None))
                Register();

            currentHealthRandomizerSetting = ehr;
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

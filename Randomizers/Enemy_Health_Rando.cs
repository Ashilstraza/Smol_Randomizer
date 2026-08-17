using System;
using System.Collections.Generic;

using BepInEx.Configuration;

using HarmonyLib;

#if TESTING
using MonoMod.Utils;

using Newtonsoft.Json;
#endif
using Smol_Randomizer.Settings;

using UnityEngine.SceneManagement;

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
    private Enemy_Health_Rando()
    {
        InitRandomizer();
    }

    private protected override void InitRandomizer()
    {
        RandomizerName = "Enemy Health Randomizer";
        RandomizerDescription = "Randomizes the health of enemies and bosses.";

        if (!CuteRandoCore.RegisterRandomizer(new(
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
        if (savedData.TryGetValue(nameof(enemyHealthNumbers), out object tempDict))
            enemyHealthNumbers.AddRange(JsonConvert.DeserializeObject<Dictionary<string, int>>(tempDict.ToString()));
        if (savedData.TryGetValue(nameof(sceneHealthNumbers), out tempDict))
            sceneHealthNumbers.AddRange(JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, int>>>(tempDict.ToString()));
    }

    protected override void SetSaveData(Dictionary<string, object> savedData)
    {
        savedData[nameof(enemyHealthNumbers)] = enemyHealthNumbers;
        savedData[nameof(sceneHealthNumbers)] = sceneHealthNumbers;
    }

    // Unneeded for this Randomizer
    protected override void OnSettingsSaved() { }
#endif

    /// <summary>
    /// Patch HealthManager.OnEnable on game startup
    /// </summary>
    private void GameStartup()
    {
        CuteRandoCore.harmony.Patch(
            AccessTools.Method(typeof(HealthManager), "OnEnable"),
            postfix: new HarmonyMethod(typeof(Enemy_Health_Rando), nameof(HealthManager_OnEnable_Postfix)));
    }

    /// <summary>
    /// On First Frame, clean the active health manager list. This is done on the first frame since some health managers get added before the scene is loaded, and some afterwards.
    /// </summary>
    /// <param name="scene">The scene we are in</param>
    private void OnFirstSceneFrame(Scene scene)
    {
        CleanCurrentHealthManagerList();
    }

    /// <summary>
    /// Updates an enemy with a new health value
    /// </summary>
    /// <param name="thing">The HealthManager to adjust hp within</param>
    /// <exception cref="NotImplementedException">Consistency type is not implemented</exception>
    private void SetHealth(HealthManager thing)
    {
        if (thing == null) return;

        Traverse initHp = CuteRandoCore.TraverseCreator(thing, "initHp");
        Traverse hp = CuteRandoCore.TraverseCreator(thing, "hp");

        if ((int)initHp.GetValue() > 5000)
            return;

        bool boss = CuteRandoCore.IsBoss(thing);

        if (boss && !EnemyHealthRandomizerSetting.HasFlag(RandomizerEnemyTypeFlags.Boss)) return;

        string operatingScene = thing.gameObject.scene.name;

        int tempHp;
        string name = thing.name;

        int cullIndex = name.IndexOf('(') - 1;
        if (cullIndex > 0) name = name[..cullIndex];

        switch (RandomizerConsistency)
        {
            case RandomizerConsistencyA.EnemyType:
                if (enemyHealthNumbers.TryGetValue(name, out tempHp))
                    SetHp(tempHp, initHp, hp);
                else
                    enemyHealthNumbers.Add(name, RandomizeHp(boss, initHp, hp, CuteRandoCore.RNGSeed(name)));

                break;
            case RandomizerConsistencyA.Scene:
                if (sceneHealthNumbers.TryGetValue(operatingScene, out Dictionary<string, int> healthManagerSet))
                {
                    if (healthManagerSet.TryGetValue(name, out tempHp))
                        SetHp(tempHp, initHp, hp);
                    else
                        healthManagerSet[name] = RandomizeHp(boss, initHp, hp, CuteRandoCore.RNGSeed(name + operatingScene));
                }
                else
                    sceneHealthNumbers[operatingScene] = new() { { name, RandomizeHp(boss, initHp, hp, CuteRandoCore.RNGSeed(name + operatingScene)) } };

                break;
            case RandomizerConsistencyA.None:
                RandomizeHp(boss, initHp, hp);
                break;
            default:
                throw new NotImplementedException();
        }

        void SetHp(int newHp, Traverse hp, Traverse initHp)
        {
            hp.SetValue(newHp);
            initHp.SetValue(newHp);
        }

        // Helper to randomize hp
        int RandomizeHp(bool boss, Traverse initHp, Traverse hp, int seed = int.MinValue)
        {
            int initialHp = (int)initHp.GetValue();
            float randFloat = CuteRandoCore.RandomFloat(boss ? BossHealthPercentRange.AsTuple() : EnemyHealthPercentRange.AsTuple(), seed);
            int tempHp;

            if (initialHp <= 0)
            {
                tempHp = (int)Math.Round((int)hp.GetValue() * randFloat);
                SetHp(tempHp, initHp, hp);
            }
            else
            {
                tempHp = (int)Math.Round(initialHp * randFloat);
                SetHp(tempHp, initHp, hp);
            }

            return tempHp;
        }
    }

    /// <summary>
    /// Cleans the current scene's HealthManager list
    /// </summary>
    private void CleanCurrentHealthManagerList()
    {
        currentEnemyHealthManagers.RemoveWhere(x => x == null);
    }

    protected override void ResetAllLists()
    {
        currentEnemyHealthManagers.Clear();
        enemyHealthNumbers.Clear();
        sceneHealthNumbers.Clear();
    }

    #region Settings
    /// <summary>
    /// Setting for how consistent the enemy health should be
    /// </summary>
    public RandomizerConsistencyA RandomizerConsistency
    {
        get => randomizerConsistency.Value;
        internal set => randomizerConsistency.Value = value;
    }
    private ConfigEntry<RandomizerConsistencyA> randomizerConsistency;
    /// <summary>
    /// Default setting for how consistent the enemy health should be
    /// </summary>
    public const RandomizerConsistencyA defaultRandomizerConsistency = RandomizerConsistencyA.None;
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
    public const RandomizerEnemyTypeFlags defaultEnemyHealthRandomizerSetting = RandomizerEnemyTypeFlags.None;
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
    public static readonly FloatRange defaultEnemyHealthPercentRange = new(0.25f, 3.0f);
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
    public static readonly FloatRange defaultBossHealthPercentRange = new(0.75f, 1.25f);

    // Used for determining if we need to update
    private RandomizerEnemyTypeFlags currentHealthRandomizerSetting;

    protected override void InitSettings()
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

        currentHealthRandomizerSetting = enemyHealthRandomizerSetting.Value;

        enemyHealthRandomizerSetting.SettingChanged += OnSettingsUpdated;
        enemyHealthPercentRange.SettingChanged += OnSettingsUpdated;
        bossHealthPercentRange.SettingChanged += OnSettingsUpdated;

        enemyHealthRandomizerSetting.SettingChanged += SettingMenu.OnRandomizerEnable;
        SettingMenu.UpdateSubMenuColor(enemyHealthRandomizerSetting);
    }

    protected override void OnSettingsUpdated(object sender, EventArgs args)
    {
        if (((SettingChangedEventArgs)args).ChangedSetting.BoxedValue is RandomizerEnemyTypeFlags ehr && !ehr.Equals(currentHealthRandomizerSetting))
        {
            if (ehr.Equals(RandomizerEnemyTypeFlags.None) && !currentHealthRandomizerSetting.Equals(RandomizerEnemyTypeFlags.None))
                Unregister();

            if (currentHealthRandomizerSetting.Equals(RandomizerEnemyTypeFlags.None) && !ehr.Equals(RandomizerEnemyTypeFlags.None))
                Register();

            currentHealthRandomizerSetting = ehr;
        }

        ResetAllLists();
    }
    #endregion
}

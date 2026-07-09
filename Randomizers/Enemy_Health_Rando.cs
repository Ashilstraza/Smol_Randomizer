using System;
using System.Collections.Generic;

using BepInEx.Configuration;

using HarmonyLib;

using MonoMod.Utils;

using Newtonsoft.Json;

using Smol_Randomizer.Settings;

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

    private protected override void ApplySaveData(Dictionary<string, object> savedData)
    {
        if (savedData.TryGetValue(nameof(enemyHealthNumbers), out object tempDict))
            enemyHealthNumbers.AddRange(JsonConvert.DeserializeObject<Dictionary<string, int>>(tempDict.ToString()));
        if (savedData.TryGetValue(nameof(sceneHealthNumbers), out tempDict))
            sceneHealthNumbers.AddRange(JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, int>>>(tempDict.ToString()));
    }

    private protected override void SetSaveData(Dictionary<string, object> savedData)
    {
        savedData[nameof(enemyHealthNumbers)] = enemyHealthNumbers;
        savedData[nameof(sceneHealthNumbers)] = sceneHealthNumbers;
    }

    // Unneeded for this Randomizer
    private protected override void OnSettingsSaved() { }

    /// <summary>
    /// Patch HealthManager.OnEnable on game startup
    /// </summary>
    private void GameStartup()
    {
        CuteRandoCore.harmony.Patch(
            AccessTools.Method(typeof(HealthManager), "OnEnable"),
            postfix: new HarmonyMethod(typeof(Enemy_Health_Rando), nameof(HealthManagerOnEnablePostfix)));
    }

    /// <summary>
    /// On First Frame, clean the active health manager list. This is done on the first frame since some health managers get added before the scene is loaded, and some afterwards.
    /// </summary>
    private void OnFirstSceneFrame()
    {
        CleanCurrentHealthManagerList();
    }

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
                    hp = initHp = tempHp;
                else
                    enemyHealthNumbers.Add(name, RandomizeHp(boss, ref initHp, ref hp, CuteRandoCore.RNGSeed(name)));

                break;
            case RandomizerConsistencyA.Scene:
                if (sceneHealthNumbers.TryGetValue(operatingScene, out Dictionary<string, int> healthManagerSet))
                {
                    if (healthManagerSet.TryGetValue(name, out tempHp))
                        hp = initHp = tempHp;
                    else
                        healthManagerSet[name] = RandomizeHp(boss, ref initHp, ref hp, CuteRandoCore.RNGSeed(name + operatingScene));
                }
                else
                    sceneHealthNumbers[operatingScene] = new() { { name, RandomizeHp(boss, ref initHp, ref hp, CuteRandoCore.RNGSeed(name + operatingScene)) } };

                break;
            case RandomizerConsistencyA.None:
                RandomizeHp(boss, ref initHp, ref hp);
                break;
            default:
                throw new NotImplementedException();
        }

        // Helper to randomize hp
        int RandomizeHp(bool boss, ref int initHp, ref int hp, int seed = int.MinValue)
        {
            float randFloat = CuteRandoCore.RandoHelper(boss ? BossHealthPercentRange.AsTuple() : EnemyHealthPercentRange.AsTuple(), seed);
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
    private void CleanCurrentHealthManagerList()
    {
        currentEnemyHealthManagers.RemoveWhere(x => x == null);
    }

    private protected override void ResetAllLists()
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
    public readonly RandomizerConsistencyA defaultRandomizerConsistency = RandomizerConsistencyA.None;
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

    // Used for determining if we need to update
    private RandomizerEnemyTypeFlags currentHealthRandomizerSetting;

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

        currentHealthRandomizerSetting = enemyHealthRandomizerSetting.Value;

        enemyHealthRandomizerSetting.SettingChanged += OnSettingsUpdated;
        enemyHealthPercentRange.SettingChanged += OnSettingsUpdated;
        bossHealthPercentRange.SettingChanged += OnSettingsUpdated;

        enemyHealthRandomizerSetting.SettingChanged += SettingMenu.OnRandomizerEnable;
        SettingMenu.UpdateSubMenuColor(enemyHealthRandomizerSetting);
    }

    private protected override void OnSettingsUpdated(object sender, EventArgs args)
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

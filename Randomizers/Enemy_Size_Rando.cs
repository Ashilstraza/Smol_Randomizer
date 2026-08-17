using System;
using System.Collections.Generic;
using System.Linq;

using BepInEx.Configuration;

using HarmonyLib;

using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;

#if TESTING
using MonoMod.Utils;

using Newtonsoft.Json;
#endif

using Smol_Randomizer.Patchers;
using Smol_Randomizer.Settings;

using UnityEngine;
using UnityEngine.SceneManagement;

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

    protected override void Register()
    {
        CuteRandoCore.RegisterRandomizer(eventActiveEnemy);
        CuteRandoCore.RegisterRandomizer(eventOnFirstSceneFrame);

        FSMPatcher.enemyStateAction.RegisterPatchSets(enemyStateActionPatchSets);
        FSMPatcher.enemyState.RegisterPatchSets(enemyStatePatchSets);
        ObjectPatcher.enemyObject.RegisterPatchSets(enemyObjectPatches);
    }

    protected override void Unregister()
    {
        CuteRandoCore.UnregisterRandomizer(eventActiveEnemy);
        CuteRandoCore.UnregisterRandomizer(eventOnFirstSceneFrame);

        FSMPatcher.enemyStateAction.UnregisterPatchSets(enemyStateActionPatchSets);
        FSMPatcher.enemyState.UnregisterPatchSets(enemyStatePatchSets);
        ObjectPatcher.enemyObject.UnregisterPatchSets(enemyObjectPatches);
    }

    // Unused as we don't need
    protected override void OnLoaded() { }
    protected override void OnUnload() { }

#if TESTING // Enable Saving Data
    protected override void ApplySaveData(Dictionary<string, object> savedData)
    {
        if (savedData.TryGetValue(nameof(enemySizes), out object tempDict))
            enemySizes.AddRange(JsonConvert.DeserializeObject<Dictionary<string, float>>(tempDict.ToString()));
        if (savedData.TryGetValue(nameof(sceneEnemySizes), out tempDict))
            sceneEnemySizes.AddRange(JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, float>>>(tempDict.ToString()));
    }

    protected override void SetSaveData(Dictionary<string, object> savedData)
    {
        savedData[nameof(enemySizes)] = enemySizes;
        savedData[nameof(sceneEnemySizes)] = sceneEnemySizes;
    }

    // Unneeded for this Randomizer
    protected override void OnSettingsSaved() { }
#endif

    /// <summary>
    /// Patch HealthManager.OnEnable on game startup
    /// </summary>
    private void GameStartup()
    {
        CuteRandoCore.harmony.Patch(AccessTools.Method(
            typeof(SetScale), "DoSetScale"),
            prefix: new HarmonyMethod(typeof(Enemy_Size_Rando), nameof(SetScale_DoSetScale_Prefix)));
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
    /// Patch that applies the correct x scale on enemies' SetScale FSM Action
    /// </summary>
    /// <param name="__instance">The SetScale Action of the enemy</param>
    /// <returns>always true to continue processing SetScale</returns>
    private static bool SetScale_DoSetScale_Prefix(ref SetScale __instance)
    {
        if (__instance.x.Value is -1f or 1f)
        {
            if (__instance.Owner != null && Instance.currentEnemyHealthManagers.Contains(__instance.Owner.GetComponent<HealthManager>()))
            {
                __instance.x = __instance.Owner.transform.GetScaleX();
            }
        }
        if (__instance.y.Value is -1f or 1f)
        {
            if (__instance.Owner != null && Instance.currentEnemyHealthManagers.Contains(__instance.Owner.GetComponent<HealthManager>()))
            {
                __instance.y = __instance.Owner.transform.GetScaleY();
            }
        }
        return true;
    }

    /// <summary>
    /// Dictionary containing various enemy state action FSM patches
    /// </summary>
    private static readonly Dictionary<string, FSMStateActionPatchSet> enemyStateActionPatchSets = new()
    {
        {
            "Crowman",
            new FSMStateActionPatchSet("Crowman",
                [new("Start Rest",
                    typeof(RandomFloatEither),
                    delegate (FsmStateAction action, object[]? extra)
                    {
                        if(extra?.Length != 1) return;
                        Transform transform = ((GameObject)extra[0]).transform;
                        RandomFloatEither rfe = (RandomFloatEither)action;

                        rfe.value1.Value = rfe.value1.Value > 0
                            ? transform.localScale.x
                            : transform.localScale.x * -1;
                        rfe.value2.Value = rfe.value2.Value > 0
                            ? transform.localScale.x
                            : transform.localScale.x * -1; ;
                    })
                ])
        },
        {
            "Farmer Scissors",
            new FSMStateActionPatchSet("Farmer Scissors",
                [new("Do Step",
                    typeof(RayCast2dV2),
                    delegate (FsmStateAction action, object[]? extra)
                    {
                        if(extra?.Length != 1) return;
                        ((RayCast2dV2)action).distance.Value *= Math.Abs(((GameObject)extra[0]).transform.GetScaleX());
                    })
                ])
        },
        {
            "Farmer Centipede",
            new FSMStateActionPatchSet("Farmer Centipede",
                [new("Idle Chase",
                    typeof(DistanceWalk),
                    delegate (FsmStateAction action, object[]? extra)
                    {
                        if(extra?.Length != 1) return;
                        DistanceWalk dw = (DistanceWalk)action;
                        dw.distance.Value *= Math.Abs(((GameObject)extra[0]).transform.GetScaleX());
                    })
                ])
        },
        {
            "Dustroach",
            new FSMStateActionPatchSet("Dustroach",
                [new("ScrabbleJump Air",
                    typeof(RayCast2dV2),
                    delegate (FsmStateAction action, object[]? extra){
                        if(extra?.Length != 1) return;
                        ((RayCast2dV2)action).distance.Value *= Math.Abs(((GameObject)extra[0]).transform.GetScaleX());
                    })
                ])
        }
    };

    /// <summary>
    /// Action to reduce the target height of Bone Worms' dive
    /// </summary>
    private static ShiftDownByHalfHeight wormAdjust;

    /// <summary>
    /// Dictionary containing various patches that add actions to enemy states.
    /// </summary>
    private static readonly Dictionary<string, FSMStatePatchSet> enemyStatePatchSets = new()
    {
        {
            "Bone Worm",
            new FSMStatePatchSet("Bone Worm",
                [new("Position",
                    delegate(FsmState state, object[]? extra){
                        wormAdjust = new()
                        {
                            gameObject = new()
                            {
                               GameObject = state.Fsm.Owner.gameObject
                            },
                            variableName = "Ground Y"
                        };

                         state.Actions = state.Actions.AddItem(wormAdjust).ToArray();
                    },
                    delegate(FsmState state, object[]? extra){
                        if(wormAdjust ==  null) return;

                        state.Actions = state.Actions.Except([wormAdjust]).ToArray();
                    })
                ])
        }
    };

    /// <summary>
    /// Dictionary containing various enemy non-FSM patches
    /// </summary>
    private static readonly Dictionary<string, ObjectPatch> enemyObjectPatches = new()
    {
        {
            "Farmer Centipede",
            new("Farmer Centipede",
                delegate(GameObject obj, object[]? extra)
                {
                    float multiplier = obj.transform.GetScaleY();
                    BoxCollider2D battleRange = obj.GetComponentsInChildren<BoxCollider2D>().ToList().FirstOrDefault(collider => collider.name == "Battle Range");
                    battleRange.size = battleRange.size with {y =  battleRange.size.y * 1/multiplier};
                })
        },
        {
            "Dustroach",
            new("Dustroach",
                delegate(GameObject obj, object[]? extra)
                {
                    float multiplier = obj.transform.GetScaleY();
                    List<Transform> allTransform = obj.GetComponentsInChildren<Transform>().ToList();
                    foreach(var transform in allTransform)
                    {
                        if(transform.name is "Force Attack Range" or "Attack Range" or "Above Range")
                            transform.localScale = transform.localScale * 1/multiplier;
                    }

                })
        }
    };

    /// <summary>
    /// Updates an enemy with a new size
    /// </summary>
    /// <param name="thing">The health manager of the enemy we want to change</param>
    /// <exception cref="NotImplementedException">Thrown if there is an unimplemented randomizer type.</exception>
    private void SetSize(HealthManager thing)
    {
        if (thing.transform == null || !Instance.currentEnemyHealthManagers.Add(thing)) return;

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

        float RandomizeSize(bool boss, Transform transform, Walker walker, int seed = int.MinValue)
        {
            float multiplier = CuteRandoCore.RandomFloat(boss ? BossSizePercentRange.AsTuple() : EnemySizePercentRange.AsTuple(), seed);
            ApplySize(transform, multiplier, walker);
            return multiplier;
        }

        void ApplySize(Transform transform, float multiplier, Walker walker)
        {
            transform.localScale *= multiplier;

            if (walker != null)
            {
                Traverse rightScale = CuteRandoCore.TraverseCreator(walker, "rightScale");
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

    protected override void ResetAllLists()
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

    protected override void InitSettings()
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

    protected override void OnSettingsUpdated(object sender, EventArgs args)
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

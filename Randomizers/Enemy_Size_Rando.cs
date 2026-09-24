using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;

using BepInEx.Configuration;

using HarmonyLib;

using HutongGames.PlayMaker.Actions;

#if TESTING

using MonoMod.Utils;

using Newtonsoft.Json;

using Smol_Randomizer.DebugDrawing;

#endif

using Smol_Randomizer.Patchers.Enemy;
using Smol_Randomizer.Settings;

using UnityEngine;
using UnityEngine.SceneManagement;

namespace Smol_Randomizer.Randomizers;

/// <summary>Randomizer for Enemy Sizes</summary>
internal partial class Enemy_Size_Rando : Rando_Base
{
    /// <summary>We make a singleton of this rando</summary>
    private static readonly Lazy<Enemy_Size_Rando> instance = new(() => new Enemy_Size_Rando());

    /// <summary>Externally visible instance of this rando</summary>
    public static Enemy_Size_Rando Instance => instance.Value;

    /// <summary>Dictionary of the enemy sizes in current game instance</summary>
    private readonly Dictionary<string, float> enemySizes = [];

    /// <summary>Dictionary of the scene enemy sizes in current game instance</summary>
    private readonly Dictionary<string, Dictionary<string, float>> sceneEnemySizes = [];

    /// <summary>Set of enemies that we have touched in this scene</summary>
    private readonly HashSet<HealthManager> currentEnemyHealthManagers = [];

    public readonly HashSet<string> excludedEnemies = ["Splinter Queen Spike"];

    #region Randomizer_Info

    /// <summary>Used for registering this randomizer in the core for when enemies activate</summary>
    private Randomizer_Info eventActiveEnemy;

    /// <summary>Used for registering this randomizer in the core for when a scene loads</summary>
    private Randomizer_Info eventOnFirstSceneFrame;

    #endregion Randomizer_Info

    /// <summary>Constructor for this singleton</summary>
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

        // Located in Enemy_Size_Rando_Patches
        EnemyFSMPatches.RegisterPatchCollection(enemyFSMPatches);
        EnemyObjectPatchCollection.Instance.RegisterPatchCollection(enemyObjectPatches);
    }

    protected override void Unregister()
    {
        CuteRandoCore.UnregisterRandomizer(eventActiveEnemy);
        CuteRandoCore.UnregisterRandomizer(eventOnFirstSceneFrame);

        // Located in Enemy_Size_Rando_Patches
        EnemyFSMPatches.UnregisterPatchCollection(enemyFSMPatches);
        EnemyObjectPatchCollection.Instance.UnregisterPatchCollection(enemyObjectPatches);
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

    /// <summary>Patch HealthManager.OnEnable on game startup</summary>
    private void GameStartup()
    {
        CuteRandoCore.harmony.Patch(AccessTools.Method(
            typeof(SetScale), "DoSetScale"),
            prefix: new HarmonyMethod(typeof(Enemy_Size_Rando), nameof(SetScale_DoSetScale_Prefix)));
#if !TESTING // Disable if we are testing
        CuteRandoCore.harmony.Patch(AccessTools.Method(
            typeof(Crawler), "ShouldTurn"),
            transpiler: new HarmonyMethod(typeof(Enemy_Size_Rando), nameof(Crawler_ShouldTurn_Transpiler)));
#else
        CuteRandoCore.harmony.Patch(AccessTools.Method(
            typeof(Crawler), "ShouldTurn"),
            postfix: new HarmonyMethod(typeof(Enemy_Size_Rando), nameof(Crawler_ShouldTurn_Postfix)));
#endif
    }

    /// <summary>
    /// On First Frame, clean the active health manager list. This is done on the first frame since some health managers
    /// get added before the scene is loaded, and some afterwards.
    /// </summary>
    /// <param name="scene">The scene we are in</param>
    private void OnFirstSceneFrame(Scene scene)
    {
        CleanCurrentHealthManagerList();
    }

    #region Crawler ShouldTurn()

#if TESTING

    private static void Crawler_ShouldTurn_Postfix(ref Crawler __instance,
                                                    ref bool drawGizmos,
                                                    ref bool ___isTurnScheduled,
                                                    ref BoxCollider2D ___collider,
                                                    ref float ___rayFrontDistance,
                                                    ref bool ___spriteFacesRight,
                                                    ref Vector2 ___velocity,
                                                    ref float ___rayDownFrontPadding,
                                                    ref bool __result)
    {
        if (___isTurnScheduled)
        {
            ___isTurnScheduled = false;
            __result = true;
            return;
        }
        Vector2 offset = ___collider.offset;
        Vector2 size = ___collider.size / 2f;
        int direction = (___spriteFacesRight ? 1 : (-1));
        Vector2 back_bottom = offset - size.MultiplyElements(direction);
        Vector2 front_top = offset + size.MultiplyElements(direction);
        Vector2 front_bottom = new Vector2(front_top.x - 0.1f * (float)direction, back_bottom.y + 0.01f);
        Vector2 front = Vector2.right.MultiplyElements(direction);
        float frontDistanceTimeScaled = Mathf.Max(___rayFrontDistance, Mathf.Abs(___velocity.x * Time.deltaTime)) + 0.1f;
        float y = back_bottom.y + 0.5f + 0.1f;
        Vector2 paddedFront_Down = new Vector2(front_top.x + ___rayDownFrontPadding * (float)direction, y);
        Vector2 back_middle = new Vector2(back_bottom.x, y);
        Vector2 down = Vector2.down;
        float length = ShouldTurnLengthCheck(Math.Abs(__instance.transform.localScale.y));

        DebugDrawer.Square(__instance.transform, offset, ___collider.size);
        DebugDrawer.Circle(__instance.transform, back_bottom, 0.1f, __instance.transform.rotation, Color.red);
        DebugDrawer.Circle(__instance.transform, front_top, 0.1f, __instance.transform.rotation, Color.yellow);
        DebugDrawer.Circle(__instance.transform, front_bottom, 0.1f, __instance.transform.rotation, Color.green);
        DebugDrawer.Circle(__instance.transform, front, 0.1f, __instance.transform.rotation);
        DebugDrawer.Circle(__instance.transform, paddedFront_Down, 0.1f, __instance.transform.rotation, Color.cyan);
        DebugDrawer.Circle(__instance.transform, back_middle, 0.1f, __instance.transform.rotation, Color.blue);
        DebugDrawer.Circle(__instance.transform, down, 0.1f, __instance.transform.rotation);

        bool airborne = !__instance.IsRayHittingLocal(back_middle, down, length);
        DebugDrawer.Line(__instance.transform,
                    back_middle,
                    back_middle + down * length,
                    airborne ? Color.cyan : Color.clear);
        if (airborne)
        {
            __result = false;
            return;
        }
        bool wallCheck = __instance.IsRayHittingLocal(front_bottom, front, frontDistanceTimeScaled);
        DebugDrawer.Line(__instance.transform,
                    front_bottom,
                    front_bottom + front * frontDistanceTimeScaled,
                    wallCheck ? Color.green : Color.red);
        if (wallCheck)
        {
            __result = true;
            return;
        }
        bool ledgeCheck = !__instance.IsRayHittingLocal(paddedFront_Down, down, length);
        DebugDrawer.Line(__instance.transform,
                    paddedFront_Down,
                    paddedFront_Down + down * length,
                    ledgeCheck ? Color.green : Color.red);
        if (ledgeCheck)
        {
            __result = true;
            return;
        }
        __result = false;
        return;
    }

#endif

    /// <summary>Scales Length check differently depending on if it is smaller or larger than 1</summary>
    /// <param name="scale">Scale of the enemy</param>
    /// <returns>Scaled length</returns>
    private static float ShouldTurnLengthCheck(float scale)
    {
        if (scale >= 1)
            return 1.1f * scale;

        return scale / 1.5f + 0.43333f;
    }

    private static IEnumerable<CodeInstruction> Crawler_ShouldTurn_Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator ilGenerator)
    {
        var getComponent = AccessTools.Method(typeof(Component), nameof(Component.GetComponent), generics: [typeof(Transform)]);
        var mathAbs = AccessTools.Method(typeof(Math), nameof(Math.Abs), parameters: [typeof(float)]);
        var shouldTurnLengthCheck = AccessTools.Method(typeof(Enemy_Size_Rando), nameof(ShouldTurnLengthCheck));
        var getLocalScale = AccessTools.Method(typeof(Transform), "get_localScale");
        var getLocalScaleY = AccessTools.Field(typeof(Vector3), "y");

        List<CodeInstruction> instructionList = [.. instructions];

        List<CodeInstruction> scaleCheckLength =
            [
                new(OpCodes.Ldarg_0), // this
                new(OpCodes.Call, getComponent), // .GetComponent<Transform>()
                new(OpCodes.Callvirt, getLocalScale), // .localScale
                new(OpCodes.Ldfld, getLocalScaleY), // .y
                new(OpCodes.Call, mathAbs), // Math.Abs(y)
                new(OpCodes.Call, shouldTurnLengthCheck) // ShouldTurnLengthCheck(Math.Abs(this.transform.localScale.y)
            ];

        for (int i = 0; i < instructionList.Count; i++)
        {
            CodeInstruction instruction = instructionList[i];

            if (instructionList[i].OperandIs(1.1f))
            {
                foreach (CodeInstruction codeInstruction in scaleCheckLength)
                {
                    yield return codeInstruction;
                }

                continue;
            }

            yield return instruction;
        }
    }

    #endregion Crawler ShouldTurn()

    /// <summary>Patch that applies the correct x scale on enemies' SetScale FSM Action</summary>
    /// <param name="__instance">The SetScale Action of the enemy</param>
    /// <returns>always true to continue processing SetScale</returns>
    private static bool SetScale_DoSetScale_Prefix(ref SetScale __instance)
    {
        if (__instance.Owner != null && Instance.currentEnemyHealthManagers.Contains(__instance.Owner.GetComponent<HealthManager>()))
        {
            __instance.x = __instance.Owner.transform.GetScaleX();
            __instance.y = __instance.Owner.transform.GetScaleY();
        }

        return true;
    }

    private static bool resizing = false;

    private void UpdateCurrentEnemies()
    {
        CleanCurrentHealthManagerList();
        resizing = true;
        foreach (var enemy in currentEnemyHealthManagers)
        {
            SetSize(enemy);
        }
        resizing = false;
    }

    private static Dictionary<string, string[]> ignoredChildAdjusts = new()
    {
        { "Bone Roller", ["All"] },
        { "Rosary Pilgrim", ["Leap Range", "Attack Range" ] },
        { "Bloom Puncher", ["Alert Range"] },
        { "Farmer Scissors", ["Close Range"] },
        { "Farmer Catcher", ["Attack Range", "Alert Range"] },
        {"Crowman", ["Slash Range"] },
        {"Roachfeeder Tall", ["Attack Range", "Evade Range"] }
    };

    private static Dictionary<string, bool[]> childAdjustsOptions = new()
    {
        { "Roof Crab", [true] }
    };

    private static string[] extraChildAdjusts =
        ["Route", // Aspid wanderings
        "Start Point",
        "Patrol Point",
        "Move Target", // Bell Fly
        "Throw Point",
        "b",
        "c"
        ];

    /// <summary>Fixes any and all Alert style Ranges to be their default pre-scaled size.</summary>
    /// <param name="obj"></param>
    public static void AdjustChildren(GameObject obj, string name)
    {
        if (ignoredChildAdjusts.ContainsKey(name) && ignoredChildAdjusts[name].Contains("All")) return;

        foreach (Transform child in obj.transform)
        {
            if (child.name.Contains(" Range"))
                Adjust(child);
            else if (extraChildAdjusts.Contains(child.name))
                Adjust(child);
        }

        // TODO: Reverse stuff
        void Adjust(Transform target)
        {
            if (target == null || (ignoredChildAdjusts.ContainsKey(name) && ignoredChildAdjusts[name].Contains(target.name))) return;

#if TESTING
            CuteRandoCore.Log.LogInfo($"Patching {target.name} for {obj.name} in scene {obj.scene.name}");
#endif
            bool reverseX = false;
            bool reverseY = false;
            bool alternateNullColliderScale = false;

            if (target.name.Equals("Throw Point"))
                alternateNullColliderScale = true;

            if (childAdjustsOptions.ContainsKey(name))
            {
                reverseX = childAdjustsOptions[name].ElementAtOrDefault(0);
                reverseY = childAdjustsOptions[name].ElementAtOrDefault(1);
                alternateNullColliderScale = childAdjustsOptions[name].ElementAtOrDefault(3);
            }

            float parentYScale = obj.transform.localScale.y;
            float parentXScale = obj.transform.localScale.x;
            double parentRotation = Math.PI * obj.transform.rotation.eulerAngles.z / 180;

            bool negX = target.localScale.x < 0;
            bool negY = target.localScale.y < 0;

            Vector2 localPosition = target.localPosition;
            Collider2D collider = target.GetComponent<Collider2D>();

            if (resizing)
                target.localScale = new(
                    target.localScale.x / Math.Abs(target.localScale.x),
                    target.localScale.y / Math.Abs(target.localScale.y));

            target.localScale = new((Math.Abs(target.lossyScale.x) * (negX ? -1 : 1)) / (parentXScale * parentXScale), (Math.Abs(target.lossyScale.y) * (negY ? -1 : 1)) / (parentYScale * parentYScale));

            if (collider == null)
            {
                if (!alternateNullColliderScale)
                    target.position = new(ScalePosition(target.position.x, obj.transform.position.x, target.localScale.x),
                                        ScalePosition(target.position.y, obj.transform.position.y, target.localScale.y),
                                        ScalePosition(target.position.z, obj.transform.position.z, target.localScale.z));
                else
                    target.position = new(target.position.x * parentXScale, target.position.y * parentYScale);
#if TESTING
                var debugCollider = target.gameObject.AddComponent<CircleCollider2D>();
                debugCollider.radius = 0.1f;
                debugCollider.isTrigger = true;
                debugCollider.enabled = true;
#endif
                return;

                float ScalePosition(float child, float parent, float scale)
                {
                    return (child - parent) * Math.Abs(scale) + parent;
                }
            }

            if ((localPosition.x.IsWithinTolerance(0.1f, 0f) && localPosition.y.IsWithinTolerance(0.1f, 0f))) return; // transform is located at 0,0 on the object, don't bother doing more math

            double cosParentRotation = Math.Cos(parentRotation);
            double sinParentRotation = Math.Sin(parentRotation);

            double rotation = Math.PI * target.rotation.eulerAngles.z / 180;

            float newX = AdjustChildPosition(
                localPosition.x,
                collider.offset.x,
                Math.Abs(parentXScale));
            float newY = AdjustChildPosition(
                localPosition.y,
                collider.offset.y,
                Math.Abs(parentYScale));

            target.localPosition = new(newX, newY);
        }
    }

    public static float AdjustChildPosition(
        float position,
        float offset,
        float scale)
    {
        var correctScale = scale / (scale * scale);
        var a = position - offset;
        var b = Math.Abs(correctScale) * a;
        var c = b + (offset * Math.Abs(correctScale));

        return (float)c;
    }

    /// <summary>Updates an enemy with a new size</summary>
    /// <param name="thing">The health manager of the enemy we want to change</param>
    /// <exception cref="NotImplementedException">Thrown if there is an unimplemented randomizer type.</exception>
    private void SetSize(HealthManager thing)
    {
        if (thing?.transform == null || (!Instance.currentEnemyHealthManagers.Add(thing) && !resizing)) return;

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

        int cullIndex = name.IndexOf('(');
        if (cullIndex > 0) name = name[..cullIndex].TrimEnd();

        if (Instance.excludedEnemies.Contains(name))
            return;

        if (resizing && enemySizeRandomizerSetting.Value == RandomizerEnemyTypeFlags.None && Math.Abs(thing.transform.localScale.x) < 1)
        {
            ApplySize(thingTransform, 1, walker);
            goto afterSwitch;
        }

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
    afterSwitch:

        AdjustChildren(thing.gameObject, name);

        // local methods
        float RandomizeSize(bool boss, Transform transform, Walker walker, int seed = int.MinValue)
        {
            float multiplier = CuteRandoCore.RandomFloat(boss ? BossSizePercentRange.AsTuple() : EnemySizePercentRange.AsTuple(), seed);
            ApplySize(transform, multiplier, walker);
            return multiplier;
        }

        void ApplySize(Transform transform, float multiplier, Walker walker)
        {
            if (resizing)
                transform.localScale = new(transform.localScale.x / Math.Abs(transform.localScale.x), transform.localScale.y / Math.Abs(transform.localScale.y));
            transform.localScale *= multiplier;

            if (walker != null)
            {
                Traverse rightScale = CuteRandoCore.TraverseCreator(walker, "rightScale");
                int direction = (float)rightScale.GetValue() < 0 ? -1 : 1;
                rightScale.SetValue(Math.Abs(transform.localScale.x) * direction);
            }
        }
    }

    /// <summary>Cleans the current scene's HealthManager list</summary>
    private void CleanCurrentHealthManagerList()
    {
        currentEnemyHealthManagers.RemoveWhere(x => x == null);
    }

    protected override void ResetAllLists()
    {
        //currentEnemyHealthManagers.Clear();
        enemySizes.Clear();
        sceneEnemySizes.Clear();
    }

    #region Settings

    /// <summary>Setting for how consistent the enemy size should be</summary>
    public RandomizerConsistencyA RandomizerConsistency
    {
        get => randomizerConsistency.Value;
        internal set => randomizerConsistency.Value = value;
    }

    private ConfigEntry<RandomizerConsistencyA> randomizerConsistency;

    /// <summary>Default setting for how consistent the enemy size should be</summary>
    public const RandomizerConsistencyA defaultRandomizerConsistency = RandomizerConsistencyA.None;

    /// <summary>Randomize the size of enemies</summary>
    public RandomizerEnemyTypeFlags EnemySizeRandomizerSetting
    {
        get => enemySizeRandomizerSetting.Value;
        internal set => enemySizeRandomizerSetting.Value = value;
    }

    private ConfigEntry<RandomizerEnemyTypeFlags> enemySizeRandomizerSetting;

    /// <summary>Default choice for the size randomizer</summary>
    public const RandomizerEnemyTypeFlags defaultEnemySizeRandomizerSetting = RandomizerEnemyTypeFlags.None;

    /// <summary>Randomize the size of normal enemies</summary>
    public FloatRange EnemySizePercentRange
    {
        get => enemySizePercentRange.Value;
        internal set => enemySizePercentRange.Value = value;
    }

    private ConfigEntry<FloatRange> enemySizePercentRange;

    /// <summary>Default choice for enemy size randomizer</summary>
    public static readonly FloatRange defaultEnemySizePercentRange = new(0.35f, 1.6f);

    /// <summary>Randomize the size of boss enemies</summary>
    public FloatRange BossSizePercentRange
    {
        get => bossSizePercentRange.Value;
        internal set => bossSizePercentRange.Value = value;
    }

    private ConfigEntry<FloatRange> bossSizePercentRange;

    /// <summary>Default choice for boss size randomizer</summary>
    public static readonly FloatRange defaultBossSizePercentRange = new(0.85f, 1.25f);

    /// <summary>Acceptable value range for the enemy sizes</summary>
    public static AcceptableRangeforFloatRange acceptableEnemySizeRange = new(0.25f, 2f);

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
                acceptableEnemySizeRange,
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
                acceptableEnemySizeRange,
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

        if (!enemySizeRandomizerSetting.Value.Equals(RandomizerEnemyTypeFlags.None))
        {
            Register();
        }
    }

    protected override void OnSettingsUpdated(object sender, EventArgs args)
    {
        if (((SettingChangedEventArgs)args).ChangedSetting.BoxedValue is RandomizerEnemyTypeFlags ehr && !ehr.Equals(currentSizeRandomizerSetting))
        {
            if (ehr.Equals(RandomizerEnemyTypeFlags.None) && !currentSizeRandomizerSetting.Equals(RandomizerEnemyTypeFlags.None))
            {
                Unregister();
            }

            if (currentSizeRandomizerSetting.Equals(RandomizerEnemyTypeFlags.None) && !ehr.Equals(RandomizerEnemyTypeFlags.None))
            {
                Register();
            }

            currentSizeRandomizerSetting = ehr;
        }

#if TESTING
        UpdateCurrentEnemies();
#endif

        ResetAllLists();
    }

    #endregion Settings
}
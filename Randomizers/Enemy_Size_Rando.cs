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

using DebugDrawing;

#endif

using Smol_Randomizer.Patchers.Enemy;
using Smol_Randomizer.Settings;

using UnityEngine;
using UnityEngine.SceneManagement;

using HutongGames.PlayMaker;

using Smol_Randomizer.Patchers.FSM_Actions;

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

    /// <summary>Dictionary of the <see cref="Scene" /> enemy sizes in current game instance</summary>
    private readonly Dictionary<string, Dictionary<string, float>> sceneEnemySizes = [];

    /// <summary>Set of enemies that we have touched in this <see cref="Scene" /></summary>
    private readonly Dictionary<HealthManager, Vector3> currentEnemyHealthManagers = [];

    public readonly HashSet<string> excludedEnemies = [ "Splinter Queen Spike", // Looks bad in boss arena
                                                        "Swamp Barnacle", // TODO Figure it out
                                                        "Centipede Trap"]; // No Point to scale

    #region Randomizer_Info

    /// <summary>Used for registering this randomizer in the core for when enemies activate</summary>
    private Randomizer_Info eventActiveEnemy;

    /// <summary>Used for registering this randomizer in the core for when a <see cref="Scene" /> loads</summary>
    private Randomizer_Info eventOnFirstSceneFrame;

    private Randomizer_Info eventOnFsmStart;

    #endregion Randomizer_Info

    /// <summary>Constructor for this singleton</summary>
    private Enemy_Size_Rando()
        => InitRandomizer();

    protected override void InitRandomizer()
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
        eventOnFsmStart = new(
            RandomizerName,
            RandomizerEventType.FSM_Start,
            AccessTools.Method(
                typeof(Enemy_Size_Rando),
                nameof(OnFSMStart)),
            this);

        base.InitRandomizer();
    }

    protected override void Register()
    {
        CuteRandoCore.RegisterRandomizer(eventActiveEnemy);
        CuteRandoCore.RegisterRandomizer(eventOnFirstSceneFrame);
        CuteRandoCore.RegisterRandomizer(eventOnFsmStart);

        // Located in Enemy_Size_Rando_Patches
        EnemyFSMPatches.RegisterPatchCollection(enemyFSMPatches);
        EnemyObjectPatchCollection.Instance.RegisterPatchCollection(enemyObjectPatches);
    }

    protected override void Unregister()
    {
        CuteRandoCore.UnregisterRandomizer(eventActiveEnemy);
        CuteRandoCore.UnregisterRandomizer(eventOnFirstSceneFrame);
        CuteRandoCore.UnregisterRandomizer(eventOnFsmStart);

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

    /// <summary>Patch <see cref="HealthManager.OnEnable" /> on game startup</summary>
    private void GameStartup()
    {
        CuteRandoCore.harmony.Patch(AccessTools.Method(
            typeof(SetScale), "DoSetScale"),
            prefix: new HarmonyMethod(typeof(Enemy_Size_Rando), nameof(SetScale_DoSetScale_Prefix)));
        CuteRandoCore.harmony.Patch(AccessTools.Method(
            typeof(SetScale), "DoSetScale"),
            postfix: new HarmonyMethod(typeof(Enemy_Size_Rando), nameof(SetScale_DoSetScale_Postfix)));
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
    /// On First Frame, clean the active <see cref="HealthManager" /> list. This is done on the first frame since some
    /// <see cref="HealthManager" /> s get added before the <see cref="Scene" /> is loaded, and some afterwards.
    /// </summary>
    /// <param name="scene">The <see cref="Scene" /> we are in</param>
    private void OnFirstSceneFrame(Scene scene)
        => CleanCurrentLists();

    #region Crawler ShouldTurn()

#if TESTING

    /// <summary>Postfix of <see cref="Crawler" />.ShouldTurn() to adjust the length check of the rays</summary>
    /// <param name="__instance">            The instance of <see cref="Crawler" />.</param>
    /// <param name="drawGizmos">            <see langword="true" /> if the gismos should be drawn</param>
    /// <param name="___isTurnScheduled">    Private Variable: <see langword="true" /> if the enemy wants to turn anyway</param>
    /// <param name="___collider">           Private Variable: <see cref="BoxCollider2D" /> fo the enemy</param>
    /// <param name="___rayFrontDistance">   Private Variable: The distance the front facing ray should check</param>
    /// <param name="___spriteFacesRight">   
    /// Private Variable: <see langword="true" /> if the enemy is facing right, <see langword="false" /> if facing left
    /// </param>
    /// <param name="___velocity">           Private Variable: <see cref="Vector2" /> velocity of the enemy</param>
    /// <param name="___rayDownFrontPadding">Private Variable: Offsets the x value of the front down ray</param>
    /// <param name="__result">              
    /// Return Variable: <see langword="true" /> if the enemy should turn, <see langword="false" /> otherwise
    /// </param>
    private static void Crawler_ShouldTurn_Postfix(Crawler __instance,
                                                    bool drawGizmos,
                                                    BoxCollider2D ___collider,
                                                    float ___rayFrontDistance,
                                                    bool ___spriteFacesRight,
                                                    Vector2 ___velocity,
                                                    float ___rayDownFrontPadding,
                                                    ref bool __result)
    {
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
        float length = ShouldTurnLengthCheck(__instance.transform.GetAbsScaleY());

#if TESTING
        /*Primitives.Square(__instance.transform, offset, ___collider.size, oneFrame: true);
        Primitives.Circle(__instance.transform, back_bottom, 0.1f, __instance.transform.rotation, Color.red, oneFrame: true);
        Primitives.Circle(__instance.transform, front_top, 0.1f, __instance.transform.rotation, Color.yellow, oneFrame: true);
        Primitives.Circle(__instance.transform, front_bottom, 0.1f, __instance.transform.rotation, Color.green, oneFrame: true);
        Primitives.Circle(__instance.transform, front, 0.1f, __instance.transform.rotation, oneFrame: true);
        Primitives.Circle(__instance.transform, paddedFront_Down, 0.1f, __instance.transform.rotation, Color.cyan, oneFrame: true);
        Primitives.Circle(__instance.transform, back_middle, 0.1f, __instance.transform.rotation, Color.blue, oneFrame: true);
        Primitives.Circle(__instance.transform, down, 0.1f, __instance.transform.rotation, oneFrame: true);*/
#endif

        bool airborne = !__instance.IsRayHittingLocal(back_middle, down, length);
#if TESTING
        Primitives.Line(__instance.transform,
                    back_middle,
                    back_middle + down * length,
                    airborne ? Color.cyan : Color.clear,
                    oneFrame: true);
#endif
        if (airborne)
        {
            __result = false;
            return;
        }
        bool wallCheck = __instance.IsRayHittingLocal(front_bottom, front, frontDistanceTimeScaled);
#if TESTING
        Primitives.Line(__instance.transform,
                    front_bottom,
                    front_bottom + front * frontDistanceTimeScaled,
                    wallCheck ? Color.green : Color.red,
                    oneFrame: true);
#endif
        if (wallCheck)
        {
            __result = true;
            return;
        }
        bool ledgeCheck = !__instance.IsRayHittingLocal(paddedFront_Down, down, length);
#if TESTING
        Primitives.Line(__instance.transform,
                    paddedFront_Down,
                    paddedFront_Down + down * length,
                    ledgeCheck ? Color.green : Color.red,
                    oneFrame: true);
#endif
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

    /// <summary>Transpiler of <see cref="Crawler" />.ShouldTurn() to adjust the length check of the rays</summary>
    /// <param name="instructions">List of IL instructions</param>
    /// <param name="ilGenerator"> The <see cref="ILGenerator" /></param>
    /// <returns>Emits new List of IL instructions</returns>
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
                new(OpCodes.Call, shouldTurnLengthCheck) // ShouldTurnLengthCheck(Math.Abs(this.transform.GetScaleY())
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
    private static bool SetScale_DoSetScale_Prefix(SetScale __instance, out Vector3 __state)
    {
        __state = Vector3.zero;
        if (__instance.Owner != null)
        {
            GameObject? obj = __instance.gameObject.GameObject.Value ?? (__instance.gameObject.OwnerOption == OwnerDefaultOption.UseOwner ? __instance.Owner : null);
            if (obj != null)
            {
                HealthManager healthManager = __instance.Owner.GetComponent<HealthManager>();

                if (__instance.gameObject.OwnerOption != OwnerDefaultOption.UseOwner && Instance.currentScaledObjects.TryGetValue(obj, out Vector3 originalScale))
                    obj.transform.localScale = originalScale;
                else if (healthManager != null && __instance.gameObject.OwnerOption == OwnerDefaultOption.UseOwner && Instance.currentScaledObjects.TryGetValue(__instance.Owner, out originalScale))
                    healthManager.transform.localScale = originalScale;
                else
                {
                    if (obj != null)
                        Instance.currentScaledObjects.TryAdd(obj, obj.transform.localScale);
                    else
                        Instance.currentScaledObjects.TryAdd(__instance.Owner, __instance.Owner.transform.localScale);
                }

                __state = new Vector3(__instance.x.Value, __instance.y.Value, __instance.z.Value);

                if (!__instance.x.UseVariable)
                {
                    var xScale = obj?.transform.GetScaleX() ?? __instance.Owner.transform.GetScaleX();

                    if (__instance.x.Value != xScale && __instance.x.Value != 0f)
                        __instance.x = Math.Abs(xScale) * (__instance.x.Value < 0 ? -1 : 1);
                }

                if (!__instance.y.UseVariable)
                {
                    var yScale = obj?.transform.GetScaleY() ?? __instance.Owner.transform.GetScaleY();

                    if (__instance.y.Value != yScale && __instance.y.Value != 0f)
                        __instance.y = Math.Abs(yScale) * (__instance.y.Value < 0 ? -1 : 1);
                }
            }
        }

        return true;
    }

    private readonly Dictionary<GameObject, Vector3> currentScaledObjects = [];

    private static void SetScale_DoSetScale_Postfix(SetScale __instance, Vector3 __state)
    {
        if (__instance.Owner != null && __state != Vector3.zero)
        {
            HealthManager healthManager = __instance.Owner.GetComponent<HealthManager>();
            if (healthManager != null && Instance.currentEnemyHealthManagers.ContainsKey(healthManager))
            {
                if (!__instance.x.UseVariable)
                    __instance.x.Value = __state.x;

                if (!__instance.y.UseVariable)
                    __instance.y.Value = __state.y;
            }
        }
    }

    /// <summary>
    /// <see langword="true" /> if a request to resize everything is happening, <see langword="false" /> if we are not
    /// </summary>
    private static bool resizing;

    /// <summary>Updates all enemy sizes in the <see cref="HealthManager" /> list</summary>
    private void UpdateCurrentEnemies()
    {
        CleanCurrentLists();
        resizing = true;
        foreach (var enemy in currentEnemyHealthManagers)
        {
            SetSize(enemy.Key);
        }
        resizing = false;
    }

    /// <summary>Called to apply patches to a specific enemy <see cref="Fsm" /></summary>
    /// <param name="fsm">The <see cref="Fsm" /> to patch</param>
    public void OnFSMStart(Fsm fsm)
    {
        if (fsm.GameObject == null)
            return;

        EnemyFSMPatches.ApplyPatches(fsm.GameObject);
#if TESTING
        EnableRayCastDebugs(fsm.GameObject);
#endif
    }

    /// <summary>Dictionary containing the various ignored child <see cref="GameObject" /> s</summary>
    private static Dictionary<string, string[]> ignoredChildAdjusts = new()
    {
        { "Bone Roller", ["All"] },
        { "Rosary Pilgrim", ["Leap Range", "Attack Range" ] },
        { "Bloom Puncher", ["Alert Range"] },
        { "Farmer Scissors", ["Close Range"] },
        { "Farmer Catcher", ["Attack Range", "Alert Range"] },
        { "Crowman", ["Slash Range"] },
        { "Roachfeeder Tall", ["Attack Range", "Evade Range"] },
        { "Roachkeeper", ["Attack Range"] },
        { "Song Pilgrim 03", ["Attack Range", "Slash Range"] },
        { "Song Pilgrim 01", ["Attack Range"] },
        { "Song Automaton Goomba", ["Alert Range"] },
        {"Understore Heavy", ["Attack Range"] },
        {"Understore Small", ["Attack Range"] },
        {"Understore Thrower", ["Cartwheel Range"] },
        {"Pilgrim 03 Understore", ["Alert Range"] },
        {"Song Knight", ["DashStop Range", "Parry Range"] },
    };

    /// <summary>Record to hold the various options for adjusting specific children</summary>
    private record ChildAdjustOption()
    {
        /// <summary>Name of the child</summary>
        public string Name = "";
        /// <summary>Name of the parent</summary>
        public string ParentName = "";
        /// <summary>If the X direction should be shifted in reverse, used in conjunction with <see cref="ShiftX" /></summary>
        public bool ReverseX = false;
        /// <summary>If the Y direction should be shifted in reverse, used in conjunction with <see cref="ShiftY" /></summary>
        public bool ReverseY = false;
        /// <summary>Use an alternate scaler for when the collider is null</summary>
        public bool AlternateNullColliderScale = false;
        /// <summary>Ignore adjusting the X direction, is ignored by <see cref="ShiftX" /></summary>
        public bool IgnoreX = false;
        /// <summary>Ignore adjusting the Y direction, is ignored by <see cref="ShiftY" /></summary>
        public bool IgnoreY = false;
        /// <summary>If the X direction should adjusted by shifting, only matters if the child has a collider</summary>
        public bool ShiftX = false;
        /// <summary>If the Y direction should adjusted by shifting, only matters if the child has a collider</summary>
        public bool ShiftY = false;
        /// <summary>
        /// If the child should be shifted by a magic number, only matters if the child does not have a collider (currently)
        /// </summary>
        public float MagicNumber = 0;

        /// <summary>If the child should not be scaled</summary>
        public bool NoScale = false;
    }

    /// <summary>List of specific adjustment options</summary>
    private static List<ChildAdjustOption> childAdjustOptions = new()
    {
        new(){ParentName = "Swamp Ductsucker", IgnoreY = true},
        new(){Name = "Ray Point G", IgnoreY = true},
        new(){Name = "Ground Ray", IgnoreY = true},
        new(){ParentName = "Song Automaton Shield", Name = "Alert Range", ShiftY = true},
        new(){ParentName = "Song Automaton Shield", Name = "Unalert Range", ShiftY = true},
        new(){Name = "Swing Flame", IgnoreX = true},
        new(){Name = "Projectile Point", MagicNumber = 0.104245f, NoScale = true },
        new(){Name = "Throw Point", AlternateNullColliderScale= true},
        new(){Name = "Near Range", ParentName = "Crawfather", ShiftY = true}
    };

    /// <summary>
    /// <see cref="Array" /> of <see langword="string" /> names for additional child <see cref="GameObject" /> s besides
    /// any that contain 'Range' that need to be adjusted
    /// </summary>
    private static string[] extraChildAdjusts =
        ["Route", // Aspid wanderings
        "Start Point", // Flyers
        "Patrol Point", // Flyers
        "Move Target", // Bell Fly
        "Throw Point", // Throwers
        "Initial Position Markers", // Stick Insects
        "Range Top", // Swamp Drifters
        "Range Bot", // Swamp Drifters
        "Ray Ground F", // Swamp Ductsucker
        "Ray Jump", // Swamp Ductsucker
        "Ray Ground B", // Swamp Ductsucker
        "Ray Ground Edge", // Swamp Ductsucker
        "Ray Point G", // Clockwork enemies
        "Ground Ray", // Clockwork enemies
        "Steam Hit", // Clockwork enemies
        "Swing Flame", // Last Judge
        "Projectile Point", // Second Sentinel
        "Chains", // Crawfather
        "Stomp Blast", // Seth
        //"Tendril",
        "a",
        "b",
        "c"
        ];

    /// <summary>Fixes any and all Alert style Ranges to be their default pre-scaled size.</summary>
    /// <param name="parent">    The enemy <see cref="GameObject" /> whos children need to be adjusted</param>
    /// <param name="parentName">The name of the enemy</param>
    public static void AdjustChildren(GameObject parent, string parentName)
    {
        if (ignoredChildAdjusts.ContainsKey(parentName) && ignoredChildAdjusts[parentName].Contains("All")) return;

        foreach (Transform child in parent.transform)
        {
            if (child.name.Contains(" Range"))
                Adjust(child);
            else if (extraChildAdjusts.Contains(child.name))
                Adjust(child);
        }

        void Adjust(Transform child)
        {
            if (child == null || (ignoredChildAdjusts.ContainsKey(parentName) && ignoredChildAdjusts[parentName].Contains(child.name))) return;

#if TESTING
            //CuteRandoCore.Log.LogInfo($"Patching {child.name} for {parent.name} in scene {parent.scene.name}");
#endif
            bool reverseX = false;
            bool reverseY = false;
            bool alternateNullColliderScale = false;
            bool ignoreX = false;
            bool ignoreY = false;
            bool shiftX = false;
            bool shiftY = false;
            float magicNumber = 0f;
            bool noScale = false;

            if (child.name.Equals("Tendril"))
            {
                TendrilAdjust(parent, child.gameObject);
                return;
            }

            var adjustOptions = childAdjustOptions.Find(adjust =>
                                                        {
                                                            if (adjust.ParentName != "" && !adjust.ParentName.Equals(parentName))
                                                                return false;
                                                            if (adjust.Name != "" && !adjust.Name.Equals(child.name))
                                                                return false;
                                                            if (adjust.ParentName.Equals(parentName) || adjust.Name.Equals(child.name))
                                                                return true;
                                                            return false;
                                                        });
            if (adjustOptions != null)
            {
                reverseX = adjustOptions.ReverseX;
                reverseY = adjustOptions.ReverseY;
                alternateNullColliderScale = adjustOptions.AlternateNullColliderScale;
                ignoreX = adjustOptions.IgnoreX;
                ignoreY = adjustOptions.IgnoreY;
                shiftX = adjustOptions.ShiftX;
                shiftY = adjustOptions.ShiftY;
                magicNumber = adjustOptions.MagicNumber;
                noScale = adjustOptions.NoScale;

                float rotation = parent.transform.rotation.eulerAngles.z;
                if ((rotation > 45 && rotation < 135) || (rotation > 225 && rotation < 315))
                {
                    if (ignoreY)
                    {
                        ignoreX = !ignoreX;
                        ignoreY = !ignoreY;
                    }
                }
            }

            Vector2 parentScale = parent.transform.localScale;
            float parentRotation = Mathf.Deg2Rad * parent.transform.rotation.eulerAngles.z;

            bool negX = child.GetScaleX() < 0;
            bool negY = child.GetScaleY() < 0;

            Vector2 childLocalPosition = child.localPosition;
            Collider2D childCollider = child.GetComponent<Collider2D>();

            if (resizing)
                child.localScale = child.localScale.DivideElements(child.GetAbsScale());

            if (!noScale)
                child.localScale = new((child.lossyScale.x.Abs() * (negX ? -1 : 1)) / (parentScale.x * parentScale.x), (child.lossyScale.y.Abs() * (negY ? -1 : 1)) / (parentScale.y * parentScale.y));

            if (childCollider == null)
            {
                if (magicNumber != 0f)
                {
                    float newX = !ignoreX ? ShiftPosToBase.ShiftN(child.localPosition.x, parentScale.x.Abs(), magicNumber, Mathf.Sin(parentRotation)) : child.localPosition.x;
                    float newY = !ignoreY ? ShiftPosToBase.ShiftN(child.localPosition.y, parentScale.y.Abs(), magicNumber, Mathf.Cos(parentRotation)) : child.localPosition.y;
                    child.localPosition = new(newX, newY);
                }
                else if (!alternateNullColliderScale)
                {
                    var newPos = (child.position - parent.transform.position).MultiplyElements(child.GetAbsScale()) + parent.transform.position;
                    child.position = new Vector2(ignoreX ? child.position.x : newPos.x, ignoreY ? child.position.y : newPos.y);
                }
                else
                    child.localPosition = new(child.localPosition.x * ((3 * parentScale.x) / 4), child.localPosition.y * ((3 * parentScale.y) / 4));
#if TESTING
                DebugDrawing.Primitives.Text(child.position + new Vector3(0, 0.1f, 0), child.name, Quaternion.identity, 0.2f, Color.red, duration: 10f);
                DebugDrawing.Primitives.Circle(child.position, 0.1f, color: Color.red, duration: 10f);
                DebugDrawing.Primitives.Text(parent.transform.position + new Vector3(0, -0.1f, 0), child.name, Quaternion.identity, 0.2f, Color.red, duration: 10f);
                DebugDrawing.Primitives.Square(parent.transform.position, 0.2f, color: Color.red, duration: 10f);
#endif
                return;
            }

            if (shiftX || shiftY)
            {
                float newX = shiftX ? ShiftChildPosition(childLocalPosition.x, child.GetScaleX().Abs(), childCollider.bounds.extents.x / 2, childCollider.offset.x, Mathf.Sin(parentRotation), reverseX) : childLocalPosition.x;
                float newY = shiftY ? ShiftChildPosition(childLocalPosition.y, child.GetScaleY().Abs(), childCollider.bounds.extents.y / 2, childCollider.offset.y, Mathf.Cos(parentRotation), reverseY) : childLocalPosition.y;
                child.localPosition = new(newX, newY);
                return;
            }
            else
            {
                if (childLocalPosition.x.IsWithinTolerance(0.1f, 0f) && childLocalPosition.y.IsWithinTolerance(0.1f, 0f)) return; // transform is located at approximately 0,0 on the object, don't bother doing more math

                float newX = AdjustChildPosition(
                    childLocalPosition.x,
                    childCollider.offset.x,
                    parentScale.x.Abs());
                float newY = AdjustChildPosition(
                    childLocalPosition.y,
                    childCollider.offset.y,
                    parentScale.y.Abs());

                child.localPosition = new(ignoreX ? childLocalPosition.x : newX, ignoreY ? childLocalPosition.y : newY);
            }
        }
    }

    private static void TendrilAdjust(GameObject parent, GameObject tendril)
    {
        var botCollider = tendril.transform.Find("Tendril Collider Bottom");
        if (botCollider == null) return;
        DebugDrawing.Primitives.Circle(botCollider.position, 0.1f, color: Color.blue, duration: 10f);

        var absParentScale = parent.transform.GetAbsScaleY();
        var newScale = absParentScale / (absParentScale * absParentScale);
        var collider = tendril.GetComponent<BoxCollider2D>();
        var size = new Vector2(collider.size.x, collider.size.y);
        var offset = new Vector2(collider.offset.x, collider.offset.y);
        var attackDetectorFSM = tendril.transform.Find("Attack Detector").GetComponent<PlayMakerFSM>();

        tendril.transform.localScale *= newScale;
        collider.offset = offset * absParentScale;
        collider.size = size * absParentScale;
        attackDetectorFSM.FsmVariables.GetFsmVector2("Box Size").Value = collider.size;
        attackDetectorFSM.FsmVariables.GetFsmVector2("Box Offset").Value = collider.offset;
        DebugDrawing.Primitives.Circle(botCollider.position, 0.1f, color: Color.red, duration: 10f);
    }

    /// <summary>Calculates a new position for a child element, scaling around its origin</summary>
    /// <param name="position">The old child position</param>
    /// <param name="offset">  The offset of the child</param>
    /// <param name="scale">   The scale of the parent</param>
    /// <returns>The new position value</returns>
    public static float AdjustChildPosition(
        float position,
        float offset,
        float scale)
    {
        var correctScale = Math.Abs(scale / (scale * scale));
        var a = position - offset;
        var b = correctScale * a;
        var c = b + (offset * correctScale);

        return (float)c;
    }

    /// <summary>Calculates a new position for a child element, shifting it a direction</summary>
    /// <inheritdoc cref="AdjustChildPosition" />
    /// <param name="scale">   The scale of the child</param>
    /// <param name="halfSize">The half-size of the child</param>
    /// <param name="rotation">The rotation of the parent</param>
    /// <param name="reverse"> Optional: If the direction should be reversed</param>
    public static float ShiftChildPosition(
        float position,
        float scale,
        float halfSize,
        float offset,
        float rotation,
        bool reverse = false)
    {
        return position - (offset * scale - halfSize) * rotation * (reverse ? 1 : -1);
    }

    /// <summary>Updates an enemy with a new size</summary>
    /// <param name="enemy">The health manager of the enemy we want to change</param>
    /// <exception cref="NotImplementedException">Thrown if there is an unimplemented randomizer type</exception>
    private void SetSize(HealthManager enemy)
    {
        if (enemy?.transform == null || (!resizing && !Instance.currentEnemyHealthManagers.TryAdd(enemy, enemy.transform.localScale))) return;

        bool boss = CuteRandoCore.IsBoss(enemy);

        if (boss && !EnemySizeRandomizerSetting.HasFlag(RandomizerEnemyTypeFlags.Boss))
            return;
        if (!boss && EnemySizeRandomizerSetting.Equals(RandomizerEnemyTypeFlags.Boss))
            return;

        string operatingScene = enemy.gameObject.scene.name;

        Walker walker = enemy.gameObject.GetComponent<Walker>();
        Transform thingTransform = enemy.transform;
        float tempMultiplier;
        string name = CuteRandoCore.CullName(enemy.name);

        if (Instance.excludedEnemies.Contains(name))
            return;

        if (resizing && enemySizeRandomizerSetting.Value == RandomizerEnemyTypeFlags.None)
        {
            ApplySize(thingTransform, currentEnemyHealthManagers[enemy].z, walker);
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

        AdjustChildren(enemy.gameObject, name);
    }

    /// <summary>Enables all the raycasts' debug lines on an enemy</summary>
    /// <param name="enemy">The enemy to enable the debug lines on</param>
    private static void EnableRayCastDebugs(GameObject enemy)
    {
        var fsms = enemy.GetComponents<PlayMakerFSM>();

        foreach (var fsm in fsms)
        {
            foreach (var state in fsm.FsmStates)
            {
                foreach (var action in state.Actions)
                {
                    if (action is RayCast2d rc2d)
                        rc2d.debug.Value = true;
                    else if (action is RayCast2dV2 rc2dv2)
                        rc2dv2.debug.Value = true;
                    else if (action is RayCast2dV3 rc2dv3)
                        rc2dv3.Debug.Value = true;
                }
            }
        }
    }

    /// <summary>Randomizes the size of a given <see cref="Transform" /></summary>
    /// <param name="boss">     <see langword="true" /> if the enemy is a boss</param>
    /// <param name="transform">The <see cref="Transform" /> of the enemy to be scaled</param>
    /// <param name="walker">   <see langword="true" /> if the enemy is a 'walker'</param>
    /// <param name="seed">     A value to use when randomizing so that the result is the same</param>
    /// <returns>The multiplier used to scale the enemy by</returns>
    public float RandomizeSize(bool boss, Transform transform, Walker? walker = null, int seed = int.MinValue)
    {
        float multiplier = CuteRandoCore.RandomFloat(boss ? BossSizePercentRange.AsTuple() : EnemySizePercentRange.AsTuple(), seed);
        ApplySize(transform, multiplier, walker);
        return multiplier;
    }

    /// <summary>Scales the given <see cref="Transform" /> by the multiplier</summary>
    /// <inheritdoc cref="RandomizeSize" />
    /// <param name="multiplier">The value used to scale the enemy by</param>
    private void ApplySize(Transform transform, float multiplier, Walker? walker)
    {
        if (resizing)
            transform.localScale = transform.localScale.DivideElements(transform.GetAbsScale());
        transform.localScale *= multiplier;

        if (walker != null)
        {
            Traverse rightScale = CuteRandoCore.TraverseCreator(walker, "rightScale");
            int direction = (float)rightScale.GetValue() < 0 ? -1 : 1;
            rightScale.SetValue(transform.GetAbsScaleX() * direction);
        }
    }

    /// <summary>Cleans the current <see cref="Scene" />'s lists of any <see langword="null" /> keys.</summary>
    private void CleanCurrentLists()
    {
        for (int i = currentEnemyHealthManagers.Count - 1; i >= 0; i--)
        {
            if (currentEnemyHealthManagers.ElementAt(i).Key == null)
                currentEnemyHealthManagers.Remove(currentEnemyHealthManagers.ElementAt(i).Key);
        }

        for (int i = currentScaledObjects.Count - 1; i >= 0; i--)
        {
            if (currentScaledObjects.ElementAt(i).Key == null)
                currentScaledObjects.Remove(currentScaledObjects.ElementAt(i).Key);
        }
    }

    protected override void ResetAllLists()
    {
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

    /// <inheritdoc cref="RandomizerConsistency" />
    private ConfigEntry<RandomizerConsistencyA> randomizerConsistency;

    /// <summary>Default setting for how consistent the enemy size should be</summary>
    public const RandomizerConsistencyA defaultRandomizerConsistency = RandomizerConsistencyA.None;

    /// <summary>Randomize the size of enemies</summary>
    public RandomizerEnemyTypeFlags EnemySizeRandomizerSetting
    {
        get => enemySizeRandomizerSetting.Value;
        internal set => enemySizeRandomizerSetting.Value = value;
    }

    /// <inheritdoc cref="EnemySizeRandomizerSetting" />
    private ConfigEntry<RandomizerEnemyTypeFlags> enemySizeRandomizerSetting;

    /// <summary>Default choice for the size randomizer</summary>
    public const RandomizerEnemyTypeFlags defaultEnemySizeRandomizerSetting = RandomizerEnemyTypeFlags.None;

    /// <summary>Randomize the size of normal enemies</summary>
    public FloatRange EnemySizePercentRange
    {
        get => enemySizePercentRange.Value;
        internal set => enemySizePercentRange.Value = value;
    }

    /// <inheritdoc cref="EnemySizePercentRange" />
    private ConfigEntry<FloatRange> enemySizePercentRange;

    /// <summary>Default choice for enemy size randomizer</summary>
    public static readonly FloatRange defaultEnemySizePercentRange = new(0.35f, 1.6f);

    /// <summary>Randomize the size of boss enemies</summary>
    public FloatRange BossSizePercentRange
    {
        get => bossSizePercentRange.Value;
        internal set => bossSizePercentRange.Value = value;
    }

    /// <inheritdoc cref="BossSizePercentRange" />
    private ConfigEntry<FloatRange> bossSizePercentRange;

    /// <summary>Default choice for boss size randomizer</summary>
    public static readonly FloatRange defaultBossSizePercentRange = new(0.85f, 1.25f);

    /// <summary>Acceptable value range for the enemy sizes</summary>
    public static AcceptableRangeforFloatRange acceptableEnemySizeRange = new(0.25f, 2f);

    /// <summary>Used for determining if we need to update</summary>
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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;

using BepInEx.Configuration;

using HarmonyLib;

using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;

using Smol_Randomizer.Patchers;
using Smol_Randomizer.Patchers.Scene;

#if TESTING

using Newtonsoft.Json;

using Smol_Randomizer.DebugDrawing;

#endif

using Smol_Randomizer.Settings;

using UnityEngine;
using UnityEngine.SceneManagement;

using static Smol_Randomizer.Settings.Settings;

using Translate = HutongGames.PlayMaker.Actions.Translate;

namespace Smol_Randomizer.Randomizers;

internal class Hero_Size_Rando : Rando_Base
{
    /// <summary>We make a singleton of this rando</summary>
    private static readonly Lazy<Hero_Size_Rando> instance = new(() => new Hero_Size_Rando());

    /// <summary>Externally visible instance of this rando</summary>
    public static Hero_Size_Rando Instance => instance.Value;

    /// <summary>The layer mask for checing collisions</summary>
    private const int LAYERMASK = 8448;

    /// <summary>True if we need to grab Hornet's base values</summary>
    private static bool grabVariables = true;

    // Various base values to save
    private static float hornetBaseDashSpeed;

    private static float hornetBaseSprintSpeed;
    private static float hornetBaseSprintStartSpeed;
    private static float hornetBaseQuickSpeed; // Flea brew?
    private static float hornetBaseQuickerSpeed; // Anklets?
    private static float hornetBaseSpeedToEnterUp;
    private static float hornetBaseSpeedToEnterHor;
    private static Vector2 defaultColliderSize;
    private static Vector2 defaultColliderOffset;

    /// <summary>If we have adjusted Hornet's size</summary>
    private bool heroSizeChanged = false;

    /// <summary>Reference to Hornet's Transform</summary>
    private Transform heroTransform;

    /// <summary>Reference to Hornet's Collider</summary>
    private BoxCollider2D heroCollider;

    /// <summary>Hornet's Scale</summary>
    internal Vector3 heroScale = new(1f, 1f, 1f);

    /// <summary>Hornet's Scale when facing Right</summary>
    internal Vector3 heroScaleFlipped = new(-1f, 1f, 1f);

    /// <summary>The Translate action within the Vault FSM state</summary>
    private static Translate mantleVaultTranslate;

    /// <summary>The YOffset float within Mantle FSM</summary>
    private static FsmFloat baseMantleVaultYOffset;

    /// <summary>Dictionary containing all the waterRegions in the current scene.</summary>
    private readonly Dictionary<SurfaceWaterRegion, float> waterRegions = [];

    /// <summary>If we have attempted to apply the transpiler</summary>
    private static bool transpilerAttempted = false;

    /// <summary>The current Scene</summary>
    private Scene currentScene;

    private readonly Dictionary<string, float> sceneHeroSize = [];
    private float saveHeroSize = float.MinValue;

    #region Randomizer_Info

    /// <summary>Used for registering this randomizer in the core for when the hero damager gets enabled</summary>
    private Randomizer_Info eventActiveHeroDamager;

    /// <summary>Used for registeromg tjos randomizer in the core for when a scene is loaded</summary>
    private Randomizer_Info eventOnSceneLoad;

    #endregion Randomizer_Info

    /// <summary>Constructor for this singleton</summary>
    private Hero_Size_Rando()
    {
        InitRandomizer();
    }

    protected override void InitRandomizer()
    {
        RandomizerName = "Hornet Size Randomizer";
        RandomizerDescription = "Randomizes the size of Hornet.";

        if (!CuteRandoCore.RegisterRandomizer(new(
        RandomizerName,
        RandomizerEventType.GameStartup,
        AccessTools.Method(
            typeof(Hero_Size_Rando),
            nameof(GameStartup)),
        this))) return;

        eventOnSceneLoad = new(
            RandomizerName,
            RandomizerEventType.OnSceneLoad,
            AccessTools.Method(typeof(Hero_Size_Rando),
            nameof(OnSceneLoad)),
            this);
        eventActiveHeroDamager = new(
            RandomizerName,
            RandomizerEventType.ActiveHeroDamager,
            AccessTools.Method(
                typeof(Hero_Size_Rando),
                nameof(DamageHero_OnEnable)),
            this);

        base.InitRandomizer();
    }

    protected override void Register()
    {
        CuteRandoCore.RegisterRandomizer(eventOnSceneLoad);

        SceneFSMPatches.RegisterPatchCollection(sceneFSMPatches);
    }

    protected override void Unregister()
    {
        CuteRandoCore.UnregisterRandomizer(eventOnSceneLoad);

        SceneFSMPatches.UnregisterPatchCollection(sceneFSMPatches);
    }

    protected override void OnLoaded()
    {
        grabVariables = true;
        heroSizeChanged = false;
    }

#if TESTING // Enable Saving Data

    protected override void ApplySaveData(Dictionary<string, object> savedData)
    {
        if (savedData.TryGetValue(nameof(heroScale), out object tempDict))
            heroScale = JsonConvert.DeserializeObject<Vector3>(tempDict.ToString());
    }

    protected override void SetSaveData(Dictionary<string, object> savedData)
    {
        savedData[nameof(heroScale)] = heroScale;
    }

    protected override void OnSettingsSaved()
    {
        Dictionary<string, object> savedData = SaveData.GetSavedData(RandomizerName);

        savedData[nameof(heroScale)] = heroScale;
    }

#endif

    /// <summary>Patch HealthManager.OnEnable on game startup</summary>
    private void GameStartup()
    {
        Type randoType = typeof(Hero_Size_Rando);
        Type heroControllerType = typeof(HeroController);

        // Basic Scale Patches
        CuteRandoCore.harmony.Patch(AccessTools.EnumeratorMoveNext(
            AccessTools.Method(
                heroControllerType, "EnterHeroSubHorizontal")),
                postfix: new HarmonyMethod(randoType, nameof(HeroController_EnterHeroSubHorizontal_Postfix)));
        CuteRandoCore.harmony.Patch(AccessTools.Method(
            typeof(SetScale), "DoSetScale"),
            postfix: new HarmonyMethod(randoType, nameof(SetScale_DoSetScale_Postfix)));

        // Water Surfaces
        CuteRandoCore.harmony.Patch(AccessTools.Method(
            typeof(SurfaceWaterRegion), "Start"),
            postfix: new HarmonyMethod(randoType, nameof(SurfaceWaterRegion_Start_Postfix)));

        // Sprint and Dash FSM Patches
        CuteRandoCore.harmony.Patch(AccessTools.Method(
            heroControllerType, "HeroDash"),
            prefix: new HarmonyMethod(randoType, nameof(HeroController_HeroDash_Prefix)));

        // Scene and Object Patches
        CuteRandoCore.harmony.Patch(AccessTools.Method(
            typeof(LiftControl), "Awake"),
            postfix: new HarmonyMethod(randoType, nameof(LiftControl_Awake_Postfix)));

        // Transpilers
        if (PlayerSizeRando) // only patch if rando is enabled
            TryTranspilerPatches();

#if TESTING
        CuteRandoCore.harmony.Patch(AccessTools.Method(
            heroControllerType, nameof(HeroController.CheckClamberLedge)),
            postfix: new HarmonyMethod(typeof(Hero_Size_Rando), nameof(HeroController_CheckClamberLedge_Postfix)));
#endif
    }

    /// <summary>Attempts to apply the transpiler patches. Can only be called once, otherwise silently aborts.</summary>
    private static void TryTranspilerPatches()
    {
        if (transpilerAttempted)
        {
            //Instance.PlayerSizeRando = false;
            return;
        }

        try
        {
            var heroControllerType = typeof(HeroController);
            var randoType = typeof(Hero_Size_Rando);

#if !TESTING // Disable if we are testing
            CuteRandoCore.harmony.Patch(AccessTools.Method(
                heroControllerType, nameof(HeroController.CheckClamberLedge)),
                transpiler: new HarmonyMethod(randoType, nameof(HeroController_CheckClamberLedge_Transpiler)));
#endif
            CuteRandoCore.harmony.Patch(AccessTools.EnumeratorMoveNext(
                AccessTools.Method(
                    typeof(NPCControlBase), "MovePlayer")),
                    transpiler: new HarmonyMethod(randoType, nameof(NPCControlBase_MovePlayer_Transpiler)));
            CuteRandoCore.harmony.Patch(AccessTools.Method(
                heroControllerType, "Update10"),
                transpiler: new HarmonyMethod(randoType, nameof(HeroController_Update10_Transpiler)));
            CuteRandoCore.harmony.Patch(AccessTools.Method(
                heroControllerType, "FaceLeft"),
                transpiler: new HarmonyMethod(randoType, nameof(HeroController_Facing_Transpilser)));
            CuteRandoCore.harmony.Patch(AccessTools.Method(
                heroControllerType, "FaceRight"),
                transpiler: new HarmonyMethod(randoType, nameof(HeroController_Facing_Transpilser)));
        }
        catch (Exception ex)
        {
            CuteRandoCore.Log.LogError($"Unable to patch a method with a transpiler, disabling Hero Size Rando. Restart Silksong please and leave it disabled.\nPlease then open a GitHub Issue report for this mod.\nException: {ex.Message}\nStack Trace:{ex.StackTrace}");
            //Instance.PlayerSizeRando = false;
        }
        finally
        {
            transpilerAttempted = true;
        }
    }

    #region Basic Scale Patches

    /// <summary>Changes setting the direction of hornet from a hard coded value to a scaled value</summary>
    /// <param name="instructions"></param>
    /// <param name="ilGenerator"> </param>
    /// <returns></returns>
    private static IEnumerable<CodeInstruction> NPCControlBase_MovePlayer_Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator ilGenerator)
    {
        var getScaleX = AccessTools.Method(typeof(Extensions), "GetScaleX");
        var setScaleX = AccessTools.Method(typeof(Extensions), "SetScaleX");
        var getLocalScale = AccessTools.Method(typeof(Transform), "get_localScale");

        List<CodeInstruction> instructionList = [.. instructions];

        for (int i = 0; i < instructionList.Count; i++)
        {
            CodeInstruction instruction = instructionList[i];

            if (instructionList[i].OperandIs(setScaleX))
            {
                yield return instructionList[i - 10]; // this (NPCControlBase)?
                yield return instructionList[i - 9]; // thisthis (MovePlayer_MoveNext_1)?
                yield return instructionList[i - 8]; // .thisthisthis (MovePlayer_MoveNext_2)?
                yield return instructionList[i - 7]; // .heroController
                yield return instructionList[i - 6]; // .get_transform()
                yield return new(OpCodes.Callvirt, getScaleX); // .getScaleX()
                yield return new(OpCodes.Mul); // multiply x by dir from earlier in stack
            }

            yield return instruction;
        }
    }

    /// <summary>Patch to hook DeSetScale and fix the Y scale of hornet</summary>
    /// <param name="__instance"></param>
    private static void SetScale_DoSetScale_Postfix(ref SetScale __instance)
    {
        if (!Instance.PlayerSizeRando) return;
        if (__instance.Owner.name.Contains("Knight Spike Death") || __instance.State.Name == "Gooped Start")
        {
            GameObject gameObject = __instance.Fsm.GetOwnerDefaultTarget(__instance.gameObject);
            gameObject.transform.localScale = gameObject.transform.GetScaleX() > 0 ? Instance.heroScale : Instance.heroScaleFlipped;
        }
    }

    /// <summary>Changes hard coded scale to be a multiply by -1 to swap direction</summary>
    /// <param name="instructions"></param>
    /// <param name="ilGenerator"> </param>
    /// <returns></returns>
    private static IEnumerable<CodeInstruction> HeroController_Facing_Transpilser(IEnumerable<CodeInstruction> instructions, ILGenerator ilGenerator)
    {
        List<CodeInstruction> instructionList = [.. instructions];

        for (int i = 0; i < instructionList.Count; i++)
        {
            CodeInstruction instruction = instructionList[i];

            if (instruction.opcode == OpCodes.Ldloca_S)
            {
                yield return instruction; // localScale variable
                yield return new(OpCodes.Dup); // duplicate that reference
                yield return new(OpCodes.Ldfld, instructionList[i + 2].operand); // load field localScale.x
                yield return new(OpCodes.Ldc_R4, -1f); // -1
                yield return new(OpCodes.Mul); // Multiply localScale.x by -1

                i++; // we handled yielding the instruction, now step to next
                continue; // but we are skipping ldc.r4 1 or -1, depending on left or right
            }

            yield return instruction;
        }
    }

    /// <summary>Bypass scale check from Update10</summary>
    /// <param name="instructions"></param>
    /// <param name="ilGenerator"> </param>
    /// <returns></returns>
    private static IEnumerable<CodeInstruction> HeroController_Update10_Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator ilGenerator)
    {
        var getScaleX = AccessTools.Method(typeof(Extensions), "GetScaleX");
        var setScaleX = AccessTools.Method(typeof(Extensions), "SetScaleX");
        var heroSizeRandoInstance = AccessTools.Method(typeof(Hero_Size_Rando), "get_Instance");
        var playerSizeRando = AccessTools.Method(typeof(Hero_Size_Rando), "get_PlayerSizeRando");
        var controlRelinquished = AccessTools.Field(typeof(HeroController), "controlReqlinquished");

        Label skipScaleCheck = ilGenerator.DefineLabel();

        List<CodeInstruction> skipCheck =
            [
                new CodeInstruction(OpCodes.Call, heroSizeRandoInstance), // Get the instance of Hero_Size_Rando
                new(OpCodes.Callvirt, playerSizeRando), // Gets the setting if the rando is enabled or not
                new(OpCodes.Ldc_I4_1), // Load one (true)
                new(OpCodes.Ceq), // Compare the setting to the loaded value
                new(OpCodes.Brtrue_S, skipScaleCheck) // Branch to a label later on in the code that we will be adding
            ];

        List<CodeInstruction> instructionList = [.. instructions];

        for (int i = 0; i < instructionList.Count; i++)
        {
            CodeInstruction instruction = instructionList[i];

            if (i < instructionList.Count - 2 && instructionList[i + 2].OperandIs(getScaleX))
            {
                foreach (var patchInstruction in skipCheck)
                {
                    if (patchInstruction.opcode == OpCodes.Call)
                        patchInstruction.MoveLabelsFrom(instruction);
                    yield return patchInstruction;
                }
            }

            if (i < instructionList.Count - 1 && instructionList[i + 1].OperandIs(controlRelinquished))
            {
                yield return instruction.WithLabels(skipScaleCheck); // Add label so we can branch here
                continue;
            }

            yield return instruction;
        }
    }

    // Used for checking where we are in the Enumerator's state
    private static bool transitionHandled = false;

    private static int enumeratorStep = 1;

    /// <summary>
    /// Patch that hooks the end of EnterHeroSubHorizontal and adjusts Hornet's position because of her scale.
    /// </summary>
    /// <param name="__result">If there are more Enumerators to be processed.</param>
    private static void HeroController_EnterHeroSubHorizontal_Postfix(ref bool __result)
    {
        if (!Instance.PlayerSizeRando) return;

        if (transitionHandled)
        {
            if (!__result)
            {
                transitionHandled = false; // We are done, reset to false
                enumeratorStep = 1;
            }

            return;
        }
        if (enumeratorStep != 2)
        {
            enumeratorStep++;
            return;
        }

        Transform? transform = Instance.heroTransform;

        if (transform == null) return;

        // We take the scale, subtract 1 from it, multiply by the magic number, then add it to the position and add
        // 0.005f to get it closer to level
        transform.localPosition = transform.localPosition with { y = ((transform.localScale.y - 1) * 0.513f) + transform.localPosition.y + 0.005f }; // Magic number came from spreadsheeting various scales and figuring out the right number through that

        transitionHandled = true;
    }

    #endregion Basic Scale Patches

    #region Ledge Clambering

#if TESTING

    /// <summary>
    /// Patch that hooks the end of CheckClamberLedge to correctly check depending on hornet's scale. This probably can
    /// be turned into a transpiler.
    /// </summary>
    /// <param name="__instance">       Hornet</param>
    /// <param name="__result">         The result of the check</param>
    /// <param name="___col2d">         Hornet's collider</param>
    /// <param name="y">                The y position that she will land at</param>
    /// <param name="clamberedCollider">What Hornet is clambering onto</param>
    private static void HeroController_CheckClamberLedge_Postfix(ref HeroController __instance, ref bool __result, ref Collider2D ___col2d, ref float y, ref Collider2D clamberedCollider)
    {
        if (__result || !Instance.PlayerSizeRando || !Instance.PatchHeroFSMs) return; // We are already going to clamber, or we aren't randomizing
        // Don't check for roof, other checks cover that later in method
        if (Instance.heroTransform == null || NoClamberRegion.IsClamberBlocked/* || __instance.CheckNearRoof()*/)
            return;
        /* Hornet's Default Size:
         *  Width: 0.25
         *  Height: 1.04
        */

        bool facingRight = __instance.cState.facingRight;

        float multiplier = Instance.heroScale.x;

        float near = 0.37f * multiplier;
        float far = 0.77f * multiplier;
        float height = 0.67f * multiplier;
        float ceiling = 2.26f * multiplier;
        float landing = 2.0f * multiplier;
        float landingHeight = 1.5f * multiplier;

        Vector2 vector = Instance.heroTransform.position;
        Vector2 heightOffset = new(0f, height);
        Vector2 facingDirection = facingRight ? Vector2.right : Vector2.left;
        Vector2 farOrigin = vector + new Vector2(facingRight ? far : far * -1, height);
        Vector2 nearOrigin = vector + new Vector2(facingRight ? near : near * -1, height);

        // Hitboxes for initial checks
        Quaternion rotation = Instance.heroTransform.rotation;

        DebugDrawer.Circle(farOrigin, 0.1f, rotation);
        DebugDrawer.Circle(nearOrigin, 0.1f, rotation);

        bool directAbove = Helper.IsRayHittingNoTriggers(vector + heightOffset, facingDirection, 0.75f, LAYERMASK);
        DebugDrawer.Line(vector + heightOffset, vector + heightOffset + facingDirection * 0.75f, directAbove ? Color.red : Color.cyan);

        if (directAbove) // Directly Above, different than CheckNearRoof, probably allows for clambering through a gap?
            return;

        bool farGood = Helper.IsRayHittingNoTriggers(farOrigin, Vector2.down, ceiling, LAYERMASK, out var closestFarHit);
        bool nearGood = Helper.IsRayHittingNoTriggers(nearOrigin, Vector2.down, ceiling, LAYERMASK, out var closestNearHit);

        Vector2 heightGoodVector = new(vector.x, closestNearHit.point.y);
        bool heightGood = !Helper.IsRayHittingNoTriggers(heightGoodVector, Vector2.up, ceiling, LAYERMASK, out var heightHit);

        // Hitboxes for landing points
        DebugDrawer.Line(heightGoodVector, heightGoodVector + Vector2.up * ceiling, heightGood ? Color.cyan : Color.red);

        if (!heightGood)
            return;

        DebugDrawer.Line(farOrigin, farOrigin + Vector2.down * ceiling, farGood ? Color.green : Color.red);
        DebugDrawer.Line(nearOrigin, nearOrigin + Vector2.down * ceiling, nearGood ? Color.green : Color.red);

        if (!farGood || !nearGood) // One or both are not good
            return;

        Vector2 farHitPoint = closestFarHit.point;
        Vector2 nearHitPoint = closestNearHit.point;

        DebugDrawer.Circle(farHitPoint, 0.1f, rotation);
        DebugDrawer.Circle(nearHitPoint, 0.1f, rotation);

        clamberedCollider = closestNearHit.collider;

        farHitPoint.y += 0.1f;
        nearHitPoint.y += 0.1f;

        bool farHitPointGood = !Helper.IsRayHittingNoTriggers(farHitPoint, Vector2.up, ceiling - 0.1f, LAYERMASK, out var closestFarPointHit);
        bool nearHitPointGood = !Helper.IsRayHittingNoTriggers(nearHitPoint, Vector2.up, ceiling - 0.1f, LAYERMASK, out var closestNearPointHit);

        if (!farHitPointGood || !nearHitPointGood) // Above Landing Spot
            return;

        if (!farHitPoint.y.IsWithinTolerance(0.1f, nearHitPoint.y))
        {
            return;
        }

        Vector2 n = new(vector.x, ___col2d.bounds.min.y + 0.2f);

        if (Helper.IsRayHittingNoTriggers(n, Vector2.down, landing, LAYERMASK, out var closestHit3) && farHitPoint.y - closestHit3.point.y < landingHeight)
        {
            return;
        }
        y = farHitPoint.y;
        __result = true;
    }

#endif

    /// <summary>Patches the ledge clamber check to allow for scaling.</summary>
    /// <param name="instructions"></param>
    /// <param name="ilGenerator"> </param>
    /// <returns></returns>
    private static IEnumerable<CodeInstruction> HeroController_CheckClamberLedge_Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator ilGenerator)
    {
        var playerSizeRando = AccessTools.Method(typeof(Hero_Size_Rando), "get_PlayerSizeRando");
        var patchHeroFSMs = AccessTools.Method(typeof(Hero_Size_Rando), "get_PatchHeroFSMs");
        var heroSizeRandoInstance = AccessTools.Method(typeof(Hero_Size_Rando), "get_Instance");
        var heroSizeRandoHeroSize = AccessTools.Field(typeof(Hero_Size_Rando), nameof(heroScale));
        var heroSizeRandoHeroSizeX = AccessTools.Field(heroSizeRandoHeroSize.FieldType, "x");
        var checkNearRoof = AccessTools.Method(typeof(HeroController), "CheckNearRoof");

        LocalBuilder multiplier = ilGenerator.DeclareLocal(typeof(float));
        LocalBuilder near = ilGenerator.DeclareLocal(typeof(float));
        LocalBuilder far = ilGenerator.DeclareLocal(typeof(float));
        LocalBuilder height = ilGenerator.DeclareLocal(typeof(float));
        LocalBuilder ceiling = ilGenerator.DeclareLocal(typeof(float));
        LocalBuilder landing = ilGenerator.DeclareLocal(typeof(float));
        LocalBuilder landingHeight = ilGenerator.DeclareLocal(typeof(float));
        Label vanilla = ilGenerator.DefineLabel();
        Label saveMultiplier = ilGenerator.DefineLabel();
        Label notPatchingFSMs = ilGenerator.DefineLabel();
        Label doCheck1 = ilGenerator.DefineLabel();
        Label doCheck2 = ilGenerator.DefineLabel();

        List<CodeInstruction> instructionList = [.. instructions];

        /* Check to see if player size rando is enabled, if so we want to skip the roof check
         *
         * if (!Instance.PatchHeroFSMs) {
         *     if (Instance.PlayerSizeRando) {
         *         goto [AfterCheckNearRoof]
         *     }
         * }
         */
        List<CodeInstruction> playerSizeRandoEnabled =
            [
                new(OpCodes.Call, heroSizeRandoInstance), // Get the instance of Hero_Size_Rando
                new(OpCodes.Callvirt, patchHeroFSMs), // Gets the setting if the patching FSMs is enabled or not
                new(OpCodes.Ldc_I4_1), // Load one (true)
                new(OpCodes.Ceq), // Compare the setting to the loaded value
                new(OpCodes.Brfalse_S, notPatchingFSMs), // Branch to a label if the two values were the same
                new(OpCodes.Call, heroSizeRandoInstance), // Get the instance of Hero_Size_Rando
                new(OpCodes.Callvirt, playerSizeRando), // Gets the setting if the rando is enabled or not
                new(OpCodes.Ldc_I4_1), // Load one (true)
                new(OpCodes.Ceq), // Compare the setting to the loaded value
                new(OpCodes.Brtrue_S, null), // Branch to a label that we will grab later if the two values were the same
                new CodeInstruction(OpCodes.Nop).WithLabels(notPatchingFSMs) // Nop to allow us to branch here from earlier
            ];

        /* Setup our local variables
         * if (Instance.PatchHeroFSMs)
         *     float multiplier = Instance.heroSize.x;
         * else
         *     float multiplier = 1f;
         * float near = 0.37f * multiplier;
         * float far = 0.77f * multiplier;
         * float height = 0.67f * multiplier;
         * float ceiling = 2.26f * multiplier;
         * float landing = 2.0f * multiplier;
         * float landingHeight = 1.5f * multiplier;
         */
        List<CodeInstruction> createVariables =
            [

                new(OpCodes.Call, heroSizeRandoInstance), // Get the instance of Hero_Size_Rando
                new(OpCodes.Callvirt, patchHeroFSMs), // Gets the setting if the rando is enabled or not
                new(OpCodes.Ldc_I4_1), // Load one (true)
                new(OpCodes.Ceq), // Compare the setting to the loaded value
                new(OpCodes.Brfalse_S, vanilla), // Branch to loading the value of 1f if we are not patching FSMs
                new(OpCodes.Call, heroSizeRandoInstance), // Get the instance of Hero_Size_Rando
                new(OpCodes.Ldflda, heroSizeRandoHeroSize), // Get the field heroSize
                new(OpCodes.Ldfld, heroSizeRandoHeroSizeX), // Grab x from that field
                new(OpCodes.Br_S, saveMultiplier), // goto saving the value to multiplier
                new CodeInstruction(OpCodes.Ldc_R4, 1f).WithLabels(vanilla), // Load value of 1f
                new CodeInstruction(OpCodes.Stloc_S, multiplier).WithLabels(saveMultiplier), // Save the value to multiplier
                new(OpCodes.Ldc_R4, 0.37f), // Load the near value
                new(OpCodes.Ldloc_S, multiplier), // Load the multiplier
                new(OpCodes.Mul), // Multiply them together
                new(OpCodes.Stloc_S, near), // Save the value to near
                new(OpCodes.Ldc_R4, 0.77f), // Load the far value
                new(OpCodes.Ldloc_S, multiplier), // Load the multiplier
                new(OpCodes.Mul), // Multiply them together
                new(OpCodes.Stloc_S, far), // Save the value to far
                new(OpCodes.Ldc_R4, 0.67f), // Load the height value
                new(OpCodes.Ldloc_S, multiplier), // Load the multiplier
                new(OpCodes.Mul), // Multiply them together
                new(OpCodes.Stloc_S, height), // Save the value to height
                new(OpCodes.Ldc_R4, 2.26f), // Load the ceiling value
                new(OpCodes.Ldloc_S, multiplier), // Load the multiplier
                new(OpCodes.Mul), // Multiply them together
                new(OpCodes.Stloc_S, ceiling), // Save the value to ceiling
                new(OpCodes.Ldc_R4, 2f), // Load the landing value
                new(OpCodes.Ldloc_S, multiplier), // Load the multiplier
                new(OpCodes.Mul), // Multiply them together
                new(OpCodes.Stloc_S, landing), // Save the value to landing
                new(OpCodes.Ldc_R4, 1.5f), // Load the landingHeight value
                new(OpCodes.Ldloc_S, multiplier), // Load the multiplier
                new(OpCodes.Mul), // Multiply them together
                new(OpCodes.Stloc_S, landingHeight) // Save the value to landingHeight
            ];

        // Substitution to use our own values
        List<CodeInstruction> patchOriginsRight =
            [
                new(OpCodes.Ldloc_S, null), // Load [VARIABLE]
                new(OpCodes.Ldloc_S, height) // Load height
            ];

        // Substitution to use our own values (but negative!)
        List<CodeInstruction> patchOriginsLeft =
            [
                new(OpCodes.Ldloc_S, null), // Load [VARIABLE]
                new(OpCodes.Ldc_R4, -1f), // Load -1
                new(OpCodes.Mul), // multiply the [VARIABLE] by -1 to make it negative
                new(OpCodes.Ldloc_S, height) // Load height
            ];

        for (int i = 0; i < instructionList.Count; i++)
        {
            CodeInstruction instruction = instructionList[i];

            // Setup our local variables right away
            if (i == 0)
            {
                foreach (CodeInstruction codeInstruction in createVariables)
                {
                    yield return codeInstruction;
                }
            }

            // Patch so that CheckNearRoof is only called when the rando is disabled or when we are not patching FSMs
            if (i < instructionList.Count - 1 && instructionList[i + 1].OperandIs(checkNearRoof))
            {
                playerSizeRandoEnabled[0].MoveLabelsFrom(instruction);
                foreach (CodeInstruction codeInstruction in playerSizeRandoEnabled)
                {
                    if (codeInstruction.opcode == OpCodes.Brtrue_S)
                    {
                        codeInstruction.operand = instructionList[i + 2].operand;
                    }

                    yield return codeInstruction;
                }
            }

            // Patch origin assignments to use scaled heights
            if (i < instructionList.Count - 4 && (instructionList[i + 4].opcode == OpCodes.Stloc_2 || instructionList[i + 4].opcode == OpCodes.Stloc_3))
            {
                if ((float)instructionList[i].operand == 0.77f)
                {
                    patchOriginsRight[0] = new(OpCodes.Ldloc_S, far);

                    foreach (CodeInstruction codeInstruction in patchOriginsRight)
                    {
                        yield return codeInstruction;
                    }
                }
                else if ((float)instructionList[i].operand == -0.77f)
                {
                    patchOriginsLeft[0] = new(OpCodes.Ldloc_S, far);

                    foreach (CodeInstruction codeInstruction in patchOriginsLeft)
                    {
                        yield return codeInstruction;
                    }
                }
                else if ((float)instructionList[i].operand == 0.37f)
                {
                    patchOriginsRight[0] = new(OpCodes.Ldloc_S, near);

                    foreach (CodeInstruction codeInstruction in patchOriginsRight)
                    {
                        yield return codeInstruction;
                    }
                }
                else if ((float)instructionList[i].operand == -0.37f)
                {
                    patchOriginsLeft[0] = new(OpCodes.Ldloc_S, near);

                    foreach (CodeInstruction codeInstruction in patchOriginsLeft)
                    {
                        yield return codeInstruction;
                    }
                }
                else
                {
                    goto EndOfIf; // Something weird is going on, hop out
                }

                i++;
                continue;

            EndOfIf:
                ;
            }

            // Patch first check that is directly above Hornet
            if (i < instructionList.Count - 4 && instructionList[i + 4].operand is float h && h == 0.75f)
            {
                yield return new(OpCodes.Ldloc_S, height);
                continue;
            }

            // Patch short height checks to not exit right away if false
            if (i < instructionList.Count - 2 && instructionList[i + 2].operand is float f && f == 2.16f)
            {
                // Reset List
                for (int j = 0; j < playerSizeRandoEnabled.Count; j++)
                {
                    playerSizeRandoEnabled[j] = new CodeInstruction(playerSizeRandoEnabled[j].opcode, playerSizeRandoEnabled[j].operand).WithLabels(playerSizeRandoEnabled[j].labels);
                    if (playerSizeRandoEnabled[j].opcode == OpCodes.Brtrue) // No idea why it randomly switches
                        playerSizeRandoEnabled[j].opcode = OpCodes.Brtrue_S;
                    if (playerSizeRandoEnabled[j].opcode == OpCodes.Brfalse)
                        playerSizeRandoEnabled[j].opcode = OpCodes.Brfalse_S;

                    if (playerSizeRandoEnabled[j].opcode == OpCodes.Brfalse_S)
                    {
                        if ((Label)playerSizeRandoEnabled[j].operand == notPatchingFSMs)
                            playerSizeRandoEnabled[j].operand = doCheck1;
                        else if ((Label)playerSizeRandoEnabled[j].operand == doCheck1)
                            playerSizeRandoEnabled[j].operand = doCheck2;
                    }

                    if (playerSizeRandoEnabled[j].opcode == OpCodes.Nop)
                    {
                        if (playerSizeRandoEnabled[j].labels.Contains(notPatchingFSMs))
                            playerSizeRandoEnabled[j].labels = [doCheck1];
                        else if (playerSizeRandoEnabled[j].labels.Contains(doCheck1))
                            playerSizeRandoEnabled[j].labels = [doCheck2];
                    }
                }

                if (instruction.labels.Count > 0)
                    playerSizeRandoEnabled[0].MoveLabelsFrom(instruction);

                foreach (CodeInstruction codeInstruction in playerSizeRandoEnabled)
                {
                    if (codeInstruction.opcode == OpCodes.Brtrue_S)
                    {
                        codeInstruction.operand = instructionList[i + 5].operand;
                    }

                    yield return codeInstruction;
                }
            }

            // Replace 2.26f with scaled version
            if (instruction.operand is float f1 && f1 == 2.26f)
            {
                yield return new(OpCodes.Ldloc_S, ceiling);
                continue;
            }

            // Replace 2.16f with scaled version
            if (instruction.operand is float f2 && f2 == 2.16f)
            {
                // ceiling - 0.1f;
                yield return new(OpCodes.Ldloc_S, ceiling);
                yield return new(OpCodes.Ldc_R4, 0.1f);
                yield return new(OpCodes.Sub);
                continue;
            }

            // Replace 2f with scaled version
            if (instruction.operand is float f3 && f3 == 2f)
            {
                yield return new(OpCodes.Ldloc_S, landing);
                continue;
            }

            // Replace 1.5f with scaled version
            if (instruction.operand is float f4 && f4 == 1.5f)
            {
                yield return new(OpCodes.Ldloc_S, landingHeight);
                continue;
            }

            yield return instruction;
        }
    }

    /// <summary>FSMAction to squish Hornet's hitbox to allow her to clamber normal spots when large</summary>
    private static SetBoxCollider2DSizeVector clamberSquishBox;

    /// <summary>FSMAction to unsquish Hornet's hitbox after clambering normal spots when large</summary>
    private static SetBoxCollider2DSizeVector clamberUnsquishBox;

    /// <summary>Set of States to patch Actions into</summary>
    private static readonly StatePatchSet mantleFSMPatchSet = new(
        "Mantle",
        [new StatePatch(
            "Vault",
            delegate (FsmState state, object[]? param)
            {
                state.Actions = state.Actions.AddToArray(clamberSquishBox);
            }),
        new StatePatch(
            "Idle",
            delegate(FsmState state, object[]? param)
            {
                state.Actions = state.Actions.AddToArray(clamberUnsquishBox);
            })
        ]);

    /// <summary>Patches the Mantle FSM to shrink Hornet's hitbox when Clambering</summary>
    private void PatchMantleFSM()
    {
        clamberSquishBox = new SetBoxCollider2DSizeVector
        {
            gameObject1 = new FsmOwnerDefault
            {
                GameObject = HeroController.instance.gameObject
            },
            size = new Vector2(defaultColliderSize.x * 2, defaultColliderSize.y / 2),
            offset = Vector2.down
        };

        clamberUnsquishBox = new SetBoxCollider2DSizeVector
        {
            gameObject1 = new FsmOwnerDefault
            {
                GameObject = HeroController.instance.gameObject
            },
            size = new Vector2(defaultColliderSize.x, defaultColliderSize.y),
            offset = new Vector2(defaultColliderOffset.x, defaultColliderOffset.y)
        };

        mantleFSMPatchSet.ApplyPatches(HeroController.instance.mantleFSM);
    }

    /// <summary>Updates the Mantle squish sizes when called</summary>
    private void UpdateMantleFSMSizes()
    {
        if (!PatchHeroFSMs) // Don't change the FSMs
        {
            ResetMantleFSMSizes();
            return;
        }
        clamberSquishBox.size = new Vector2(defaultColliderSize.x * 2, defaultColliderSize.y / 2);
        clamberSquishBox.offset = Vector2.down;
        clamberUnsquishBox.size = new Vector2(defaultColliderSize.x, defaultColliderSize.y);
        clamberUnsquishBox.offset = new Vector2(defaultColliderOffset.x, defaultColliderOffset.y);
    }

    /// <summary>Resets the Mantle squish sizes to default when called</summary>
    private void ResetMantleFSMSizes()
    {
        clamberSquishBox.size = new Vector2(defaultColliderSize.x, defaultColliderSize.y);
        clamberSquishBox.offset = new Vector2(defaultColliderOffset.x, defaultColliderOffset.y);
        clamberUnsquishBox.size = new Vector2(defaultColliderSize.x, defaultColliderSize.y);
        clamberUnsquishBox.offset = new Vector2(defaultColliderOffset.x, defaultColliderOffset.y);
    }

    #endregion Ledge Clambering

    #region Water Surfaces

    /// <summary>
    /// Patch that hooks SurfaceWaterRegion's Start method to adjust the collider and adds it to a list so we can update
    /// it mid scene if needed
    /// </summary>
    /// <param name="__instance">The SurfaceWaterRegion we are adjusting</param>
    private static void SurfaceWaterRegion_Start_Postfix(ref SurfaceWaterRegion __instance)
    {
        if (!Instance.PlayerSizeRando) return;

        float offset = __instance.GetComponent<BoxCollider2D>().offset.y;
        Instance.waterRegions.Add(__instance, offset);
        UpdateWaterSurface(__instance, Instance.heroScale.x, offset);
    }

    /// <summary>Update all the water surfaces in the current scene</summary>
    private static void UpdateWaterSurfaces()
    {
        foreach (var region in Instance.waterRegions)
        {
            UpdateWaterSurface(region.Key, Instance.heroScale.x, region.Value);
        }
    }

    /// <summary>Update the given water surface region.</summary>
    /// <param name="region">    The region to update</param>
    /// <param name="multiplier">The multiplier to use, should be Hornet's size</param>
    /// <param name="offset">    The water collider's height to be scaled</param>
    private static void UpdateWaterSurface(SurfaceWaterRegion region, float multiplier, float offset)
    {
        BoxCollider2D col = region.GetComponent<BoxCollider2D>();

        col.offset = col.offset with { y = offset - (1.44f * multiplier) + 1.44f };
    }

    #endregion Water Surfaces

    #region Scene  and Object Patches

    /// <summary>Dictionary containing the various scene patches</summary>
    private static readonly HashSet<ISceneFSMPatch> sceneFSMPatches = new()
    {
        new SceneStateActionPatch("Bonetown",
            "Churchkeeper Intro Scene",
            "Wait for Hero Grounded",
            typeof(FloatCompare),
            delegate(FsmStateAction action, object[]? param)
            {
                ((FloatCompare)action).float2.Value *= Instance.heroScale.y;
            },
            fsmName: "Control"),
        new SceneStateActionPatch("Greymoor_01",
            "Floor Control Scene",
            "Flip",
            typeof(CheckYPosition),
            delegate(FsmStateAction action, object[]? param)
            {
                ((CheckYPosition)action).compareTo.Value *= Instance.heroScale.y;
            },
            fsmName: "Control")
    };

    private static void LiftControl_Awake_Postfix(LiftControl __instance, TriggerEnterEvent ___doorCloseTrigger)
    {
        if ((bool)___doorCloseTrigger)
            ___doorCloseTrigger.OnTriggerEntered += delegate
            {
                OnDoorCloseTrigger(___doorCloseTrigger);
            };
    }

    private static void OnDoorCloseTrigger(TriggerEnterEvent doorCloseTrigger)
    {
        if (!Instance.PlayerSizeRando) return;

        var position = Instance.heroTransform.position;
        var width = doorCloseTrigger.GetComponent<BoxCollider2D>().size.x / 3;
        var heroWidth = Instance.heroCollider.size.x / 3;
        Instance.heroTransform.position = position with
        {
            x = position.x > doorCloseTrigger.transform.position.x ? position.x - width - heroWidth : position.x + width + heroWidth
        };
    }

    #endregion Scene  and Object Patches

    #region Sprint and Dash Patches

    /// <summary>FSMAction to squish Hornet's hitbox to allow her to sprint through normal spots when large</summary>
    private static SetBoxCollider2DSizeVector sprintSquishBox;

    /// <summary>FSMAction to unsquish Hornet's hitbox after sprinting through normal spots when large</summary>
    private static SetBoxCollider2DSizeVector sprintUnsquishBox;

    /// <summary>Set of States to patch Actions into</summary>
    private static readonly StatePatchSet sprintFSMPatchSet = new(
        "Sprint",
        [new StatePatch(
            ["Dashed", "Start Sprint"],
            delegate (FsmState state, object[]? param)
            {
                if(sprintSquishBox == null) return;

                FsmStateAction[] bassAckwards = [sprintSquishBox];
                state.Actions = bassAckwards.AddRangeToArray(state.Actions);
            },
            delegate (FsmState state, object[]? param){
                if(sprintSquishBox == null) return;

                state.Actions = state.Actions.Except([sprintSquishBox]).ToArray();
            }),
        new StatePatch(
            ["Idle", "Dash Stab Dir"],
            delegate(FsmState state, object[]? param)
            {
                if(sprintUnsquishBox == null) return;

                FsmStateAction[] bassAckwards = [sprintUnsquishBox];
                state.Actions = bassAckwards.AddRangeToArray(state.Actions);
            },
            delegate(FsmState state, object[]? param){
                if(sprintUnsquishBox == null) return;

                state.Actions = state.Actions.Except([sprintUnsquishBox]).ToArray();
            })
        ]);

    /// <summary>Patches the Sprint FSM to adjust Hornet's hitbox when dashing</summary>
    private void PatchSprintFSM()
    {
        sprintSquishBox = new SetBoxCollider2DSizeVector
        {
            gameObject1 = new FsmOwnerDefault
            {
                GameObject = HeroController.instance.gameObject
            },
        };

        SquishSize(false);

        sprintUnsquishBox = new SetBoxCollider2DSizeVector
        {
            gameObject1 = new FsmOwnerDefault
            {
                GameObject = HeroController.instance.gameObject
            },
            size = new Vector2(defaultColliderSize.x, defaultColliderSize.y),
            offset = new Vector2(defaultColliderOffset.x, defaultColliderOffset.y)
        };

        sprintFSMPatchSet.ApplyPatches(HeroController.instance.sprintFSM);
    }

    /// <summary>Removes the Sprint FSM patches that adjust Hornet's hitbox when dashing</summary>
    private void UnpatchSprintFSM()
    {
        sprintFSMPatchSet.RemovePatches(HeroController.instance.sprintFSM);
    }

    /// <summary>Patch to listen for if we are air dashing and adjust the collider accordingly</summary>
    private static bool HeroController_HeroDash_Prefix()
    {
        if (Instance.PlayerSizeRando)
        {
            Instance.UpdateSprintFSMSizes();
        }
        return true;
    }

    /// <summary>Sets the sprint squish size and offset</summary>
    /// <param name="dashingDown">If hornet is dashing down</param>
    private void SquishSize(bool dashingDown)
    {
        if (sprintSquishBox == null) return;
        if (dashingDown)
        {
            sprintSquishBox.size = new Vector2(defaultColliderSize.x, defaultColliderSize.y);
            sprintSquishBox.offset = new Vector2(defaultColliderOffset.x, defaultColliderOffset.y);
        }
        else
        {
            sprintSquishBox.size = new Vector2(defaultColliderSize.x * 3.1f, defaultColliderSize.y / 1.9f);
            sprintSquishBox.offset = new Vector2(defaultColliderOffset.x, defaultColliderOffset.y + -0.494529f);
        }
    }

    /// <summary>Updates the Sprint squish sizes when called</summary>
    private void UpdateSprintFSMSizes()
    {
        if (!PatchHeroFSMs) // Don't change the FSMs
        {
            ResetSprintFSMSizes();
            return;
        }

        if (sprintSquishBox is null || sprintUnsquishBox is null) PatchSprintFSM();

        HeroActions inputActions = InputHandler.Instance.inputActions;
        bool dashingDown = inputActions.Down.IsPressed
            && !HeroController.instance.cState.onGround
            && !inputActions.Left.IsPressed
            && !inputActions.Right.IsPressed;

        SquishSize(dashingDown);

#pragma warning disable CS8602
        sprintUnsquishBox.size = new Vector2(defaultColliderSize.x, defaultColliderSize.y);
#pragma warning restore CS8602
        sprintUnsquishBox.offset = new Vector2(defaultColliderOffset.x, defaultColliderOffset.y);
    }

    /// <summary>Resets the Sprint squish sizes to default when called</summary>
    private void ResetSprintFSMSizes()
    {
        sprintSquishBox.size = new Vector2(defaultColliderSize.x, defaultColliderSize.y);
        sprintSquishBox.offset = new Vector2(defaultColliderOffset.x, defaultColliderOffset.y);
        sprintUnsquishBox.size = new Vector2(defaultColliderSize.x, defaultColliderSize.y);
        sprintUnsquishBox.offset = new Vector2(defaultColliderOffset.x, defaultColliderOffset.y);
    }

    #endregion Sprint and Dash Patches

    /// <summary>On Scene Load, save current loading scene</summary>
    /// <param name="scene">The new scene that is loading</param>
    /// <param name="mode"> ?</param>
    private void OnSceneLoad(Scene scene, LoadSceneMode _)
    {
        currentScene = scene;
        SetSize();
        waterRegions.Clear();
    }

    protected override void OnUnload()
    {
    }

    /// <summary>Patch to watch for when Hornet gets damaged</summary>
    /// <param name="instance">The HeroDamager we want to attach a listener to</param>
    /// <param name="_">       Discarded HealthManager</param>
    private static void DamageHero_OnEnable(ref DamageHero instance, ref HealthManager _)
    {
        if (Instance.PlayerSizeRando && Instance.HeroSizeConsistency == RandomizerConsistencyC.OnDamageTaken)
            instance.OnDamagedHero.AddListener(HeroDamaged);
    }

    /// <summary>Called when DamageHero fires OnDamagedHero</summary>
    public static void HeroDamaged()
    {
        Instance.SetSize(true);
    }

    // TODO: tall hornet breaks some triggers and breaks the universe;
    /// <summary>Sets the size of Hornet</summary>
    private void SetSize(bool damageTaken = false)
    {
        if (!PlayerSizeRando)
        {
            // If the rando was disabled and we had changed things
            if (heroSizeChanged)
                ReturnToDefault();
            return;
        }

        // Get the base stats and stash them for later
        if (grabVariables || heroTransform == null || heroCollider == null)
        {
            if (!SaveVariables())
            {
                return;
            }
        }

        float multiplier;

        switch (HeroSizeConsistency)
        {
            case RandomizerConsistencyC.OnSceneTransition:
                if (currentScene == null || currentScene.name == null)
                    return;
                if (!sceneHeroSize.TryGetValue(currentScene.name, out multiplier))
                {
                    multiplier = CuteRandoCore.RandomFloat(
                        PlayerSizeRange.AsTuple(),
                        CuteRandoCore.RNGSeed(currentScene.name));
                    sceneHeroSize[currentScene.name] = multiplier;
                }
                break;

            case RandomizerConsistencyC.PerSaveFile:
                if (saveHeroSize.Equals(float.MinValue))
                {
                    saveHeroSize = CuteRandoCore.RandomFloat(PlayerSizeRange.AsTuple(), SaveData.SaveSeed);
                }
                multiplier = saveHeroSize;
                break;

            case RandomizerConsistencyC.OnDamageTaken:
                if (!damageTaken) return;

                multiplier = CuteRandoCore.RandomFloat(PlayerSizeRange.AsTuple());
                break;

            default:
                throw new NotImplementedException();
        }

        SetVariables(multiplier);
        heroSizeChanged = true;
    }

    /// <summary>Sets Hornet's variables according to the given multiplier.</summary>
    /// <param name="multiplier">Hornet's scale</param>
    private void SetVariables(float multiplier)
    {
        if (heroTransform == null) return;

        heroScale = Vector3.one * multiplier;
        heroScaleFlipped = heroScale with { x = heroScale.x * -1 };
        FsmVariables sprintFSMVariables = HeroController.instance.sprintFSM.FsmVariables;

        // Divide so that when scaled, Hornet runs the same speed as her normal size
        sprintFSMVariables.FindFsmFloat("Dash Speed").Value = hornetBaseDashSpeed / multiplier;
        sprintFSMVariables.FindFsmFloat("Sprint Speed Regular").Value = hornetBaseSprintSpeed / multiplier;
        sprintFSMVariables.FindFsmFloat("Sprint Start Speed").Value = hornetBaseSprintStartSpeed / multiplier;
        sprintFSMVariables.FindFsmFloat("Sprint Speed Quick").Value = hornetBaseQuickSpeed / multiplier;
        sprintFSMVariables.FindFsmFloat("Sprint Speed Quicker").Value = hornetBaseQuickerSpeed / multiplier;

        mantleVaultTranslate.y = baseMantleVaultYOffset.Value * multiplier;

        HeroController.instance.SPEED_TO_ENTER_SCENE_HOR = (hornetBaseSpeedToEnterHor * Instance.heroScale.x) + (0.2f - (0.2f * Instance.heroScale.x));
        HeroController.instance.SPEED_TO_ENTER_SCENE_UP = (hornetBaseSpeedToEnterUp * Instance.heroScale.y) + (0.2f - (0.2f * Instance.heroScale.y));

        heroTransform.localScale = heroTransform.localScale.x > 0f ? heroScale : heroScaleFlipped;

        UpdateMantleFSMSizes();
        UpdateSprintFSMSizes();

        UpdateWaterSurfaces();
    }

    /// <summary>Save Hornet's base values</summary>
    /// <returns>True if it succeeded, false if it did not</returns>
    private bool SaveVariables()
    {
        try
        {
            heroTransform = (Transform)CuteRandoCore.TraverseCreator(HeroController.instance, "transform").GetValue();
            heroCollider = (BoxCollider2D)CuteRandoCore.TraverseCreator(HeroController.instance, "col2d").GetValue();

            if (heroTransform == null || heroCollider == null) return false; // Something is wrong here, abort

            if (grabVariables)
            {
                FsmVariables sprintFSMVariables = HeroController.instance.sprintFSM.FsmVariables;

                hornetBaseDashSpeed = sprintFSMVariables.FindFsmFloat("Dash Speed").Value;
                hornetBaseSprintSpeed = sprintFSMVariables.FindFsmFloat("Sprint Speed Regular").Value;
                hornetBaseSprintStartSpeed = sprintFSMVariables.FindFsmFloat("Sprint Start Speed").Value;
                hornetBaseQuickSpeed = sprintFSMVariables.FindFsmFloat("Sprint Speed Quick").Value;
                hornetBaseQuickerSpeed = sprintFSMVariables.FindFsmFloat("Sprint Speed Quicker").Value;

                defaultColliderSize = new(heroCollider.size.x, heroCollider.size.y);
                defaultColliderOffset = new(heroCollider.offset.x, heroCollider.offset.y);

                hornetBaseSpeedToEnterHor = HeroController.instance.SPEED_TO_ENTER_SCENE_HOR;
                hornetBaseSpeedToEnterUp = HeroController.instance.SPEED_TO_ENTER_SCENE_UP;

                foreach (var state in HeroController.instance.mantleFSM.FsmStates)
                {
                    if (state.Name == "Vault")
                    {
                        foreach (var action in state.Actions)
                            if (action.GetType() == typeof(Translate))
                            {
                                mantleVaultTranslate = (Translate)action;
                                baseMantleVaultYOffset = mantleVaultTranslate.y;
                                break;
                            }
                        break;
                    }
                }

                grabVariables = false;
            }
        }
        catch (Exception ex)
        {
            CuteRandoCore.Log.LogError($"Failed to capture Hero variables, disabling {RandomizerName}.\nException: {ex.Message}\nStack Trace:\n{ex.StackTrace}");
            PlayerSizeRando = false;
            grabVariables = false;
            return false;
        }

        PatchMantleFSM();
        PatchSprintFSM();

        return true;
    }

    /// <summary>Returns Hornet's values to default</summary>
    private void ReturnToDefault()
    {
        if (!heroSizeChanged || heroTransform == null)
            return;

        FsmVariables sprintFSMVariables = HeroController.instance.sprintFSM.FsmVariables;

        heroTransform.localScale = heroTransform.localScale.x > 0f ? new(1f, 1f, 1f) : new(-1f, 1f, 1f);
        sprintFSMVariables.FindFsmFloat("Dash Speed").Value = hornetBaseDashSpeed;
        sprintFSMVariables.FindFsmFloat("Sprint Speed Regular").Value = hornetBaseSprintSpeed;
        sprintFSMVariables.FindFsmFloat("Sprint Start Speed").Value = hornetBaseSprintStartSpeed;
        sprintFSMVariables.FindFsmFloat("Sprint Speed Quick").Value = hornetBaseQuickSpeed;
        sprintFSMVariables.FindFsmFloat("Sprint Speed Quicker").Value = hornetBaseQuickerSpeed;

        CuteRandoCore.TraverseCreator(typeof(HeroController), "SPEED_TO_ENTER_SCENE_HOR").SetValue(hornetBaseSpeedToEnterHor);
        CuteRandoCore.TraverseCreator(typeof(HeroController), "SPEED_TO_ENTER_SCENE_UP").SetValue(hornetBaseSpeedToEnterUp);

        mantleVaultTranslate.y = baseMantleVaultYOffset;

        ResetMantleFSMSizes();
        ResetSprintFSMSizes();

        heroCollider.size = defaultColliderSize;
        heroCollider.offset = defaultColliderOffset;

        heroScale = Vector3.one;

        UpdateWaterSurfaces();

        heroSizeChanged = false;
    }

    protected override void ResetAllLists()
    {
        sceneHeroSize.Clear();
        saveHeroSize = float.MinValue;
    }

    #region Settings

    /// <summary>Setting for if player size should be randomized</summary>
    public bool PlayerSizeRando
    {
        get => playerSizeRando.Value;
        internal set => playerSizeRando.Value = value;
    }

    private ConfigEntry<bool> playerSizeRando;

    /// <summary>Default choice for if player size should be randomized</summary>
    public const bool defaultPlayerSizeRando = false;

    /// <summary>Setting for how conistent Hornet's size should be</summary>
    public RandomizerConsistencyC HeroSizeConsistency
    {
        get => playerSizeConsistency.Value;
        internal set => playerSizeConsistency.Value = value;
    }

    private ConfigEntry<RandomizerConsistencyC> playerSizeConsistency;

    /// <summary>Default consistency of Hornet's size</summary>
    public const RandomizerConsistencyC defaultHeroSizeConsistency = RandomizerConsistencyC.PerSaveFile;

    /// <summary>Setting for the range that the hero's size can be randomized to</summary>
    public FloatRange PlayerSizeRange
    {
        get => playerSizeRange.Value;
        internal set => playerSizeRange.Value = value;
    }

    private ConfigEntry<FloatRange> playerSizeRange;

    /// <summary>Default range for the hero's size</summary>
    public readonly FloatRange defaultPlayerSizeRange = new(0.68f, 1.35f);

    /// <summary>Acceptable value range for the hero's size</summary>
    public static AcceptableRangeforFloatRange acceptablePlayerSizeRange = new(0.25f, 2f);

    /// <summary>Setting for if the hero's FSMs should be patched for quality of life</summary>
    public bool PatchHeroFSMs
    {
        get => patchHeroFSMs.Value;
        internal set => patchHeroFSMs.Value = value;
    }

    private ConfigEntry<bool> patchHeroFSMs;

    /// <summary>Default option for patching the FSMs</summary>
    public const bool defaultPatchHeroFSMs = true;

    protected override void InitSettings()
    {
        ConfigFile config = Settings.Settings.ConfigFile;
        playerSizeRando = config.Bind(
            section: RandomizerName,
            key: "Randomize Hornets Size",
            defaultValue: defaultPlayerSizeRando,
            configDescription: new ConfigDescription(
                description: "Enable/Disable Randomization of Hornet's Size.",
                tags: new ConfigurationManagerAttributes
                {
                    Order = 3
                }));
        playerSizeConsistency = config.Bind(
            section: RandomizerName,
            key: "Size Change Trigger",
            defaultValue: defaultHeroSizeConsistency,
            configDescription: new ConfigDescription(
                description: "When Hornet's size will change.",
                tags: new ConfigurationManagerAttributes
                {
                    Order = 2
                }));
        playerSizeRange = config.Bind(
            section: RandomizerName,
            key: "Size Range",
            defaultValue: defaultPlayerSizeRange,
            configDescription: new ConfigDescription(
                description: "Randomize Hornet's size. Extreme sizes will prevent Hornet from going places.",
                acceptableValues: acceptablePlayerSizeRange,
                tags: new ConfigurationManagerAttributes
                {
                    Order = 1,
                    CustomDrawer = RangeDrawer
                }));
        patchHeroFSMs = config.Bind(
            section: RandomizerName,
            key: "Patch FSMs",
            defaultValue: defaultPatchHeroFSMs,
            configDescription: new ConfigDescription(
                description: "Enables QoL features for non-standard Hornet sizes.",
                tags: new ConfigurationManagerAttributes
                {
                    Order = 0
                }));
        playerSizeRando.SettingChanged += OnSettingsUpdated;
        playerSizeRange.SettingChanged += OnSettingsUpdated;

        playerSizeRando.SettingChanged += SettingMenu.OnRandomizerEnable;
        SettingMenu.UpdateSubMenuColor(playerSizeRando);

        if (playerSizeRando.Value)
        {
            Register();
        }
    }

    protected override void OnSettingsUpdated(object sender, EventArgs args)
    {
        if (((SettingChangedEventArgs)args).ChangedSetting.Definition.Key == "Randomize Hornets Size")
        {
            if ((bool)((SettingChangedEventArgs)args).ChangedSetting.BoxedValue)
                Register();
            else
                Unregister();
        }
        ResetAllLists();
        SetSize(true);

        // Try applying transpiler patch mid game if enabled after load
        if (PlayerSizeRando && !transpilerAttempted)
            TryTranspilerPatches();
    }

    #endregion Settings
}
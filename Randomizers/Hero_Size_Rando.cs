using System;
using System.Collections.Generic;
using System.Reflection.Emit;

using BepInEx.Configuration;

using HarmonyLib;

using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;

#if TESTING
using Newtonsoft.Json;
#endif
using Smol_Randomizer.Settings;

using UnityEngine;
using UnityEngine.SceneManagement;

using static Smol_Randomizer.Settings.Settings;

using Translate = HutongGames.PlayMaker.Actions.Translate;

namespace Smol_Randomizer.Randomizers;

internal class Hero_Size_Rando : Rando_Base
{
    /// <summary>
    /// We make a singleton of this rando
    /// </summary>
    private static readonly Lazy<Hero_Size_Rando> instance = new(() => new Hero_Size_Rando());
    /// <summary>
    /// Externally visible instance of this rando
    /// </summary>
    public static Hero_Size_Rando Instance => instance.Value;

    /// <summary>
    /// The base offset for the near clamber check
    /// </summary>
    private const float NEARCHECK = 0.37f;
    /// <summary>
    /// The base offset for the far clamber check
    /// </summary>
    private const float FARCHECK = 0.77f;
    /// <summary>
    /// The base offset for the clamber height check
    /// </summary>
    private const float HEIGHTCHECK = 0.67f;
    /// <summary>
    /// The layer mask for checing collisions
    /// </summary>
    private const int LAYERMASK = 8448;

    /// <summary>
    /// The scaled near check
    /// </summary>
    private float nearCheck = NEARCHECK;
    /// <summary>
    /// The scaled far check
    /// </summary>
    private float farCheck = FARCHECK;
    /// <summary>
    /// The scaled height check
    /// </summary>
    private float heightCheck = HEIGHTCHECK;

    /// <summary>
    /// True if we need to grab Hornet's base stats
    /// </summary>
    private static bool grabVariables = true;
    private static float hornetBaseDashSpeed;
    private static float hornetBaseSprintSpeed;
    private static float hornetBaseSprintStartSpeed;
    private static float hornetBaseQuickSpeed; // Flea brew?
    private static float hornetBaseQuickerSpeed; // Anklets?
    private static float hornetBaseSpeedToEnterUp;
    private static float hornetBaseSpeedToEnterHor;

    /// <summary>
    /// If we have adjusted Hornet's size
    /// </summary>
    private bool heroSizeChanged = false;
    /// <summary>
    /// Reference to Hornet's Transform
    /// </summary>
    private Transform? heroTransform;
    /// <summary>
    /// Reference to Hornet's Collider
    /// </summary>
    private Collider2D? heroCollider;
    /// <summary>
    /// Hornet's Size
    /// </summary>
    internal Vector3 heroSize = new(1f, 1f, 1f);
    /// <summary>
    /// Hornet's Size when facing Right
    /// </summary>
    internal Vector3 heroSizeFlipped = new(-1f, 1f, 1f);

    /// <summary>
    /// The Translate action within the Vault FSM state
    /// </summary>
    private static Translate mantleVaultTranslate;
    /// <summary>
    /// The YOffset float within Mantle FSM
    /// </summary>
    private static FsmFloat baseMantleVaultYOffset;
    /// <summary>
    /// Dictionary containing all the waterRegions in the current scene.
    /// </summary>
    private readonly Dictionary<SurfaceWaterRegion, float> waterRegions = [];

    /// <summary>
    /// If we have attempted to apply the transpiler
    /// </summary>
    private static bool transpilerAttempted = false;
    /// <summary>
    /// The current Scene
    /// </summary>
    private Scene currentScene;

    private readonly Dictionary<string, float> sceneHeroSize = [];
    private float saveHeroSize = float.MinValue;

    #region Randomizer_Info
    private Randomizer_Info eventOnSceneLoad;
    #endregion

    /// <summary>
    /// Constructor for this singleton
    /// </summary>
    private Hero_Size_Rando()
    {
        InitRandomizer();
    }

    private protected override void InitRandomizer()
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

        base.InitRandomizer();
    }

    private protected override void Register()
    {
        CuteRandoCore.RegisterRandomizer(eventOnSceneLoad);
    }

    private protected override void Unregister()
    {
        CuteRandoCore.UnregisterRandomizer(eventOnSceneLoad);
    }

#if TESTING // Enable Saving Data
    private protected override void ApplySaveData(Dictionary<string, object> savedData)
    {
        if (savedData.TryGetValue(nameof(heroSize), out object tempDict))
            heroSize = JsonConvert.DeserializeObject<Vector3>(tempDict.ToString());
    }

    private protected override void SetSaveData(Dictionary<string, object> savedData)
    {
        savedData[nameof(heroSize)] = heroSize;
    }

    private protected override void OnSettingsSaved()
    {
        Dictionary<string, object> savedData = Settings.Settings.SaveData.GetSavedData(RandomizerName);

        savedData[nameof(heroSize)] = heroSize;
    }
#endif

    /// <summary>
    /// Patch HealthManager.OnEnable on game startup
    /// </summary>
    private void GameStartup()
    {
        Type randoType = typeof(Hero_Size_Rando);

        CuteRandoCore.harmony.Patch(AccessTools.Method(
            typeof(HeroController), nameof(HeroController.FaceRight)),
            postfix: new HarmonyMethod(randoType, nameof(HeroController_FaceRight_Postfix)));
        CuteRandoCore.harmony.Patch(AccessTools.Method(
            typeof(HeroController), nameof(HeroController.FaceLeft)),
            postfix: new HarmonyMethod(randoType, nameof(HeroController_FaceLeft_Postfix)));
        CuteRandoCore.harmony.Patch(AccessTools.Method(
            typeof(HeroController), "Update10"),
            postfix: new HarmonyMethod(randoType, nameof(HeroController_Update10_Postfix)));
        CuteRandoCore.harmony.Patch(AccessTools.EnumeratorMoveNext(
            AccessTools.Method(
                typeof(HeroController), "EnterHeroSubHorizontal")),
            postfix: new HarmonyMethod(randoType, nameof(HeroController_EnterHeroSubHorizontal_Postfix)));
        CuteRandoCore.harmony.Patch(AccessTools.Method(
            typeof(SurfaceWaterRegion), "Start"),
            postfix: new HarmonyMethod(randoType, nameof(SurfaceWaterRegion_Start_Postfix)));
        CuteRandoCore.harmony.Patch(
            AccessTools.Method(typeof(DamageHero), "OnEnable"),
            postfix: new HarmonyMethod(randoType, nameof(DamageHero_OnEnable_Postfix)));

        CuteRandoCore.harmony.Patch(AccessTools.Method(
            typeof(SetScale), "DoSetScale"),
            postfix: new HarmonyMethod(randoType, nameof(SetScale_DoSetScale_Postfix)));

#if !TESTING
        if (PlayerSizeRando) // only patch if rando is enabled
            TryTranspilerPatch();
#endif

#if TESTING
        CuteRandoCore.harmony.Patch(AccessTools.Method(
            typeof(HeroController), nameof(HeroController.CheckClamberLedge)),
            postfix: new HarmonyMethod(typeof(Hero_Size_Rando), nameof(HeroController_CheckClamberLedge_Postfix)));
        debugPoints = new GameObject("CuteRandoDebugPoints", []);

        heroVectorCollider = debugPoints.AddComponent<CircleCollider2D>();
        heroVectorAboveCollider = debugPoints.AddComponent<CircleCollider2D>();
        heroVectorToAboveLine = debugPoints.AddComponent<EdgeCollider2D>();
        farOriginCollider = debugPoints.AddComponent<CircleCollider2D>();
        heroVectorAboveToFarLine = debugPoints.AddComponent<EdgeCollider2D>();
        nearOriginCollider = debugPoints.AddComponent<CircleCollider2D>();
        heroVectorAboveToNearLine = debugPoints.AddComponent<EdgeCollider2D>();
        farGoodCollider = debugPoints.AddComponent<CircleCollider2D>();
        nearGoodCollider = debugPoints.AddComponent<CircleCollider2D>();
        farOriginToFarGoodLine = debugPoints.AddComponent<EdgeCollider2D>();
        nearOriginToNearGoodLine = debugPoints.AddComponent<EdgeCollider2D>();
        farHitPointCollider = debugPoints.AddComponent<CircleCollider2D>();
        nearHitPointCollider = debugPoints.AddComponent<CircleCollider2D>();


        foreach (Collider2D collider in debugPoints.GetComponents<Collider2D>())
        {
            if (collider is CircleCollider2D circle)
            {
                circle.radius = colliderRadius;
            }
            collider.enabled = true;
            collider.isTrigger = true;
            UnityEngine.Object.DontDestroyOnLoad(collider);
        }

        UnityEngine.Object.DontDestroyOnLoad(debugPoints);
#endif
    }

    /// <summary>
    /// Attempts to apply the transpiler patch. Can only be called once, otherwise silently aborts.
    /// </summary>
    private static void TryTranspilerPatch()
    {
        if (transpilerAttempted) return;

        try
        {
            CuteRandoCore.harmony.Patch(AccessTools.Method(
            typeof(HeroController), nameof(HeroController.CheckClamberLedge)),
            transpiler: new HarmonyMethod(typeof(Hero_Size_Rando), nameof(HeroController_CheckClamberLedge_Transpiler)));
        }
        catch (Exception ex)
        {
            CuteRandoCore.Log.LogError($"Unable to patch CheckClamberLedge with transpiler, disabling Hero Size Rando. Restart Silksong please and leave it disabled.\nPlease then open a GitHub Issue report for this mod.\nException: {ex.Message}\nStack Trace:{ex.StackTrace}");
            Instance.PlayerSizeRando = false;
        }
        finally
        {
            transpilerAttempted = true;
        }
    }


    /// <summary>
    /// Patch to hook DeSetScale and fix the Y scale of hornet
    /// </summary>
    /// <param name="__instance"></param>
    private static void SetScale_DoSetScale_Postfix(ref SetScale __instance)
    {
        if (__instance.Owner.name.Contains("Knight Spike Death"))
        {
            GameObject gameObject = __instance.Fsm.GetOwnerDefaultTarget(__instance.gameObject);
            gameObject.transform.SetScaleY(gameObject.transform.GetScaleX());
        }
    }

    /// <summary>
    /// Patch to watch for when Hornet gets damaged
    /// </summary>
    private static void DamageHero_OnEnable_Postfix(ref DamageHero __instance)
    {
        if (Instance.PlayerSizeRando && Instance.HeroSizeConsistency == RandomizerConsistencyC.OnDamageTaken)
           __instance.OnDamagedHero.AddListener(HeroDamaged);

    }

    /// <summary>
    /// Called when DamageHero fires OnDamagedHero
    /// </summary>
    private static void HeroDamaged()
    {
        Instance.SetSize(true);
    }

    /// <summary>
    /// Patch to hook FaceRight that would set Hornet's horizontal scale to -1
    /// </summary>
    /// <param name="___transform">Hornet's Transform</param>
    private static void HeroController_FaceRight_Postfix(ref Transform ___transform)
    {
        if (!Instance.PlayerSizeRando) return;

        ___transform.localScale = Instance.heroSizeFlipped;
    }

    /// <summary>
    /// Patch to hook FaceLeft that would set Hornet's horizontal scale to 1
    /// </summary>
    /// <param name="___transform">Hornet's Transform</param>
    private static void HeroController_FaceLeft_Postfix(ref Transform ___transform)
    {
        if (!Instance.PlayerSizeRando) return;


        ___transform.localScale = Instance.heroSize;
    }

    /// <summary>
    /// Patch to hook Update10 that is run every 10 frames which would change Hornet's horizontal scale back to default
    /// </summary>
    /// <param name="___transform">Hornet's Transform</param>
    private static void HeroController_Update10_Postfix(ref Transform ___transform)
    {
        if (!Instance.PlayerSizeRando) return;

        ___transform.SetScaleX(___transform.GetScaleX() > 0 ? Instance.heroSize.x : Instance.heroSizeFlipped.x);
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

        // We take the scale, subtract 1 from it, multiply by the magic number, then add it to the position and add 0.005f to get it closer to level
        transform.localPosition = transform.localPosition with { y = ((transform.localScale.y - 1) * 0.513f) + transform.localPosition.y + 0.005f }; // Magic number came from spreadsheeting various scales and figuring out the right number through that

        transitionHandled = true;
    }
    #region LedgeClambering
#if TESTING

    #region TestingColliders
    // This GameObject and colliders are used to debug the various clamber checks

    private const float colliderRadius = 0.25f;
    private static GameObject debugPoints;
    private static CircleCollider2D heroVectorCollider;
    private static CircleCollider2D heroVectorAboveCollider;
    private static EdgeCollider2D heroVectorToAboveLine;
    private static CircleCollider2D farOriginCollider;
    private static EdgeCollider2D heroVectorAboveToFarLine;
    private static CircleCollider2D nearOriginCollider;
    private static EdgeCollider2D heroVectorAboveToNearLine;
    private static CircleCollider2D farGoodCollider;
    private static EdgeCollider2D farOriginToFarGoodLine;
    private static CircleCollider2D nearGoodCollider;
    private static EdgeCollider2D nearOriginToNearGoodLine;
    private static CircleCollider2D farHitPointCollider;
    private static CircleCollider2D nearHitPointCollider;

    /// <summary>
    /// Set the position of all colliders to 0,0
    /// </summary>
    private static void ClearAllColliders()
    {
        heroVectorCollider.offset = Vector2.zero;
        heroVectorAboveCollider.offset = Vector2.zero;
        heroVectorToAboveLine.points = [];
        farOriginCollider.offset = Vector2.zero;
        heroVectorAboveToFarLine.points = [];
        nearOriginCollider.offset = Vector2.zero;
        heroVectorAboveToNearLine.points = [];
        farGoodCollider.offset = Vector2.zero;
        nearGoodCollider.offset = Vector2.zero;
        farOriginToFarGoodLine.points = [];
        nearOriginToNearGoodLine.points = [];
        farHitPointCollider.offset = Vector2.zero;
        nearHitPointCollider.offset = Vector2.zero;
    }
    #endregion

    /// <summary>
    /// Patch that hooks the end of CheckClamberLedge to correctly check depending on hornet's scale. This probably can be turned into a transpiler.
    /// </summary>
    /// <param name="__instance">Hornet</param>
    /// <param name="__result">The result of the check</param>
    /// <param name="___col2d">Hornet's collider</param>
    /// <param name="y">The y position that she will land at</param>
    /// <param name="clamberedCollider">What Hornet is clambering onto</param>
    private static void HeroController_CheckClamberLedge_Postfix(ref HeroController __instance, ref bool __result, ref Collider2D ___col2d, ref float y, ref Collider2D clamberedCollider)
    {
        if (/*__result || */!Instance.PlayerSizeRando) return; // We are already going to clamber, or we aren't randomizing
        // Don't check for roof, other checks cover that later in method
        if (Instance.heroTransform == null || NoClamberRegion.IsClamberBlocked/* || __instance.CheckNearRoof()*/)
            return;
        /* Hornet's Default Size:
         *  Width: 0.25
         *  Height: 1.04
        */

        bool facingRight = __instance.cState.facingRight;

        float near = Instance.nearCheck;
        float far = Instance.farCheck;
        float height = Instance.heightCheck;
        Vector2 vector = Instance.heroTransform.position;
        Vector2 heightOffset = new(0f, height);
        Vector2 facingDirection = facingRight ? Vector2.right : Vector2.left;
        Vector2 farOrigin = vector + new Vector2(facingRight ? far : far * -1, height);
        Vector2 nearOrigin = vector + new Vector2(facingRight ? near : near * -1, height);

        // Hitboxes for debugging
        if (DebugMod.Hitbox.HitboxRender.Instance != null)
            DebugMod.Hitbox.HitboxRender.Instance.UpdateHitbox(debugPoints);

        ClearAllColliders();

        // Hitboxes for initial checks
        heroVectorCollider.offset = vector;
        heroVectorAboveCollider.offset = vector + heightOffset;
        heroVectorToAboveLine.points = [heroVectorCollider.offset, heroVectorAboveCollider.offset];
        farOriginCollider.offset = farOrigin;
        heroVectorAboveToFarLine.points = [heroVectorAboveCollider.offset, farOriginCollider.offset];
        nearOriginCollider.offset = nearOrigin;
        heroVectorAboveToNearLine.points = [heroVectorAboveCollider.offset, nearOriginCollider.offset];

        if (Helper.IsRayHittingNoTriggers(vector + heightOffset, facingDirection, 0.75f, LAYERMASK)) // Directly Above, different than CheckNearRoof, probably allows for clambering through a gap?
            return;

        bool farGood = Helper.IsRayHittingNoTriggers(farOrigin, Vector2.down, 2.26f, LAYERMASK, out var closestFarHit);
        bool nearGood = Helper.IsRayHittingNoTriggers(nearOrigin, Vector2.down, 2.26f, LAYERMASK, out var closestNearHit);

        // Hitboxes for landing points
        farGoodCollider.offset = closestFarHit.point;
        nearGoodCollider.offset = closestNearHit.point;
        if (farGood)
            farOriginToFarGoodLine.points = [farOriginCollider.offset, farGoodCollider.offset];
        if (nearGood)
            nearOriginToNearGoodLine.points = [nearOriginCollider.offset, nearGoodCollider.offset];

        if (!farGood || !nearGood) // One or both are not good
            return;

        Vector2 farHitPoint = closestFarHit.point;
        Vector2 nearHitPoint = closestNearHit.point;
        clamberedCollider = closestNearHit.collider;

        farHitPoint.y += 0.1f;
        nearHitPoint.y += 0.1f;

        bool farHitPointGood = !Helper.IsRayHittingNoTriggers(farHitPoint, Vector2.up, 2.16f, LAYERMASK, out var closestFarPointHit);
        bool nearHitPointGood = !Helper.IsRayHittingNoTriggers(nearHitPoint, Vector2.up, 2.16f, LAYERMASK, out var closestNearPointHit);

        farHitPointCollider.offset = closestFarPointHit.point;
        nearHitPointCollider.offset = closestNearPointHit.point;

        if (!farHitPointGood || !nearHitPointGood) // Above Landing Spot
            return;

        if (!farHitPoint.y.IsWithinTolerance(0.1f, nearHitPoint.y))
        {
            return;
        }

        Vector2 n = new(vector.x, ___col2d.bounds.min.y + 0.2f);

        if (Helper.IsRayHittingNoTriggers(n, Vector2.down, 2f, LAYERMASK, out var closestHit3) && farHitPoint.y - closestHit3.point.y < 1.5f)
        {
            return;
        }
        y = farHitPoint.y;
        __result = true;
    }
#endif

    /// <summary>
    /// Patches the ledge clamber check to allow for scaling.
    /// </summary>
    /// <param name="instructions"></param>
    /// <param name="ilGenerator"></param>
    /// <returns></returns>
    private static IEnumerable<CodeInstruction> HeroController_CheckClamberLedge_Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator ilGenerator)
    {
        var playerSizeRando = AccessTools.Method(typeof(Hero_Size_Rando), "get_PlayerSizeRando");
        var heroSizeRandoInstance = AccessTools.Method(typeof(Hero_Size_Rando), "get_Instance");
        var checkNearRoof = AccessTools.Method(typeof(HeroController), "CheckNearRoof");

        var nearCheck = AccessTools.Field(typeof(Hero_Size_Rando), "nearCheck");
        var farCheck = AccessTools.Field(typeof(Hero_Size_Rando), "farCheck");
        var heightCheck = AccessTools.Field(typeof(Hero_Size_Rando), "heightCheck");

        LocalBuilder near = ilGenerator.DeclareLocal(typeof(float));
        LocalBuilder far = ilGenerator.DeclareLocal(typeof(float));
        LocalBuilder height = ilGenerator.DeclareLocal(typeof(float));

        List<CodeInstruction> instructionList = [.. instructions];

        /* Check to see if player size rando is enabled, if so we want to skip the roof check
         * 
         * if (Instance.PlayerSizeRando) {
         *     goto [AfterCheckNearRoof]
         * }
         */
        List<CodeInstruction> playerSizeRandoEnabled =
            [
                new(OpCodes.Call, heroSizeRandoInstance), // Get the instance of Hero_Size_Rando
                new(OpCodes.Callvirt, playerSizeRando), // Gets the setting if the rando is enabled or not
                new(OpCodes.Ldc_I4_1), // Load one (true)
                new(OpCodes.Ceq), // Compare the setting to the loaded value
                new(OpCodes.Brtrue_S) // Branch to a label that we will grab later if the two values were the same
            ];

        /* Setup our local variables
         * 
         * float near = Instance.nearCheck;
         * float far = Instance.farCheck;
         * float height = Instance.heightCheck;
         * Vector2 heightOffset = new(0f, height);
         */
        List<CodeInstruction> createVariables =
            [
                new(OpCodes.Call, heroSizeRandoInstance), // Get the instance of Hero_Size_Rando
                new(OpCodes.Ldfld, nearCheck), // Get the value of nearCheck
                new(OpCodes.Stloc_S, near), // Saves the value to a local variable
                new(OpCodes.Call, heroSizeRandoInstance), // Get the instance of Hero_Size_Rando
                new(OpCodes.Ldfld, farCheck), // Get the value of farCheck
                new(OpCodes.Stloc_S, far), // Saves the value to a local variable
                new(OpCodes.Call, heroSizeRandoInstance), // Get the instance of Hero_Size_Rando
                new(OpCodes.Ldfld, heightCheck), // Get the value of heightCheck
                new(OpCodes.Stloc_S, height) // Saves the value to a local variable
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

            // Patch so that CheckNearRoof is only called when the rando is disabled
            if (i < instructionList.Count - 2 && instructionList[i + 1].OperandIs(checkNearRoof))
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
            if(i< instructionList.Count - 2 && instructionList[i + 2].operand is float f && f == 2.16f)
            {
                // Get the instance of Hero_Size_Rando
                /*if (instruction.labels.Count > 0)
                {
                    yield return new CodeInstruction(OpCodes.Call, heroSizeRandoInstance).MoveLabelsFrom(instruction);
                }
                else
                    yield return new(OpCodes.Call, heroSizeRandoInstance);
                yield return new(OpCodes.Callvirt, playerSizeRando); // Gets the setting if the rando is enabled or not
                yield return new(OpCodes.Ldc_I4_1); // Load one (true)
                yield return new(OpCodes.Ceq); // Compare the setting to the loaded value
                yield return new(OpCodes.Brtrue_S, instructionList[i+5].operand); // Branch to a label that we will grab*/


                // Reset List
                for (int j = 0; j < playerSizeRandoEnabled.Count; j++) {    
                    playerSizeRandoEnabled[j] = new(playerSizeRandoEnabled[j].opcode, playerSizeRandoEnabled[j].operand);
                    if (playerSizeRandoEnabled[j].opcode == OpCodes.Brtrue) // No idea why it randomly switches
                        playerSizeRandoEnabled[j].opcode = OpCodes.Brtrue_S;
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

            yield return instruction;
        }
    }
    #endregion

    #region WaterSurfaces
    /// <summary>
    /// Patch that hooks SurfaceWaterRegion's Start method to adjust the collider and adds it to a list so we can update it mid scene if needed
    /// </summary>
    /// <param name="__instance">The SurfaceWaterRegion we are adjusting</param>
    private static void SurfaceWaterRegion_Start_Postfix(ref SurfaceWaterRegion __instance)
    {
        float offset = __instance.GetComponent<BoxCollider2D>().offset.y;
        Instance.waterRegions.Add(__instance, offset);
        UpdateWaterSurface(__instance, Instance.heroSize.x, offset);
    }

    /// <summary>
    /// Update all the water surfaces in the current scene
    /// </summary>
    private static void UpdateWaterSurfaces()
    {
        foreach (var region in Instance.waterRegions)
        {
            UpdateWaterSurface(region.Key, Instance.heroSize.x, region.Value);
        }
    }

    /// <summary>
    /// Update the given water surface region.
    /// </summary>
    /// <param name="region">The region to update</param>
    /// <param name="multiplier">The multiplier to use, should be Hornet's size</param>
    /// <param name="offset">The water collider's height to be scaled</param>
    private static void UpdateWaterSurface(SurfaceWaterRegion region, float multiplier, float offset)
    {
        BoxCollider2D col = region.GetComponent<BoxCollider2D>();

        col.offset = col.offset with { y = offset + (1.04f - (1.04f * multiplier)) + (0.4f - (0.4f * multiplier)) };
    }
    #endregion

    private void OnSceneLoad(Scene scene, LoadSceneMode _)
    {
        currentScene = scene;
        SetSize();
        waterRegions.Clear();
    }

    // TODO: damage from hazard spikes does weird scaling
    /// <summary>
    /// Sets the size of Hornet
    /// </summary>
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
        if (heroTransform == null || heroCollider == null)
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
                if (currentScene == null)
                    return;
                if (!sceneHeroSize.TryGetValue(currentScene.name, out multiplier))
                {
                    multiplier = CuteRandoCore.RandoHelper(
                        PlayerSizeRange.AsTuple(),
                        CuteRandoCore.RNGSeed(currentScene.name));
                    sceneHeroSize[currentScene.name] = multiplier;
                }
                break;
            case RandomizerConsistencyC.PerSaveFile:
                if (saveHeroSize.Equals(float.MinValue))
                {
                    saveHeroSize = CuteRandoCore.RandoHelper(PlayerSizeRange.AsTuple(), SaveData.SaveSeed);
                }
                multiplier = saveHeroSize;
                break;
            case RandomizerConsistencyC.OnDamageTaken:
                if (!damageTaken) return;

                multiplier = CuteRandoCore.RandoHelper(PlayerSizeRange.AsTuple());
                break;
            default:
                throw new NotImplementedException();
        }

        SetVariables(multiplier);
        heroSizeChanged = true;
    }

    /// <summary>
    /// Sets Hornet's variables according to the given multiplier.
    /// </summary>
    /// <param name="multiplier">Hornet's scale</param>
    private void SetVariables(float multiplier)
    {
        if (heroTransform == null) return;

        Instance.heroSize = new(multiplier, multiplier, 1f);
        Instance.heroSizeFlipped = Instance.heroSize with { x = Instance.heroSize.x * -1 };

        // Divide so that when scaled, Hornet runs the same speed as her normal size
        HeroController.instance.sprintFSM.FsmVariables.FindFsmFloat("Dash Speed").Value = hornetBaseDashSpeed / multiplier;
        HeroController.instance.sprintFSM.FsmVariables.FindFsmFloat("Sprint Speed Regular").Value = hornetBaseSprintSpeed / multiplier;
        HeroController.instance.sprintFSM.FsmVariables.FindFsmFloat("Sprint Start Speed").Value = hornetBaseSprintStartSpeed / multiplier;
        HeroController.instance.sprintFSM.FsmVariables.FindFsmFloat("Sprint Speed Quick").Value = hornetBaseQuickSpeed / multiplier;
        HeroController.instance.sprintFSM.FsmVariables.FindFsmFloat("Sprint Speed Quicker").Value = hornetBaseQuickerSpeed / multiplier;

        mantleVaultTranslate.y = baseMantleVaultYOffset.Value * multiplier;

        CuteRandoCore.TraverseHelper(HeroController.instance, "SPEED_TO_ENTER_SCENE_UP").SetValue((hornetBaseSpeedToEnterHor * Instance.heroSize.x) + (0.2f - (0.2f * Instance.heroSize.x)));
        CuteRandoCore.TraverseHelper(HeroController.instance, "SPEED_TO_ENTER_SCENE_UP").SetValue((hornetBaseSpeedToEnterUp * Instance.heroSize.y) + (0.2f - (0.2f * Instance.heroSize.y)));

        heroTransform.localScale = heroTransform.localScale.x > 0f ? Instance.heroSize : Instance.heroSizeFlipped;

        nearCheck = NEARCHECK * multiplier;
        farCheck = FARCHECK * multiplier;
        heightCheck = HEIGHTCHECK * multiplier;

        UpdateWaterSurfaces();
    }

    /// <summary>
    /// Save Hornet's base values
    /// </summary>
    /// <returns>True if it succeeded, false if it did not</returns>
    private bool SaveVariables()
    {
        try
        {
            heroTransform = CuteRandoCore.TraverseHelper(HeroController.instance, "transform").GetValue() as Transform;
            heroCollider = CuteRandoCore.TraverseHelper(HeroController.instance, "col2d").GetValue() as Collider2D;

            if (heroTransform == null || heroCollider == null) return false; // Something is wrong here, abort

            if (grabVariables)
            {
                hornetBaseDashSpeed = HeroController.instance.sprintFSM.FsmVariables.FindFsmFloat("Dash Speed").Value;
                hornetBaseSprintSpeed = HeroController.instance.sprintFSM.FsmVariables.FindFsmFloat("Sprint Speed Regular").Value;
                hornetBaseSprintStartSpeed = HeroController.instance.sprintFSM.FsmVariables.FindFsmFloat("Sprint Start Speed").Value;
                hornetBaseQuickSpeed = HeroController.instance.sprintFSM.FsmVariables.FindFsmFloat("Sprint Speed Quick").Value;
                hornetBaseQuickerSpeed = HeroController.instance.sprintFSM.FsmVariables.FindFsmFloat("Sprint Speed Quicker").Value;

                hornetBaseSpeedToEnterHor = (float)CuteRandoCore.TraverseHelper(HeroController.instance, "SPEED_TO_ENTER_SCENE_HOR").GetValue();
                hornetBaseSpeedToEnterUp = (float)CuteRandoCore.TraverseHelper(HeroController.instance, "SPEED_TO_ENTER_SCENE_UP").GetValue();

                foreach (var state in HeroController.instance.mantleFSM.FsmStates)
                {
                    if (state.Name == "Vault")
                    {
                        foreach (var action in state.Actions)
                            if (action.GetType() == typeof(Translate))
                            {
                                mantleVaultTranslate = (Translate)action;
                                baseMantleVaultYOffset = mantleVaultTranslate.y;
                            }
                    }
                }

                grabVariables = false;
            }
            return true;
        }
        catch (Exception ex)
        {
            CuteRandoCore.Log.LogError($"Failed to capture Hero variables, disabling {RandomizerName}.\nException: {ex.Message}\nStack Trace:\n{ex.StackTrace}");
            PlayerSizeRando = false;
            grabVariables = false;
            return false;
        }
    }

    /// <summary>
    /// Returns Hornet's values to default
    /// </summary>
    private void ReturnToDefault()
    {
        if (!heroSizeChanged || heroTransform == null)
            return;

        heroTransform.localScale = heroTransform.localScale.x > 0f ? new(1f, 1f, 1f) : new(-1f, 1f, 1f);
        HeroController.instance.sprintFSM.FsmVariables.FindFsmFloat("Dash Speed").Value = hornetBaseDashSpeed;
        HeroController.instance.sprintFSM.FsmVariables.FindFsmFloat("Sprint Speed Regular").Value = hornetBaseSprintSpeed;
        HeroController.instance.sprintFSM.FsmVariables.FindFsmFloat("Sprint Start Speed").Value = hornetBaseSprintStartSpeed;
        HeroController.instance.sprintFSM.FsmVariables.FindFsmFloat("Sprint Speed Quick").Value = hornetBaseQuickSpeed;
        HeroController.instance.sprintFSM.FsmVariables.FindFsmFloat("Sprint Speed Quicker").Value = hornetBaseQuickerSpeed;

        CuteRandoCore.TraverseHelper(typeof(HeroController), "SPEED_TO_ENTER_SCENE_HOR").SetValue(hornetBaseSpeedToEnterHor);
        CuteRandoCore.TraverseHelper(typeof(HeroController), "SPEED_TO_ENTER_SCENE_UP").SetValue(hornetBaseSpeedToEnterUp);

        mantleVaultTranslate.y = baseMantleVaultYOffset;

        heroSize = new(1f, 1f, 1f);

        nearCheck = NEARCHECK;
        farCheck = FARCHECK;
        heightCheck = HEIGHTCHECK;

        UpdateWaterSurfaces();

        heroSizeChanged = false;
    }

    private protected override void ResetAllLists()
    {
        sceneHeroSize.Clear();
        saveHeroSize = float.MinValue;
    }

    #region Settings
    /// <summary>
    /// Setting for if player size should be randomized
    /// </summary>
    public bool PlayerSizeRando
    {
        get => playerSizeRando.Value;
        internal set => playerSizeRando.Value = value;
    }
    private ConfigEntry<bool> playerSizeRando;
    /// <summary>
    /// Default choice for if player size should be randomized
    /// </summary>
    public const bool defaultPlayerSizeRando = false;
    /// <summary>
    /// Setting for how conistent Hornet's size should be
    /// </summary>
    public RandomizerConsistencyC HeroSizeConsistency
    {
        get => playerSizeConsistency.Value;
        internal set => playerSizeConsistency.Value = value;
    }
    private ConfigEntry<RandomizerConsistencyC> playerSizeConsistency;
    /// <summary>
    /// Default consistency of Hornet's size
    /// </summary>
    public const RandomizerConsistencyC defaultHeroSizeConsistency = RandomizerConsistencyC.PerSaveFile;
    /// <summary>
    /// Setting for the range that the hero's size can be randomized to
    /// </summary>
    public FloatRange PlayerSizeRange
    {
        get => playerSizeRange.Value;
        internal set => playerSizeRange.Value = value;
    }
    private ConfigEntry<FloatRange> playerSizeRange;
    /// <summary>
    /// Default range for the hero's size
    /// </summary>
    public readonly FloatRange defaultPlayerSizeRange = new(0.68f, 1.35f);
    /// <summary>
    /// Acceptable value range for the hero's size
    /// </summary>
    public static readonly AcceptableRangeforFloatRange acceptablePlayerSizeRange = new(0.25f, 2f);

    private protected override void InitSettings()
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
                    Order = 2
                }));
        playerSizeConsistency = config.Bind(
            section: RandomizerName,
            key: "Size Change Trigger",
            defaultValue: defaultHeroSizeConsistency,
            configDescription: new ConfigDescription(
                description: "When Hornet's size will change.",
                tags: new ConfigurationManagerAttributes
                {
                    Order = 1
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
                    Order = 0,
                    CustomDrawer = RangeDrawer
                }));
        playerSizeRando.SettingChanged += OnSettingsUpdated;
        playerSizeRange.SettingChanged += OnSettingsUpdated;

        playerSizeRando.SettingChanged += SettingMenu.OnRandomizerEnable;
        SettingMenu.UpdateSubMenuColor(playerSizeRando);
    }

    private protected override void OnSettingsUpdated(object sender, EventArgs args)
    {
        ResetAllLists();
        SetSize(true);

#if !TESTING
        // Try applying transpiler patch mid game if enabled after load
        if (PlayerSizeRando && !transpilerAttempted)
            TryTranspilerPatch();
#endif
    }
    #endregion
}

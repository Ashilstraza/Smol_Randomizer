using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;

using BepInEx.Configuration;

using HarmonyLib;

using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;

#if TESTING
using Newtonsoft.Json;
#endif

using Smol_Randomizer.FSMThings;
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
    /// The layer mask for checing collisions
    /// </summary>
    private const int LAYERMASK = 8448;

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
    private Transform heroTransform;
    /// <summary>
    /// Reference to Hornet's Collider
    /// </summary>
    private BoxCollider2D heroCollider;
    /// <summary>
    /// Hornet's Scale
    /// </summary>
    internal Vector3 heroScale = new(1f, 1f, 1f);
    /// <summary>
    /// Hornet's Scale when facing Right
    /// </summary>
    internal Vector3 heroScaleFlipped = new(-1f, 1f, 1f);
    /// <summary>
    /// Hornet's default Collider Size
    /// </summary>
    internal Vector2 defaultColliderSize;
    /// <summary>
    /// Hornet's default Collider Offset
    /// </summary>
    internal Vector2 defaultColliderOffset;

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

    private protected override void OnLoaded()
    {
        grabVariables = true;
        heroSizeChanged = false;
    }

#if TESTING // Enable Saving Data
    private protected override void ApplySaveData(Dictionary<string, object> savedData)
    {
        if (savedData.TryGetValue(nameof(heroScale), out object tempDict))
            heroScale = JsonConvert.DeserializeObject<Vector3>(tempDict.ToString());
    }

    private protected override void SetSaveData(Dictionary<string, object> savedData)
    {
        savedData[nameof(heroScale)] = heroScale;
    }

    private protected override void OnSettingsSaved()
    {
        Dictionary<string, object> savedData = SaveData.GetSavedData(RandomizerName);

        savedData[nameof(heroScale)] = heroScale;
    }
#endif

    /// <summary>
    /// Patch HealthManager.OnEnable on game startup
    /// </summary>
    private void GameStartup()
    {
        Type randoType = typeof(Hero_Size_Rando);
        Type heroControllerType = typeof(HeroController);

        // Basic Scale Patches
        CuteRandoCore.harmony.Patch(AccessTools.Method(
            heroControllerType, nameof(HeroController.FaceRight)),
            postfix: new HarmonyMethod(randoType, nameof(HeroController_FaceRight_Postfix)));
        CuteRandoCore.harmony.Patch(AccessTools.Method(
            heroControllerType, nameof(HeroController.FaceLeft)),
            postfix: new HarmonyMethod(randoType, nameof(HeroController_FaceLeft_Postfix)));
        CuteRandoCore.harmony.Patch(AccessTools.Method(
            heroControllerType, "Update10"),
            postfix: new HarmonyMethod(randoType, nameof(HeroController_Update10_Postfix)));
        CuteRandoCore.harmony.Patch(AccessTools.EnumeratorMoveNext(
            AccessTools.Method(
                heroControllerType, "EnterHeroSubHorizontal")),
                postfix: new HarmonyMethod(randoType, nameof(HeroController_EnterHeroSubHorizontal_Postfix)));
        CuteRandoCore.harmony.Patch(
            AccessTools.Method(typeof(DamageHero), "OnEnable"),
            postfix: new HarmonyMethod(randoType, nameof(DamageHero_OnEnable_Postfix)));
        CuteRandoCore.harmony.Patch(AccessTools.EnumeratorMoveNext(
            AccessTools.Method(
                typeof(NPCControlBase), "MovePlayer")),
                postfix: new HarmonyMethod(randoType, nameof(MovePlayer_NPCControllerBase_Postfix)));
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

        // Ledge Clambering
#if !TESTING
        if (PlayerSizeRando) // only patch if rando is enabled
            TryTranspilerPatch();
#endif

#if TESTING
        CuteRandoCore.harmony.Patch(AccessTools.Method(
            heroControllerType, nameof(HeroController.CheckClamberLedge)),
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
        farHPCtoHPCCLine = debugPoints.AddComponent<EdgeCollider2D>();
        nearHPCtoHPCCLine = debugPoints.AddComponent<EdgeCollider2D>();
        farHitPointColliderCheck = debugPoints.AddComponent<CircleCollider2D>();
        nearHitPointColliderCheck = debugPoints.AddComponent<CircleCollider2D>();


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

    #region Basic Scale Patches
    /// <summary>
    /// Patch to hook MovePlayer in NPCControllerBase to correct usage of using -1 or 1 for setting scale
    /// </summary>
    private static void MovePlayer_NPCControllerBase_Postfix()
    {
        if (!Instance.PlayerSizeRando || Instance.heroTransform == null) return;
        float x = Instance.heroTransform.localScale.x;
        Instance.heroTransform.SetScaleX(x > 0 ? Instance.heroScale.x : Instance.heroScaleFlipped.x);
    }


    /// <summary>
    /// Patch to hook DeSetScale and fix the Y scale of hornet
    /// </summary>
    /// <param name="__instance"></param>
    private static void SetScale_DoSetScale_Postfix(ref SetScale __instance)
    {
        if (Instance.PlayerSizeRando && __instance.Owner.name.Contains("Knight Spike Death"))
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

        ___transform.localScale = Instance.heroScaleFlipped;
    }

    /// <summary>
    /// Patch to hook FaceLeft that would set Hornet's horizontal scale to 1
    /// </summary>
    /// <param name="___transform">Hornet's Transform</param>
    private static void HeroController_FaceLeft_Postfix(ref Transform ___transform)
    {
        if (!Instance.PlayerSizeRando) return;


        ___transform.localScale = Instance.heroScale;
    }

    /// <summary>
    /// Patch to hook Update10 that is run every 10 frames which would change Hornet's horizontal scale back to default
    /// </summary>
    /// <param name="___transform">Hornet's Transform</param>
    private static void HeroController_Update10_Postfix(ref Transform ___transform)
    {
        if (!Instance.PlayerSizeRando) return;

        ___transform.SetScaleX(___transform.GetScaleX() > 0 ? Instance.heroScale.x : Instance.heroScaleFlipped.x);
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
    #endregion

    #region Ledge Clambering
#if TESTING

    #region Testing Colliders
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
    private static EdgeCollider2D farHPCtoHPCCLine;
    private static EdgeCollider2D nearHPCtoHPCCLine;
    private static CircleCollider2D farHitPointColliderCheck;
    private static CircleCollider2D nearHitPointColliderCheck;

    private static bool debugGoodCollider = true;
    private static bool debugOriginCollider = true;
    private static bool debugHitPointCollider = true;

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
        farHPCtoHPCCLine.points = [];
        nearHPCtoHPCCLine.points = [];
        farHitPointColliderCheck.offset = Vector2.zero;
        nearHitPointColliderCheck.offset = Vector2.zero;
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

        // Hitboxes for debugging
        if (DebugMod.Hitbox.HitboxRender.Instance != null)
            DebugMod.Hitbox.HitboxRender.Instance.UpdateHitbox(debugPoints);

        ClearAllColliders();

        // Hitboxes for initial checks
        heroVectorCollider.offset = vector;
        

        if (debugOriginCollider)
        {
            farOriginCollider.offset = farOrigin;
            heroVectorAboveToFarLine.points = [vector + heightOffset, farOriginCollider.offset];
            nearOriginCollider.offset = nearOrigin;
            heroVectorAboveToNearLine.points = [vector + heightOffset, nearOriginCollider.offset];
        }
        
        if (Helper.IsRayHittingNoTriggers(vector + heightOffset, facingDirection, 0.75f, LAYERMASK)) // Directly Above, different than CheckNearRoof, probably allows for clambering through a gap?
            return;

        bool farGood = Helper.IsRayHittingNoTriggers(farOrigin, Vector2.down, ceiling, LAYERMASK, out var closestFarHit);
        bool nearGood = Helper.IsRayHittingNoTriggers(nearOrigin, Vector2.down, ceiling, LAYERMASK, out var closestNearHit);

        bool heightGood = !Helper.IsRayHittingNoTriggers(new(vector.x, closestNearHit.point.y), Vector2.up, ceiling, LAYERMASK, out var heightHit);

        heroVectorAboveCollider.offset = heightHit.point;
        heroVectorToAboveLine.points = [heroVectorCollider.offset, heroVectorAboveCollider.offset];

        // Hitboxes for landing points
        if (debugGoodCollider)
        {
            farGoodCollider.offset = closestFarHit.point;
            nearGoodCollider.offset = closestNearHit.point;

            if (debugOriginCollider)
            {
                if (farGood)
                    farOriginToFarGoodLine.points = [farOriginCollider.offset, farGoodCollider.offset];
                if (nearGood)
                    nearOriginToNearGoodLine.points = [nearOriginCollider.offset, nearGoodCollider.offset];
            }
        }

        if (!heightGood)
            return;

        if (!farGood || !nearGood) // One or both are not good
            return;

        Vector2 farHitPoint = closestFarHit.point;
        Vector2 nearHitPoint = closestNearHit.point;
        clamberedCollider = closestNearHit.collider;

        farHitPoint.y += 0.1f;
        nearHitPoint.y += 0.1f;

        bool farHitPointGood = !Helper.IsRayHittingNoTriggers(farHitPoint, Vector2.up, ceiling - 0.1f, LAYERMASK, out var closestFarPointHit);
        bool nearHitPointGood = !Helper.IsRayHittingNoTriggers(nearHitPoint, Vector2.up, ceiling - 0.1f, LAYERMASK, out var closestNearPointHit);

        if (debugHitPointCollider)
        {
            farHitPointCollider.offset = closestFarPointHit.point;
            nearHitPointCollider.offset = closestNearPointHit.point;
            farHitPointColliderCheck.offset = farHitPoint with { y = farHitPoint.y + ceiling - 0.1f };
            nearHitPointColliderCheck.offset = nearHitPoint with { y = nearHitPoint.y + ceiling - 0.1f };
            if(!farHitPointGood)
                farHPCtoHPCCLine.points = [farHitPointCollider.offset, farHitPointColliderCheck.offset];
            if (!nearHitPointGood)
                nearHPCtoHPCCLine.points = [nearHitPointCollider.offset, nearHitPointColliderCheck.offset];
        }

        if (!farHitPointGood || !nearHitPointGood) // Above Landing Spot
            return;

        if (!farHitPoint.y.IsWithinTolerance(0.1f, nearHitPoint.y))
        {
            return;
        }

        Vector2 n = new(vector.x, ___col2d.bounds.min.y + 0.2f);

        if (Helper.IsRayHittingNoTriggers(n, Vector2.down, landing , LAYERMASK, out var closestHit3) && farHitPoint.y - closestHit3.point.y < landingHeight)
        {
            return;
        }
        y = farHitPoint.y;
        __result = true;
    }
#endif

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
    /// Patches the ledge clamber check to allow for scaling.
    /// </summary>
    /// <param name="instructions"></param>
    /// <param name="ilGenerator"></param>
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
                new(OpCodes.Brfalse_S, notPatchingFSMs), // Branch to a label that we will grab later if the two values were the same
                new(OpCodes.Call, heroSizeRandoInstance), // Get the instance of Hero_Size_Rando
                new(OpCodes.Callvirt, playerSizeRando), // Gets the setting if the rando is enabled or not
                new(OpCodes.Ldc_I4_1), // Load one (true)
                new(OpCodes.Ceq), // Compare the setting to the loaded value
                new(OpCodes.Brtrue_S, null), // Branch to a label that we will grab later if the two values were the same
                new CodeInstruction(OpCodes.Nop).WithLabels(notPatchingFSMs)
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

    /// <summary>
    /// FSMAction to squish Hornet's hitbox to allow her to clamber normal spots when large
    /// </summary>
    private static SetBoxCollider2DSizeVector clamberSquishBox;
    /// <summary>
    /// FSMAction to unsquish Hornet's hitbox after clambering normal spots when large
    /// </summary>
    private static SetBoxCollider2DSizeVector clamberUnsquishBox;
    /// <summary>
    /// Set of States to patch Actions into 
    /// </summary>
    private static readonly FSMStatePatchSet mantleFSMPatchSet = new(
        "Mantle",
        [new FSMStatePatch(
            "Vault",
            delegate (FsmState state, object[]? extra)
            {
                state.Actions = state.Actions.AddToArray(clamberSquishBox);

            }),
        new FSMStatePatch(
            "Idle",
            delegate(FsmState state, object[]? extra)
            {
                state.Actions = state.Actions.AddToArray(clamberUnsquishBox);
            })
        ]);

    /// <summary>
    /// Patches the Mantle FSM to shrink Hornet's hitbox when Clambering
    /// </summary>
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

    /// <summary>
    /// Updates the Mantle squish sizes when called
    /// </summary>
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

    /// <summary>
    /// Resets the Mantle squish sizes to default when called
    /// </summary>
    private void ResetMantleFSMSizes()
    {
        clamberSquishBox.size = new Vector2(defaultColliderSize.x, defaultColliderSize.y);
        clamberSquishBox.offset = new Vector2(defaultColliderOffset.x, defaultColliderOffset.y);
        clamberUnsquishBox.size = new Vector2(defaultColliderSize.x, defaultColliderSize.y);
        clamberUnsquishBox.offset = new Vector2(defaultColliderOffset.x, defaultColliderOffset.y);
    }
    #endregion

    #region Water Surfaces
    /// <summary>
    /// Patch that hooks SurfaceWaterRegion's Start method to adjust the collider and adds it to a list so we can update it mid scene if needed
    /// </summary>
    /// <param name="__instance">The SurfaceWaterRegion we are adjusting</param>
    private static void SurfaceWaterRegion_Start_Postfix(ref SurfaceWaterRegion __instance)
    {
        float offset = __instance.GetComponent<BoxCollider2D>().offset.y;
        Instance.waterRegions.Add(__instance, offset);
        UpdateWaterSurface(__instance, Instance.heroScale.x, offset);
    }

    /// <summary>
    /// Update all the water surfaces in the current scene
    /// </summary>
    private static void UpdateWaterSurfaces()
    {
        foreach (var region in Instance.waterRegions)
        {
            UpdateWaterSurface(region.Key, Instance.heroScale.x, region.Value);
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

        //col.offset = col.offset with { y = offset + (1.04f - (1.04f * multiplier)) + (0.4f - (0.4f * multiplier)) };
        col.offset = col.offset with { y = offset - (1.44f * multiplier) + 1.44f };
    }
    #endregion

    #region Scene Patches
    /// <summary>
    /// Dictionary containing the various scene patches
    /// </summary>
    private static readonly Dictionary<string, Dictionary<string, FSMStateActionPatchSet>> sceneFSMPatches = new()
    {
        {
            "Bonetown",
            new()
            {
                {
                    "Churchkeeper Intro Scene",
                    new FSMStateActionPatchSet(
                        "Churchkeeper Intro Scene",
                        [new("Wait for Hero Grounded",
                            typeof(FloatCompare),
                            delegate(FsmStateAction action, object[]? extra)
                            {
                                ((FloatCompare)action).float2.Value *= Instance.heroScale.y;
                            },
                            "Control")]
                        )
                }
            }
        },
        {
            "Greymoor_01",
            new()
            {
                {
                    "Floor Control Scene",
                    new FSMStateActionPatchSet(
                        "Floor Control Scene",
                        [new("Flip",
                            typeof(CheckYPosition),
                            delegate(FsmStateAction action, object[]? extra)
                            {
                                ((CheckYPosition)action).compareTo.Value *= Instance.heroScale.y;
                            },
                            "Control")]
                        )
                }
            }
        }
    };

    /// <summary>
    /// Patches the FSMs of objects within a scene
    /// </summary>
    /// <param name="scene">The scene that was loaded</param>
    private static void PatchSceneFSMs(Scene scene)
    {
        if (sceneFSMPatches.TryGetValue(scene.name, out Dictionary<string, FSMStateActionPatchSet> patches))
        {
            GameObject[] objects = scene.GetRootGameObjects();

            foreach (var patchSet in patches)
            {
                patchSet.Value.ApplyPatches(objects.FirstOrDefault(obj => obj.name.Equals(patchSet.Key)).GetComponents<PlayMakerFSM>());
            }
        }
    }
    #endregion

    #region Sprint and Dash Patches
    /// <summary>
    /// FSMAction to squish Hornet's hitbox to allow her to sprint through normal spots when large
    /// </summary>
    private static SetBoxCollider2DSizeVector sprintSquishBox;
    /// <summary>
    /// FSMAction to unsquish Hornet's hitbox after sprinting through normal spots when large
    /// </summary>
    private static SetBoxCollider2DSizeVector sprintUnsquishBox;
    /// <summary>
    /// Set of States to patch Actions into 
    /// </summary>
    private static readonly FSMStatePatchSet sprintFSMPatchSet = new(
        "Sprint",
        [new FSMStatePatch(
            ["Dashed", "Start Sprint"],
            delegate (FsmState state, object[]? extra)
            {
                if(sprintSquishBox == null) return;

                FsmStateAction[] bassAckwards = [sprintSquishBox];
                state.Actions = bassAckwards.AddRangeToArray(state.Actions);
            }),
        new FSMStatePatch(
            ["Idle", "Dash Stab Dir"],
            delegate(FsmState state, object[]? extra)
            {
                if(sprintUnsquishBox == null) return;

                FsmStateAction[] bassAckwards = [sprintUnsquishBox];
                state.Actions = bassAckwards.AddRangeToArray(state.Actions);
            })
        ]);

    /// <summary>
    /// Patches the Sprint FSM to adjust Hornet's hitbox when dashing
    /// </summary>
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

    /// <summary>
    /// Patch to listen for if we are air dashing and adjust the collider accordingly
    /// </summary>
    private static bool HeroController_HeroDash_Prefix()
    {
        if (Instance.PlayerSizeRando)
        {
            Instance.UpdateSprintFSMSizes();
        }
        return true;
    }

    /// <summary>
    /// Sets the sprint squish size and offset
    /// </summary>
    /// <param name="dashingDown">If hornet is dashing down</param>
    private void SquishSize(bool dashingDown)
    {
        if (dashingDown)
        {
            sprintSquishBox.size = new Vector2(defaultColliderSize.x, defaultColliderSize.y);
            sprintSquishBox.offset = new Vector2(defaultColliderOffset.x, defaultColliderOffset.y);
        }
        else
        {
            sprintSquishBox.size = new Vector2(defaultColliderSize.x * 3.25f, defaultColliderSize.y / 1.5f);
            sprintSquishBox.offset = new Vector2(defaultColliderOffset.x, -0.3464352f + defaultColliderOffset.y);
        }
    }

    /// <summary>
    /// Updates the Sprint squish sizes when called
    /// </summary>
    private void UpdateSprintFSMSizes()
    {
        if (!PatchHeroFSMs) // Don't change the FSMs
        {
            ResetSprintFSMSizes();
            return;
        }

        HeroActions inputActions = InputHandler.Instance.inputActions;
        bool dashingDown = inputActions.Down.IsPressed
            && !HeroController.instance.cState.onGround
            && !inputActions.Left.IsPressed
            && !inputActions.Right.IsPressed;

        SquishSize(dashingDown);

        sprintUnsquishBox.size = new Vector2(defaultColliderSize.x, defaultColliderSize.y);
        sprintUnsquishBox.offset = new Vector2(defaultColliderOffset.x, defaultColliderOffset.y);
    }

    /// <summary>
    /// Resets the Sprint squish sizes to default when called
    /// </summary>
    private void ResetSprintFSMSizes()
    {
        sprintSquishBox.size = new Vector2(defaultColliderSize.x, defaultColliderSize.y);
        sprintSquishBox.offset = new Vector2(defaultColliderOffset.x, defaultColliderOffset.y);
        sprintUnsquishBox.size = new Vector2(defaultColliderSize.x, defaultColliderSize.y);
        sprintUnsquishBox.offset = new Vector2(defaultColliderOffset.x, defaultColliderOffset.y);
    }
    #endregion

    /// <summary>
    /// On Scene Load, save current loading scene
    /// </summary>
    /// <param name="scene">The new scene that is loading</param>
    /// <param name="mode">?</param>
    private void OnSceneLoad(Scene scene, LoadSceneMode _)
    {
        currentScene = scene;
        SetSize();
        waterRegions.Clear();
        PatchSceneFSMs(scene);
    }

    // TODO: tall hornet breaks some triggers; 
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
                if (currentScene == null)
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

    /// <summary>
    /// Sets Hornet's variables according to the given multiplier.
    /// </summary>
    /// <param name="multiplier">Hornet's scale</param>
    private void SetVariables(float multiplier)
    {
        if (heroTransform == null) return;

        heroScale = new(multiplier, multiplier, 1f);
        heroScaleFlipped = heroScale with { x = heroScale.x * -1 };
        FsmVariables sprintFSMVariables = HeroController.instance.sprintFSM.FsmVariables;

        // Divide so that when scaled, Hornet runs the same speed as her normal size
        sprintFSMVariables.FindFsmFloat("Dash Speed").Value = hornetBaseDashSpeed / multiplier;
        sprintFSMVariables.FindFsmFloat("Sprint Speed Regular").Value = hornetBaseSprintSpeed / multiplier;
        sprintFSMVariables.FindFsmFloat("Sprint Start Speed").Value = hornetBaseSprintStartSpeed / multiplier;
        sprintFSMVariables.FindFsmFloat("Sprint Speed Quick").Value = hornetBaseQuickSpeed / multiplier;
        sprintFSMVariables.FindFsmFloat("Sprint Speed Quicker").Value = hornetBaseQuickerSpeed / multiplier;

        mantleVaultTranslate.y = baseMantleVaultYOffset.Value * multiplier;

        CuteRandoCore.TraverseCreator(HeroController.instance, "SPEED_TO_ENTER_SCENE_UP").SetValue((hornetBaseSpeedToEnterHor * Instance.heroScale.x) + (0.2f - (0.2f * Instance.heroScale.x)));
        CuteRandoCore.TraverseCreator(HeroController.instance, "SPEED_TO_ENTER_SCENE_UP").SetValue((hornetBaseSpeedToEnterUp * Instance.heroScale.y) + (0.2f - (0.2f * Instance.heroScale.y)));

        heroTransform.localScale = heroTransform.localScale.x > 0f ? heroScale : heroScaleFlipped;

        UpdateMantleFSMSizes();
        UpdateSprintFSMSizes();

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

                hornetBaseSpeedToEnterHor = (float)CuteRandoCore.TraverseCreator(HeroController.instance, "SPEED_TO_ENTER_SCENE_HOR").GetValue();
                hornetBaseSpeedToEnterUp = (float)CuteRandoCore.TraverseCreator(HeroController.instance, "SPEED_TO_ENTER_SCENE_UP").GetValue();

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

    /// <summary>
    /// Returns Hornet's values to default
    /// </summary>
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

        heroScale = new(1f, 1f, 1f);

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
    /// <summary>
    /// Setting for if the hero's FSMs should be patched for quality of life
    /// </summary>
    public bool PatchHeroFSMs
    {
        get => patchHeroFSMs.Value;
        internal set => patchHeroFSMs.Value = value;
    }
    private ConfigEntry<bool> patchHeroFSMs;
    /// <summary>
    /// Default option for patching the FSMs
    /// </summary>
    public const bool defaultPatchHeroFSMs = true;

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
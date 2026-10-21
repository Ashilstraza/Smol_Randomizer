using DebugDrawing;

using HarmonyLib;

using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;

using Smol_Randomizer.Patchers.Enemy;
using Smol_Randomizer.Patchers.FSM_Actions;

using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;

using Translate = HutongGames.PlayMaker.Actions.Translate;

namespace Smol_Randomizer.Randomizers;

internal partial class Enemy_Size_Rando
{
    /// <summary>Creates a <see langword="delegate" /> for shifting an enemy toward its base</summary>
    /// <param name="atStart">
    /// Optional: <see langword="true" /> if <see cref="ShiftPosToBase" /> should be inserted at the start of the <see cref="FsmState" />
    /// </param>
    /// <param name="rotateAdjust">   Optional: Changes the direction the shift should come from in degrees</param>
    /// <param name="backupSize">
    /// Optional: <see cref="Vector2" /> containing the default size of the enemy, used when they don't have a <see cref="Collider" />
    /// </param>
    /// <param name="reverseX">
    /// Optional: <see langword="true" /> if the X direction of the shifting should be reversed
    /// </param>
    /// <param name="reverseY">
    /// Optional: <see langword="true" /> if the Y direction of the shifting should be reversed
    /// </param>
    /// <param name="magicNumber">
    /// Optional: <see langword="float" /> value used to shift the enemy instead of calculating it, is multiplied by
    /// their scale percentage to shift by that value
    /// </param>
    /// <param name="tempInvincible">
    /// Optional: <see langword="true" /> if the enemy should be made invincible when loading them, fixes issues where
    /// they die on spawn
    /// </param>
    /// <param name="skipRaycast">
    /// Optional: <see langword="true" /> if the enemy should not use raycasting to shift them to their base
    /// </param>
    /// <param name="cancelYVelocity">
    /// Optional: <see langword="true" /> if the enemy's Y velocity should be zeroed out, useful when they are spawned
    /// in with their <see cref="Rigidbody2D.bodyType" /> is set to <see cref="RigidbodyType2D.Dynamic" />
    /// </param>
    /// <returns>The <see langword="delegate" /> for creating a new <see cref="ShiftPosToBase" /><see cref="FsmStateAction" /></returns>
    public static Action<FsmState, object[]> CreateShiftToBaseDelegate(
        bool atStart = true,
        float rotateAdjust = 0,
        Vector2? backupSize = null,
        bool reverseX = false,
        bool reverseY = false,
        float magicNumber = float.MinValue,
        bool skipRaycast = false,
        bool cancelYVelocity = true)
    {
        return delegate (FsmState state, object[] param)
        {
            GameObject enemy = (GameObject)param[0];
            HealthManager healthManager = enemy.GetComponent<HealthManager>();
            Vector3 originalScale = Vector3.one;
            if (healthManager != null && !Instance.currentEnemyHealthManagers.TryGetValue(healthManager, out originalScale))
                originalScale = Vector3.one;

            FsmStateAction shift = new ShiftPosToBase
            {
                gameObject = new()
                {
                    GameObject = enemy
                },
                rotateAdjust = rotateAdjust,
                backupSize = backupSize ?? Vector2.zero,
                reverseX = reverseX,
                reverseY = reverseY,
                magicNumber = magicNumber,
                originalScale = originalScale,
                skipRaycast = skipRaycast,
                cancelYVelocity = cancelYVelocity
            };

            if (atStart)
            {
                FsmStateAction[] bassAckwards = [shift];
                state.Actions = bassAckwards.AddRangeToArray(state.Actions);
            }
            else state.Actions = state.Actions.AddToArray(shift);
        };
    }

    public static Action<FsmStateAction, object[]> CreateAdjustRaycast2dV2Scaler(bool onlyShort = false)
    {
        return delegate (FsmStateAction action, object[] param)
        {
            GameObject enemy = (GameObject)param[0];

            if (onlyShort && Math.Abs(enemy.transform.GetScaleY()) < 1)
                return;
            ((RayCast2dV2)action).distance.Value *= Math.Abs(enemy.transform.localScale.y);
        };
    }

    /// <summary>Dictionary containing various <see cref="PlayMakerFSM" /> patches.</summary>
    private static readonly HashSet<IEnemyFSMPatch> enemyFSMPatches = new()
    {
#region Many Places

    #region Pilgrims

        new EnemyStatePatch([   // pilgrim_behaviour - Init
                                "Pilgrim 01 Judge Buddy",
                                "Pilgrim StaffWielder",
                                "Pilgrim 01 Song",
                                "Pilgrim 02 Song"],
            "Init",
            CreateShiftToBaseDelegate(),
            fsmName: "pilgrim_behaviour"),

        new EnemyStatePatch([   // Control - Init
                                "Pilgrim Hiker",
                                "Pilgrim BellThrower",
                                "Pilgrim Moss Spitter",
                                "Pilgrim Fisher Enemy",
                                "Pilgrim 03 Song",
                                "Pilgrim 04 Song",
                                "Song Pilgrim Maestro"],
            "Init",
            CreateShiftToBaseDelegate(),
            fsmName: "Control"),

        new EnemyStatePatch([   // Attack - Init
                                "Pilgrim 05",
                                "Pilgrim Stomper Song",
                                "Song Pilgrim 03"],
            "Init",
            CreateShiftToBaseDelegate(),
            fsmName: "Attack"),

        new EnemyStatePatchSet("Pilgrim 01",
            [new EnemyStatePatch("",    // Pilgrim 01 drops from celing in Wanderer Chapel
                "Drop Pause",
                CreateShiftToBaseDelegate(reverseY: true)),
            new EnemyStatePatch("",
                "Init",
                CreateShiftToBaseDelegate())
            ],
            fsmName: "pilgrim_behaviour"),

        new EnemyStatePatchSet("Pilgrim 02", // Pilgrim 02 exists
            [
                new EnemyStatePatch("",
                    ["Init", "Dormant"],
                    CreateShiftToBaseDelegate()),
                new EnemyStatePatch("", // Fix for squish and scale
                    ["Roll Bounce","Attack Bounce"],
                    delegate (FsmState state, object[] param)
                    {
                        FsmFloat yScale = state.Fsm.Variables.FloatVariables.FirstOrDefault(var => var.Name.Equals("Y Scale"));
                        FsmFloat ySquash = state.Fsm.Variables.FloatVariables.FirstOrDefault(var => var.Name.Equals("Y Squash"));

                        if(yScale == null){
                            yScale = new("Y Scale")
                                {
                                    UseVariable = true,
                                    Value = ((GameObject)param[0]).transform.GetScaleY()
                                };
                            state.Fsm.Variables.FloatVariables = state.Fsm.Variables.FloatVariables.AddToArray(yScale);
                        }

                        if(ySquash == null)
                        {
                            ySquash = new("Y Squash")
                                {
                                    UseVariable = true
                                };
                            state.Fsm.Variables.FloatVariables = state.Fsm.Variables.FloatVariables.AddToArray(ySquash);
                        }

                        FsmStateAction[] newActionArray = new FsmStateAction[state.Actions.Length + 1];
                        int i = 0;

                        // Iterate over each action to modify or insert
                        foreach(var action in state.Actions)
                        {
                            if(action.GetType().Equals(typeof(GetScale)))
                            {
                                ((GetScale)action).yScale = yScale;

                                FloatOperator fo = new()
                                {
                                    float1 = yScale,
                                    float2 = 0.8f,
                                    operation = FloatOperator.Operation.Multiply,
                                    storeResult = ySquash
                                };

                                newActionArray[i++] = action;
                                newActionArray[i++] = fo;
                                continue;
                            }
                            if(action.GetType().Equals (typeof(SetScale)))
                            {
                                ((SetScale)action).y = ySquash;
                            }
                            newActionArray[i++] = action;
                        }

                        state.Actions = newActionArray;
                    },
                    fsmName: "Attack"),
                new EnemyStatePatch("", // Fix for squish and scale
                    ["Reset Scale", "Roll Rev", "Bounce Up"],
                    delegate (FsmState state, object[] param)
                    {
                        SetScale setScale = (SetScale)state.Actions.Where(action => action.GetType().Equals(typeof(SetScale))).First();
                        setScale.y = state.Fsm.Variables.FloatVariables.FirstOrDefault(var => var.Name.Equals("Y Scale")) ?? ((GameObject)param[0]).transform.GetScaleZ();
                    },
                    fsmName: "Attack"),
            ],
            fsmName: "pilgrim_behaviour"),

        new EnemyStatePatch("Pilgrim 03", // Pilgrin 03 is too short and clips under sometimes
            "Init",
            CreateShiftToBaseDelegate(magicNumber: -0.01f),
            fsmName: "pilgrim_behaviour"),

        new EnemyStatePatch("Pilgrim 04", // Pilgrin 04 has no collider in Wanderer Chapel before ambush
            "Init",
            CreateShiftToBaseDelegate(backupSize: new(1.1094f, 1.8125f)),
            fsmName: "pilgrim_behaviour"),

        new EnemyStatePatch("Pilgrim Fly",
            "Start Check",
            CreateShiftToBaseDelegate(),
            fsmName: "Control"),

        new EnemyStatePatch("Pilgrim Bellthrower Fly",
            "Wall Cling",
            CreateShiftToBaseDelegate(rotateAdjust: 90f, magicNumber: 0.009856f),
            fsmName: "Control"),

        new EnemyStateActionPatch("Song Pilgrim 03",
            "Move Start",
            typeof(RandomFloat),
            delegate(FsmStateAction action, object[] param)
            {
                RandomFloat rf = (RandomFloat)action;
                GameObject gameObject = (GameObject)param[0];
                rf.min.Value *= Math.Abs(gameObject.transform.localScale.x);
                rf.max.Value *= Math.Abs(gameObject.transform.localScale.x);
            },
            fsmName: "Attack"),

        new EnemyStatePatch("Song Pilgrim 01",
            "Init",
            delegate(FsmState state, object[] param)
            {
                if(((GameObject)param[0]).scene.name.Equals("Hang_04_boss")) // One in the HHG falls through the floor at minimum size
                    CreateShiftToBaseDelegate(magicNumber: -0.02f)(state, param);
                else
                    CreateShiftToBaseDelegate()(state, param);
            },
            fsmName: "Attack"),

    #endregion Pilgrims

        new EnemyStatePatch([   // Control - Init
                                // Bone Bottom & Marrow
                                "Bone Crawler",
                                "Bone Goomba",
                                "Bone Goomba Large",
                                "Bone Thumper",
                                // Hunter's March & Far Fields
                                "Bone Hunter",
                                "Bone Hunter Child",
                                "Bone Hunter Tiny",
                                // Greymoor
                                "Farmer Scissors",
                                "Farmer Centipede",
                                "Farmer Catcher",
                                "Mite",
                                "Crowman",
                                "Crowman Juror",
                                "Crowman Dagger",
                                "Crowman Dagger Juror",
                                // Sinner's Road
                                "Dustroach",
                                "Dustroach Caged",
                                // Blasted Steps & Sands of Karak
                                "Coral Spike Goomba",
                                // Bilewater
                                "Swamp Goomba",
                                // Slab
                                "Slab Prisoner Leaper New",
                                "Slab Fly Mid",
                                "Slab Prisoner Fly New",
                                "Slab Fly Large",
                                // Underworks
                                "Understore Poker",
                                // Citadel
                                "Song Reed",
                                "Song Automaton Shield",
                                "Song Automaton 01",
                                "Song Automaton 02",
                                // Abyss
                                "Gloom Beast",
                                "Abyss Crawler Large",
                                // Many Places
                                "Mite Heavy",
                                "Rosary Thief",
                                "Understore Mite Giant",
                            ],
            "Init",
            CreateShiftToBaseDelegate(),
            fsmName: "Control"),

        new EnemyStatePatch([   // Control - Init - Rotate 180
                                // Shellwood
                                "Shellwood Goomba Flyer",
                                // Many Places
                                "Citadel Bat",],
            "Init",
            CreateShiftToBaseDelegate(rotateAdjust: 180f),
            fsmName: "Control"),

        new EnemyStatePatch([   // Behaviour - Init
                                // Deep Docks
                                "Shield Dockworker",
                                "Dock Bomber",
                                // Greymoor
                                "Crow",
                                "Crowman Juror Tiny",
                                "Gnat Giant",
                                // Underworks
                                "Understore Small",
                                // Coral Tower
                                "Coral Goomba L",
                                "Coral Goomba M",
                                // Many Places
                                ],
            "Init",
            CreateShiftToBaseDelegate(),
            fsmName:  "Behaviour"),

        new EnemyStateActionPatch("Rhino",// TODO: check
            ["Charge Down", "Charge Up"],
            typeof(RayCast2dV2),
            CreateAdjustRaycast2dV2Scaler(),
            fsmName: "Control"),

        new EnemyStatePatch("Blade Spider",
            "Init",
            CreateShiftToBaseDelegate(magicNumber: -0.014f),
            fsmName: "Behaviour"),

        new EnemyStatePatch("Blade Spider Hang",
            "Init",
            CreateShiftToBaseDelegate(reverseY: true),
            fsmName: "Behaviour"),

        new EnemyStateActionPatch("Mite Heavy",
            "Set Bot",
            typeof(FloatAdd),
            delegate(FsmStateAction action, object[] param)
            {
                ((FloatAdd)action).add.Value *= ((GameObject)param[0]).transform.localScale.y;
            },
            fsmName: "Control"),
#endregion Many Places

#region Weavenests

         new EnemyStatePatch("Weaver Servitor",
            "Init",
            CreateShiftToBaseDelegate(magicNumber: -0.0055f),
            fsmName: "Control"),

         new EnemyStatePatch("Weaver Servitor Large",
            "Init",
            CreateShiftToBaseDelegate(magicNumber: -0.022f),
            fsmName: "Control"),
#endregion Weavenests

#region Moss Grotto & Mosshome

        new EnemyStatePatch("MossBone Crawler",
            "Init",
             CreateShiftToBaseDelegate(),
            fsmName: "Noise Reaction"),

        new EnemyStatePatch("Aspid Collector",
            "Init",
            CreateShiftToBaseDelegate(rotateAdjust: 90f, magicNumber: -0.004f, skipRaycast: true),
            fsmName: "Control"),

        new EnemyStateActionPatch("Pilgrim Moss Spitter",
            ["Attempt Larger Jump", "Escape Antic"],
            typeof(GetGroundPointClampedToEdge),
            delegate(FsmStateAction action, object[] param)
            {
                var minJumpDistance = ((GetGroundPointClampedToEdge)action).MinJumpDistance;

                minJumpDistance.Value = minJumpDistance.Value * Math.Abs(((GameObject)param[0]).transform.localScale.x);
            },
            fsmName: "Control"),
#endregion Moss Grotto & Mosshome

#region Bone Bottom & Marrow

#endregion Bone Bottom & Marrow

#region Wormways

        new EnemyStatePatch("Bone Worm", // Burrow height after drop
            "Position",
            delegate(FsmState state, object[] param)
            {
                ShiftVarDownByHalfHeight shift = new()
                {
                    gameObject = new()
                    {
                        GameObject = (GameObject)param[0]
                    },
                    variableName = "Ground Y"
                };

                    state.Actions = state.Actions.AddItem(shift).ToArray();
            },
            fsmName: "Control"),

        new EnemyStatePatch("Crypt Worm", // unburrow point
            ["Static Ready", "Ambush Ready"],
            CreateShiftToBaseDelegate(magicNumber: -0.004f),
            fsmName: "Control"),

        new EnemyStatePatch("Roof Crab",
            "Init",
            CreateShiftToBaseDelegate(magicNumber: -0.039928f, reverseY: true),
            fsmName: "Control"),
#endregion Wormways

#region Deep Docks
        new EnemyStatePatch("Dock Flyer",
            "Init",
            CreateShiftToBaseDelegate(magicNumber: -0.015f, skipRaycast: true),
            fsmName: "Behaviour"),

        new EnemyStatePatchSet("Dock Worker", // Halt velocity and shift
            [new("Dock Worker",
                "Init",
                CreateShiftToBaseDelegate(),
                fsmName: "Behaviour"),
            new("Dock Worker",
                "Start Dig",
                delegate(FsmState action, object[] param)
                {
                    GameObject enemy = (GameObject)param[0];

                    if ((enemy.name.Equals("Dock Worker (1)") && enemy.scene.name.Equals("Bone_East_03")) || // Several workers spawn too close to coals
                        (enemy.name.Equals("Dock Worker (5)") && enemy.scene.name.Equals("Dock_02b")) ||
                        (enemy.name.Equals("Dock Worker") && enemy.scene.name.Equals("Dock_03")))
                        CreateShiftToBaseDelegate(rotateAdjust: 90f, skipRaycast: true, magicNumber: -0.01f)(action, param);
                    else
                        CreateShiftToBaseDelegate(rotateAdjust: 90f, skipRaycast: true, magicNumber: -0.02f)(action, param);
                },
                fsmName: "Behaviour"),
            new("Dock Worker", // Signis & Gronn fight intro
                "Idle",
                CreateShiftToBaseDelegate(),
                fsmName: "Control")]),

        new EnemyStatePatch("Tar Slug Huge", // Most have placement issues requiring a stack of ifs
            "Init",
            delegate(FsmState action, object[] param)
            {
                GameObject enemy = (GameObject)param[0];
                float magic = 0f;

                if (enemy.scene.name.Equals("Dock_11"))
                {
                    if(enemy.name.Equals("Tar Slug Huge (2)"))
                        magic = -0.025f;
                    else if(enemy.name.Equals("Tar Slug Huge (5)") ||
                            enemy.name.Equals("Tar Slug Huge (6)") ||
                            enemy.name.Equals("Tar Slug Huge (7)"))
                        magic = -0.02f;
                    else if(enemy.name.Equals("Tar Slug Huge (4)"))
                        magic = -0.01f;
                }

                CreateShiftToBaseDelegate(magicNumber: magic)(action, param);
            },
            fsmName: "Control"),
#endregion Deep Docks

#region Hunters March & Far Fields

        new EnemyStateActionPatch("Bone Hunter Buzzer",
            "Roost Start",
            typeof(FloatAdd),
            delegate (FsmStateAction action, object[] param)
            {
                ((FloatAdd)action).add.Value *= Math.Abs(((GameObject)param[0]).transform.GetScaleX());
            },
            fsmName: ""),

        new EnemyStatePatch("Bone Hunter Fly",
            "Init",
            CreateShiftToBaseDelegate(magicNumber: -0.015f, skipRaycast: true),
            fsmName: "Control"),
#endregion Hunters March & Far Fields

#region Greymoor
        new EnemyStateActionPatch("Farmer Scissors", // Fixes distance check
            "Do Step",
            typeof(RayCast2dV2),
            CreateAdjustRaycast2dV2Scaler(),
            fsmName: "Control"),

        new EnemyStateActionPatch("Farmer Scissors", // Cut up end check
            "Cut Through U",
            typeof(RayCast2dV2),
            delegate(FsmStateAction action, object[] param)
            {
                RayCast2dV2 rc2dv2 = (RayCast2dV2)action;
                GameObject enemy = (GameObject)param[0];
                float height = enemy.GetComponent<BoxCollider2D>()?.size.y ?? 2.5837f;

                float newDistance = (rc2dv2.distance.Value - height) + height * Math.Abs(enemy.transform.GetScaleY());
                rc2dv2.distance.Value = newDistance;
            },
            fsmName: "Control"),

        new EnemyStateActionPatch("Farmer Centipede", // Fixes distance check
            "Idle Chase",
            typeof(DistanceWalk),
            delegate (FsmStateAction action, object[] param)
            {
                ((DistanceWalk)action).distance.Value *= Math.Abs(((GameObject)param[0]).transform.GetScaleX());
            },
            fsmName: "Control"),

        new EnemyStatePatch("FlyAway Crow",
            "Init",
            delegate(FsmState action, object[] param)
            {
                GameObject crow = (GameObject)param[0];

                string name = CuteRandoCore.CullName((string)param[1]);

                Instance.RandomizeSize(false, crow.transform);
                AdjustChildren(crow, name);

                CreateShiftToBaseDelegate(backupSize: new(0.99f, 1.3624f), magicNumber: -0.01f)(action, param);
            },
            fsmName: "Control"),

        new EnemyStateActionPatch(["Crowman", "Crowman Juror"], // Fixes scale issues
            "Start Rest",
            typeof(RandomFloatEither),
            delegate (FsmStateAction action, object[] param)
            {
                Transform transform = ((GameObject)param[0]).transform;
                RandomFloatEither rfe = (RandomFloatEither)action;

                rfe.value1.Value = rfe.value1.Value > 0
                    ? transform.localScale.x
                    : transform.localScale.x * -1;
                rfe.value2.Value = rfe.value2.Value > 0
                    ? transform.localScale.x
                    : transform.localScale.x * -1; ;
            },
            fsmName: "Control"),

        new EnemyStateActionPatch(["Crow", "Crowman Juror Tiny"], // Fix Swoop
            "Swoop Down",
            typeof(FloatOperator),
            delegate (FsmStateAction action, object[] param)
            {
                Transform transform = ((GameObject)param[0]).transform;
                FloatOperator fo = (FloatOperator)action;

                fo.float1.Value = fo.float1.Value * (1/Math.Abs(transform.localScale.x));
            },
            fsmName: "Behaviour"),
#endregion Greymoor

#region Wisp Thicket
        new EnemyStatePatch("Farmer Wisp",
            "State",
            CreateShiftToBaseDelegate(),
            fsmName: "Control"),

        new EnemyStateActionPatch("Farmer Wisp",
            "Tele Pos",
            typeof(Vector2AddXY),
            delegate(FsmStateAction action, object[] param)
            {
                ((Vector2AddXY)action).addY.Value *= Math.Abs(((GameObject)param[0]).transform.GetScaleY());
            }),

#endregion Wisp Thicket

#region Bellways

        new EnemyStateActionPatch("Bell Goomba",
            ["Set To Ground","Set To Wall L", "Set To Wall R", "Set To Roof"],
            typeof(Translate),
            delegate(FsmStateAction action, object[] param)
            {
                GameObject gameObject = (GameObject)param[0];
                float halfSize = gameObject.GetComponent<BoxCollider2D>().size.y * Math.Abs(gameObject.transform.localScale.y);
                Translate translate = (Translate)action;

                if(translate.x.Value > 0)
                    translate.x.Value = halfSize;
                else if(translate.x.Value < 0)
                    translate.x.Value = halfSize * -1;
                else if(translate.y.Value > 0)
                    translate.y.Value = halfSize;
                else if(translate.y.Value < 0)
                    translate.y.Value= halfSize * -1;
                else
                    CuteRandoCore.Log.LogWarning("Bell Goomba Translate Patch failed. Translate x and y are both zero?");
            },
            fsmName: "Control"),

        new EnemyStateActionPatch("Bell Goomba", // Fixes them getting stuck and vibrating?
            "Surface Dig",
            typeof(CheckXPosition),
            delegate(FsmStateAction action, object[] param)
            {
                ((CheckXPosition)action).everyFrame = false;
            },
            fsmName: "Control"),
#endregion Bellways

#region Shellwood

        new EnemyStatePatch("Shellwood Goomba Flyer",
            "Init",
            CreateShiftToBaseDelegate(rotateAdjust: 180f),
            fsmName: "Control"),

        new EnemyStatePatch("Shellwood Goomba",
            "Init",
            delegate(FsmState action, object[] param)
            {
                GameObject enemy = (GameObject)param[0];
                if (enemy.scene.name.Equals("Shellwood_01") && enemy.name.Equals("Shellwood Goomba (2)"))
                    CreateShiftToBaseDelegate(rotateAdjust: 180f)(action, param);
                else
                    CreateShiftToBaseDelegate()(action, param);
            },
            fsmName: "Control"),

        new EnemyStatePatch("Bloom Puncher",
            "Init",
            delegate(FsmState action, object[] param)
            {
                GameObject enemy = (GameObject)param[0];
                if (enemy.scene.name.Equals("Shellwood_10") && enemy.name.Equals("Bloom Puncher"))
                    enemy.transform.position = new(
                        enemy.transform.position.x + 0.5f,
                        enemy.transform.position.y + (0.2f * (Math.Abs(enemy.transform.localScale.y))));

                CreateShiftToBaseDelegate(rotateAdjust: 270f, skipRaycast: true)(action, param);
            },
            fsmName: "Control"),

        new EnemyStatePatch("Stick Insect Charger", // Shellwood_20 stick insect doesn't start with a collider
            "Init",
            CreateShiftToBaseDelegate(backupSize: new(1.8438f, 1.9375f)),
            fsmName: "Behaviour Base"),

        new EnemyStatePatch("Stick Insect Flyer",
            "Init",
            CreateShiftToBaseDelegate(rotateAdjust: 270f, magicNumber: -0.005f),
            fsmName: "Control"),

        new EnemyStatePatch("Stick Insect",
            "Init",
            CreateShiftToBaseDelegate(),
            fsmName: "Behaviour Base"),
#endregion Shellwood

#region Blasted Steps & Sands of Karak
        new EnemyStatePatch("Coral Judge", // Has a delay at the start of Init that causes issues, so we have to target something other than Init
            "Check Scale",
            CreateShiftToBaseDelegate(),
            fsmName: "Control"),

        new EnemyStatePatch("Coral Conch Driller",
            "Init",
            CreateShiftToBaseDelegate(skipRaycast: true),
            fsmName: "Control"),

        new EnemyStateActionPatchSet("Coral Conch Driller",
            [new("",
                ["Aim D", "Antic1 D"],
                typeof(ChaseObjectVertical),
                delegate(FsmStateAction action, object[] param)
                {
                    ((ChaseObjectVertical)action).offset.Value *= Math.Abs(((GameObject)param[0]).transform.GetScaleY());
                }),
            new("",
                ["Aim F", "Antic1 L", "Antic2 R"],
                typeof(DistanceFly),
                delegate(FsmStateAction action, object[] param)
                {
                    ((DistanceFly)action).distance.Value *= Math.Abs(((GameObject)param[0]).transform.GetScaleY());
                }),
            new("",
                ["Drill In D", "Drill In L", "Drill In R"],
                typeof(AnimatePositionBy),
                delegate(FsmStateAction action, object[] param)
                {
                    ((AnimatePositionBy)action).shiftBy.Value = ((AnimatePositionBy)action).shiftBy.Value.MultiplyElements(((GameObject)param[0]).transform.localScale.Abs());
                }),
            new("",
                ["Emerge 1 D", "Emerge 1 L", "Emerge 1 R"],
                typeof(AnimatePositionBy),
                delegate(FsmStateAction action, object[] param)
                {
                    GameObject enemy = (GameObject)param[0];

                    if(enemy.transform.GetScaleY() < 1)
                        return;
                    ((AnimatePositionBy)action).shiftBy.Value = ((AnimatePositionBy)action).shiftBy.Value.MultiplyElements(enemy.transform.localScale.Abs());
                }),
            new("",
                ["Under D", "Under L", "Under R"],
                typeof(Translate),
                delegate(FsmStateAction action, object[] param)
                {
                    Translate translate = (Translate)action;
                    GameObject enemy = (GameObject)param[0];

                    translate.x.Value *= Math.Abs(enemy.transform.GetScaleX());
                    translate.y.Value *= Math.Abs(enemy.transform.GetScaleY());
                }),
            new("",
                ["Under Aim D", "Under Aim L", "Under Aim R", "Shoot L", "Shoot R", "Shoot D"],
                typeof(RayCast2dV2),
                CreateAdjustRaycast2dV2Scaler(onlyShort: true))
            ],
            fsmName: "Control"),

#endregion Blasted Steps & Sands of Karak

#region Sinner's Road
        new EnemyStatePatch("Dustroach FG",
            "Init",
            delegate(FsmState state, object[] param)
            {
                GameObject dustroach = (GameObject)param[0];

                string name = CuteRandoCore.CullName((string)param[1]);

                Instance.RandomizeSize(false, dustroach.transform);
                AdjustChildren(dustroach, name);

                CreateShiftToBaseDelegate(backupSize: new(2.6999f, 1.2145f), magicNumber: -0.01f)(state, param);
            },
            fsmName: "Control"),

        new EnemyStateActionPatch("Dustroach", // Fixes scale issues
            "ScrabbleJump Air",
            typeof(RayCast2dV2),
            CreateAdjustRaycast2dV2Scaler(),
            fsmName: "Control"),

        new EnemyStateActionPatch("Roachfeeder Tall",
            "Watch",
            typeof(DistanceFly),
            delegate(FsmStateAction action, object[] param)
            {
                ((DistanceFly)action).distance.Value *= Math.Abs(((GameObject) param[0]).transform.GetScaleX());
            }),
#endregion Sinner's Road

#region Bilewater & Putrified Ducts
        new EnemyStatePatch(["Swamp Mosquito", "Swamp Mosquito Skinny"],
            "Wall Cling",
            CreateShiftToBaseDelegate(rotateAdjust: 270f),
            fsmName: "Control"),

        new EnemyStateActionPatch("Swamp Muckman",
            ["Stick To Floor", "Stick to Wall R", "Stick to Wall L"],
            typeof(Translate),
            delegate(FsmStateAction action, object[] param)
            {
                GameObject gameObject = (GameObject)param[0];
                float halfSize = gameObject.GetComponent<BoxCollider2D>().size.y * Math.Abs(gameObject.transform.localScale.y);
                Translate translate = (Translate)action;

                if(translate.x.Value > 0)
                    translate.x.Value = halfSize;
                else if(translate.x.Value < 0)
                    translate.x.Value = halfSize * -1;
                else if(translate.y.Value > 0)
                    translate.y.Value = halfSize;
                else if(translate.y.Value < 0)
                    translate.y.Value= halfSize * -1;
                else
                    CuteRandoCore.Log.LogWarning("Swamp Mosquito Translate Patch failed. Translate x and y are both zero?");
            },
            fsmName: "Control"),

        new EnemyFSMPatch("Swamp Ductsucker",
            delegate(PlayMakerFSM fsm, object[] param)
            {
                GameObject gameObject = (GameObject)param[0];

                var teleMin = gameObject.transform.Find("Tele Min");
                var teleMax = gameObject.transform.Find("Tele Max");
                var boundsMin = gameObject.transform.Find("Bounds Min");
                var boundsMax = gameObject.transform.Find("Bounds Max");

                teleMin.SetLocalPositionX(teleMin.localPosition.x / gameObject.transform.localScale.x);
                teleMax.SetLocalPositionX(teleMax.localPosition.x / gameObject.transform.localScale.x);
                boundsMin.SetLocalPositionX(boundsMin.localPosition.x / gameObject.transform.localScale.x);
                boundsMax.SetLocalPositionX(boundsMax.localPosition.x / gameObject.transform.localScale.x);

                fsm.FsmVariables.GetFsmFloat("Tele Min X").Value = teleMin.position.x;
                fsm.FsmVariables.GetFsmFloat("Tele Max X").Value = teleMax.position.x;
                fsm.FsmVariables.GetFsmFloat("X Pos Min").Value = boundsMin.position.x;
                fsm.FsmVariables.GetFsmFloat("X Pos Max").Value = boundsMax.position.x;
            }),

        /*new EnemyStateActionPatch("Tendril",
            "State 2",
            typeof(FloatOperator),
            delegate(FsmStateAction action, object[] param)
            {
                FloatOperator fo = (FloatOperator)action;
                var tendril = ((GameObject)param[0]);

                if(fo.storeResult.Name is ("Collider Height" or "Mid Point Y"))
                    fo.float2.Value *= tendril.transform.parent.GetScaleY();
            },
            fsmName: "Set Colliders"),

        new EnemyStatePatch("Tendril",
            "State 2",
            delegate(FsmState state, object[] param)
            {
                FsmStateAction[] actionArray = state.Actions;
                FsmStateAction[] newArray = new FsmStateAction[actionArray.Length + 1];
                var tendril = ((GameObject)param[0]);
                int j = 0;
                float parentHalfScale = tendril.transform.parent.GetScaleY() * 2;
                float newScale = parentHalfScale / (parentHalfScale * parentHalfScale);

                for (int i = 0; i < actionArray.Length; i++)
                {
                    newArray[j++] = actionArray[i];
                    if(actionArray[i] is GetPosition2d gp2d)
                    {
                        if(gp2d.y.Name.Equals("Self Y")){
                            newArray[j++] = new FloatOperator()
                            {
                                float1 = gp2d.y,
                                float2 = -1,
                                operation = FloatOperator.Operation.Multiply,
                                storeResult = gp2d.y,
                                everyFrame = false
                            };
                        }
                    }
                }

                state.Actions = newArray;
            },
            fsmName: "Set Colliders"),

        new EnemyStatePatch("Attack Detector",
            "Set Collider",
            delegate(FsmState state, object[] param)
            {
                var attackDetector = ((GameObject)param[0]);

                if (!attackDetector.transform.parent.name.Equals("Tendril"))
                    return;

                var attackDetectiorCollider = attackDetector.GetComponent<BoxCollider2D>();
                attackDetectiorCollider.size = state.Fsm.GetFsmVector2("Box Size").Value;
                attackDetectiorCollider.offset = state.Fsm.GetFsmVector2("Box Offset").Value;

                var x = state.Actions.FirstOrDefault(action => action.GetType() == typeof(GetBoxCollider2DSizeVector)) as GetBoxCollider2DSizeVector;

                if(x != null)
                    x.Enabled = false;
            },
            fsmName: "Detect Attack"),*/

#endregion Bilewater & Putrified Ducts

#region Slab
        new EnemyStatePatch("Slab Fly Small",
            "Rest",
            CreateShiftToBaseDelegate(rotateAdjust: 33f, magicNumber: -0.022f),
            fsmName: "Bouncer Control"),

        #endregion Slab

#region Citadel
        new EnemyStatePatch("Song Reed Grand",
            "Init",
            CreateShiftToBaseDelegate(backupSize: new(1.3594f, 5.8281f)),
            fsmName: "Control"),

        new EnemyStateActionPatchSet("Song Reed Grand",
            [new("",
                ["Set Cast", "Set Slash", "Set Jump Attack"],
                typeof(SetFloatValue),
                delegate(FsmStateAction action, object[] param)
                {
                    var sfv = (SetFloatValue)action;

                    if(sfv.floatVariable.Name.Equals("Max Distance"))
                        sfv.floatValue.Value *= Math.Abs(((GameObject)param[0]).transform.localScale.x);
                }),
            new("",
                "Crawl F",
                typeof(FloatOperator),
                delegate(FsmStateAction action, object[] param)
                {
                    ((FloatOperator)action).float2.Value *= Math.Abs(((GameObject)param[0]).transform.localScale.x);
                }),
            new("",
                "Hop",
                typeof(FloatTestToBool),
                delegate(FsmStateAction action, object[] param)
                {
                    ((FloatTestToBool)action).float2.Value *= Math.Abs(((GameObject)param[0]).transform.localScale.x);
                }),
            new("",
                "Stomp Land",
                typeof(Translate),
                delegate(FsmStateAction action, object[] param)
                {
                    ((Translate)action).y.Value *= Math.Abs(((GameObject)param[0]).transform.localScale.x);
                })
            ],
            fsmName: "Control"),

#region Cogwork
        new EnemyStatePatch("Song Automaton Fly",
            "Land",
            delegate(FsmState state, object[] param)
            {
                FsmStateAction[] actions = state.Actions;
                FsmStateAction[] newActionArray = new FsmStateAction[actions.Length + 1];
                int j = 0;

                for(int i = 0; i < actions.Length; i++)
                {
                    newActionArray[j++] = actions[i];
                    if(actions[i].GetType() == typeof(SetRotation))
                    {
                        newActionArray[j++] = new ShiftVarDownByHalfHeight()
                        {
                            gameObject = new()
                            {
                                GameObject = (GameObject)param[0]
                            },
                            variableName = "Target Pos",
                            angleName = "Angle"
                        };
                    }
                }

                state.Actions = newActionArray;
            },
            fsmName: "Control"),

        new EnemyStatePatch("Song Automaton Goomba",
            "Init",
            CreateShiftToBaseDelegate(magicNumber: -0.02f),
            fsmName: "Control"),

        new EnemyStateActionPatch("Song Automaton Goomba",
            "Attack Check",
            typeof(RayCast2dV2),
            CreateAdjustRaycast2dV2Scaler(),
            fsmName: "Control"),

        new EnemyStateActionPatch(["Song Automaton 01", "Song Automaton 02", "Song Automaton Goomba"],
            ["Do Step S", "Do Step F", "Stepped"],
            typeof(RayCast2dV2),
            CreateAdjustRaycast2dV2Scaler(),
            fsmName: "Walk"),

        new EnemyStateActionPatch("Song Automaton Shield",
            ["Do Step S", "Do Step F", "Stepped"],
            typeof(RayCast2dV2),
            CreateAdjustRaycast2dV2Scaler(),
            fsmName: "Automaton Walk"){
        },

#endregion Cogwork

#region High Halls

        new EnemyStatePatch("Song Administrator",
            "Init",
            delegate(FsmState state, object[] param)
            {
                var enemy = (GameObject)param[0];
                var enemyName = enemy.name;
                var scene = enemy.scene.name;
                if ((scene.Equals("Hang_08") && enemyName.Equals("Song Administrator (1)"))
                || (scene.Equals("Hang_03") && enemyName.Equals("Song Administrator")))
                    CreateShiftToBaseDelegate(magicNumber: -0.02f, skipRaycast: true)(state, param);
                else
                    CreateShiftToBaseDelegate(rotateAdjust: 270f)(state, param);
            },
            fsmName: "Control"),

#endregion High Halls

#region Library

        new EnemyStatePatch("Scholar",
            "Init",
            delegate(FsmState state, object[] param)
            {
                var enemy = (GameObject)param[0];
                var scene = enemy.scene.name;

                if(scene.Equals("Library_02"))
                    CreateShiftToBaseDelegate(magicNumber: -0.015f)(state, param);
                else if(scene.Equals("Library_14"))
                    CreateShiftToBaseDelegate(rotateAdjust: 180f, magicNumber: -0.02f)(state, param);
                else
                    CreateShiftToBaseDelegate(rotateAdjust: 180f)(state, param);
            },
            fsmName: "Control"),

        new EnemyStatePatch("Hiding Song Acolyte",
            "Init",
            delegate(FsmState action, object[] param)
            {
                GameObject acolyte = (GameObject)param[0];

                string name = CuteRandoCore.CullName((string)param[1]);

                Instance.RandomizeSize(false, acolyte.transform);
                AdjustChildren(acolyte, name);

                CreateShiftToBaseDelegate(backupSize: new(0.7904f, 2.747f), magicNumber: -0.01f)(action, param);
            },
            fsmName: "Control"),

        new EnemyStatePatchSet("Song Scholar Acolyte",
            [new("",
                [   "Voluntary Sing",
                    "Sleep",
                    "Walk Start Frame",
                    "Church BG",
                    "Popup Ready"],
                CreateShiftToBaseDelegate()),
            new("",
                "Wall Idle",
                CreateShiftToBaseDelegate(rotateAdjust:270f)),
            new("",
                "Roof Idle",
                CreateShiftToBaseDelegate(rotateAdjust:180f))
            ],
            fsmName: "Control"),
#endregion Library

#endregion Citadel

#region Underworks

#endregion Underworks

#region Abyss
        new EnemyStatePatch("Abyss Crawler",
            "Check",
            CreateShiftToBaseDelegate(),
            fsmName: "Fall"),

        new EnemyFSMPatch("Gloomfly Cocoon S",
            delegate(PlayMakerFSM fsm, object[] param)
            {
                var cocoon = ((GameObject)param[0]);
                var gloomfly = fsm.FsmVariables.GetFsmGameObject("Gloomfly");
                fsm.FsmVariables.GetFsmGameObject("Gloomfly").OnChange += delegate()
                {
                    cocoon.transform.localScale = gloomfly.Value.transform.localScale;
                    Enemy_Size_Rando.AdjustChildren(cocoon, cocoon.name);
                };
                if(gloomfly.Value == null) return;

                cocoon.transform.localScale = gloomfly.Value.transform.localScale;
                Enemy_Size_Rando.AdjustChildren(cocoon, cocoon.name);
            },
            fsmName: "Control"),

        new EnemyFSMPatch("Gloomfly Cocoon L",
            delegate(PlayMakerFSM fsm, object[] param)
            {
                var cocoon = ((GameObject)param[0]);
                var gloomfly1 = fsm.FsmVariables.GetFsmGameObject("Gloomfly 1");
                var gloomfly2 = fsm.FsmVariables.GetFsmGameObject("Gloomfly 2");
                var gloomfly3 = fsm.FsmVariables.GetFsmGameObject("Gloomfly 3");

                var alertRangeCollider = cocoon.transform.Find("Alert Range").GetComponent<Collider2D>();
                Vector2 alertRangeOffset = alertRangeCollider.offset;
                Vector3 alertPosition = alertRangeCollider.transform.localPosition;
                Vector3 alertScale = alertRangeCollider.transform.localScale;

                Action scaler = delegate()
                {
                    // Revert changes before re-applying. This allows for proper scaling when we have more than one gloomfly.
                    alertRangeCollider.offset = alertRangeOffset;
                    alertRangeCollider.transform.localPosition = alertPosition;
                    alertRangeCollider.transform.localScale = alertScale;

                    Vector3 maxScale = Vector3.zero;

                    if(gloomfly1.Value != null)
                        maxScale = gloomfly1.Value.transform.localScale.Abs();
                    if(gloomfly2.Value != null)
                        maxScale = Math.Abs(gloomfly2.Value.transform.localScale.x) > maxScale.x ? gloomfly2.Value.transform.localScale.Abs() : maxScale;
                    if(gloomfly3.Value != null)
                        maxScale = Math.Abs(gloomfly3.Value.transform.localScale.x) > maxScale.x ? gloomfly3.Value.transform.localScale.Abs() : maxScale;

                    if(maxScale == Vector3.zero) return;

                    cocoon.transform.localScale =  maxScale.MultiplyElements(cocoon.transform.localScale.normalized);
                    Enemy_Size_Rando.AdjustChildren(cocoon, cocoon.name);
                };

                fsm.FsmVariables.GetFsmGameObject("Gloomfly 1").OnChange += scaler;
                fsm.FsmVariables.GetFsmGameObject("Gloomfly 2").OnChange += scaler;
                fsm.FsmVariables.GetFsmGameObject("Gloomfly 3").OnChange += scaler;

                Vector3 maxScale = Vector3.zero;
                    if(gloomfly1.Value != null)
                        maxScale = gloomfly1.Value.transform.localScale.Abs();
                    if(gloomfly2.Value != null)
                        maxScale = Math.Abs(gloomfly2.Value.transform.localScale.x) > maxScale.x ? gloomfly2.Value.transform.localScale.Abs() : maxScale;
                    if(gloomfly3.Value != null)
                        maxScale = Math.Abs(gloomfly3.Value.transform.localScale.x) > maxScale.x ? gloomfly3.Value.transform.localScale.Abs() : maxScale;
                    if(maxScale == Vector3.zero) return;
                    cocoon.transform.localScale =  maxScale.MultiplyElements(cocoon.transform.localScale.normalized);
                    Enemy_Size_Rando.AdjustChildren(cocoon, cocoon.name);
            },
            fsmName: "Control"),
#endregion Abyss

#region Coral Tower
        new EnemyStatePatch("Coral Big Jellyfish",
            "Init",
            CreateShiftToBaseDelegate(rotateAdjust: 180f, magicNumber: -0.60f),
            fsmName: "Control"),

#endregion Coral Tower
    };

    /// <summary>Dictionary containing various enemy non-FSM patches</summary>
    private static readonly List<IEnemyObjectPatch> enemyObjectPatches =
    [
        new EnemyObjectPatch("Farmer Centipede", // Fix emerge point
            delegate(GameObject patchTarget, object[] param)
            {
                var collider = patchTarget.GetComponent<BoxCollider2D>();

                if(collider == null) return;

                var height = collider.size.y;
                var digEmerge = patchTarget.transform.Find("Pt DigEmerge");

                digEmerge.transform.localPosition = digEmerge.transform.localPosition with { y = height * Math.Abs(patchTarget.transform.localScale.y) * -1 };
            }),

        new EnemyObjectPatch(["Crowman Juror", "Crowman Dagger Juror", "Crowman Juror Tiny"], // Fix height constrain
            delegate(GameObject patchTarget, object[] param)
            {
                var collider = patchTarget.GetComponent<BoxCollider2D>();

                if(collider == null || !patchTarget.scene.name.Equals("Room_CrowCourt_02")) return;

                var height = collider.size.y;
                var constrain = patchTarget.GetComponent<ConstrainPosition>();

                constrain.yMin = 20f + (height * Math.Abs(patchTarget.transform.localScale.y)) / 2;
            }),

        new EnemyObjectPatch("Pond Skater", // Fix skate height
            delegate(GameObject patchTarget, object[] param)
            {
                var collider = patchTarget.GetComponent<BoxCollider2D>();

                if(collider == null) return;

                patchTarget.transform.position = patchTarget.transform.position with
                {
                    y = ShiftPosToBase.ShiftN(
                        patchTarget.transform.position.y,
                        patchTarget.transform.localScale.y,
                        collider.size.y / 2,
                        collider.offset.y,
                        Math.Cos(Math.PI * patchTarget.transform.rotation.eulerAngles.z / 180))
                };
            }),

        new EnemyObjectPatch("Bone Crawler", // One has a bad constrain position, fixed by increasing range and setting Crawler's rayDownFrontPadding higher
            delegate(GameObject patchTarget, object[] param)
            {
                if(patchTarget.scene.name.Equals("Bone_10") && patchTarget.name.Equals("Bone Crawler (4)")){
                    ConstrainPosition constrain = patchTarget.GetComponent<ConstrainPosition>();
                    constrain.xMax = 45f;
                    constrain.xMin = 25f;
                    CuteRandoCore.TraverseCreator(patchTarget.GetComponent<Crawler>(), "rayDownFrontPadding").SetValue(3f);
                }
            }),

        new EnemyObjectPatch("Swamp Goomba", // TC plz...
            delegate (GameObject patchTarget, object[] param)
            {
                if(patchTarget.scene.name.Equals("Shadow_04"))
                {
                    if(patchTarget.name.Equals("Swamp Goomba (1)")){
                        ConstrainPosition constrain = patchTarget.GetComponent<ConstrainPosition>();
                        constrain.constrainX = false;
                    }
                    else if(patchTarget.name.Equals("Swamp Goomba"))
                    {
                        KeepWorldPosition kwp = patchTarget.GetComponent<KeepWorldPosition>();
                        kwp.keepX = false;
                    }
                }
            }),

        new EnemyObjectPatch("Fields Flock Flyer", // Drop Flock Flyers to ground
            delegate(GameObject patchTarget, object[] param)
            {
                ShiftPosToBase.Shift(patchTarget, Vector2.one, skipRaycast: true);
            }),

        /*new EnemyObjectPatch("Bone Roller", //TODO: Needs a lot of fixing!
            delegate(GameObject patchTarget, object[] param)
            {
                var fsm = patchTarget.GetComponents<PlayMakerFSM>().FirstOrDefault(fsm => fsm.FsmName.Equals("Control"));
                var moveConstrain = fsm.FsmVariables.GetFsmFloat("Move Constrain");

                foreach(var state in fsm.FsmStates){
                    int i = 0;

                    while(i < state.Actions.Length){
                        if(state.Actions[i].GetType() == typeof(RayCast2dV2)){
                            RayCast2dV2 firstCheck = (RayCast2dV2)state.Actions[i];
                            RayCast2dV2 secondCheck = new()
                            {
                                fromGameObject = firstCheck.fromGameObject,
                                fromPosition = firstCheck.fromPosition w,
                            }
                        }
                    }
                }

                moveConstrain.Value *= Math.Abs(patchTarget.transform.localScale.x);
            })*/
    ];
}
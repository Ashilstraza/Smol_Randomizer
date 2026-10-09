using HarmonyLib;

using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;

using Smol_Randomizer.Patchers.Enemy;
using Smol_Randomizer.Patchers.FSM_Actions;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

using UnityEngine;

using Translate = HutongGames.PlayMaker.Actions.Translate;

namespace Smol_Randomizer.Randomizers;

internal partial class Enemy_Size_Rando
{
    /// <summary>Returns an Action to shift an enemy to its base</summary>
    public static Action<FsmState, object[]> CreateShiftToBaseDelegate(
        bool atStart = true,
        float rotateAdjust = 0,
        Vector2? backupSize = null,
        bool reverseX = false,
        bool reverseY = false,
        float magicNumber = float.MinValue,
        bool tempInvincible = false,
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
            bool wasInvincible = false;

            if (tempInvincible)
            {
                if (healthManager != null)
                {
                    wasInvincible = healthManager.IsInvincible;
                    healthManager.IsInvincible = tempInvincible;
                }
                else
                    tempInvincible = false;
            }

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
                tempInvincible = tempInvincible,
                wasInvincible = wasInvincible,
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

    /// <summary>Dictionary containing various FSM patches.</summary>
    private static readonly HashSet<IEnemyFSMPatch> enemyFSMPatches = new()
    {
#region Many Places

    #region Pilgrims

        new EnemyStatePatch([   // pilgrim_behaviour - Init
                                "Pilgrim 01 Judge Buddy",
                                "Pilgrim StaffWielder"],
            "Init",
            CreateShiftToBaseDelegate(),
            fsmName: "pilgrim_behaviour"),

        new EnemyStatePatch([   // Control - Init
                                "Pilgrim Hiker",
                                "Pilgrim BellThrower",
                                "Pilgrim Moss Spitter",
                                "Pilgrim Fisher Enemy",
            ],
            "Init",
            CreateShiftToBaseDelegate(),
            fsmName: "Control"),

        new EnemyStatePatchSet("Pilgrim 01",
            [new EnemyStatePatch("",    // Pilgrim 01 drops from celing in Wanderer Chapel
                "Drop Pause",
                CreateShiftToBaseDelegate(reverseY: true)),
            new EnemyStatePatch("",
                "Init",
                CreateShiftToBaseDelegate(tempInvincible: true)) // Vanishes in Greymoor_13 at 200%
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

        new EnemyStatePatch("Pilgrim 05",
            "Init",
            CreateShiftToBaseDelegate(),
            fsmName: "Attack"),

        new EnemyStatePatch("Pilgrim Fly",
            "Start Check",
            CreateShiftToBaseDelegate(tempInvincible: true),
            fsmName: "Control"),

        new EnemyStatePatchSet("Pilgrim Bellthrower Fly",
            [new("Pilgrim Bellthrower Fly",
                "Wall Cling",
                CreateShiftToBaseDelegate(rotateAdjust: 90f, magicNumber: 0.009856f),
                fsmName: "Control")
            ]),
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
                                // Many Places
                                "Mite Heavy"
                            ],
            "Init",
            CreateShiftToBaseDelegate(),
            fsmName: "Control"),

        new EnemyStatePatch([   // Behaviour - Init
                                // Deep Docks
                                "Shield Dockworker",
                                "Dock Bomber",
                                // Greymoor
                                "Crow",
                                "Crowman Juror Tiny",
                                "Gnat Giant",
                                // Many Places
                                ],
            "Init",
            CreateShiftToBaseDelegate(),
            fsmName:  "Behaviour"),

        new EnemyStateActionPatch("Rhino",// check
            ["Charge Down", "Charge Up"],
            typeof(RayCast2dV2),
            delegate (FsmStateAction action, object[] param)
            {
                var rayCast = (RayCast2dV2)action;

                rayCast.distance.Value = ((GameObject)param[0]).transform.localScale.x * rayCast.distance.Value;
            },
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

        new EnemyStatePatch("Citadel Bat",
            "Init",
            CreateShiftToBaseDelegate(rotateAdjust: 180f),
            fsmName: "Control"),
#endregion Many Places

#region Weavenests

         new EnemyStatePatch("Weaver Servitor",
            "Init",
            CreateShiftToBaseDelegate(magicNumber: -0.0055f),
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
            delegate (FsmStateAction action, object[] param)
            {
                ((RayCast2dV2)action).distance.Value *= Math.Abs(((GameObject)param[0]).transform.GetScaleX());
            },
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

        new EnemyStatePatch("Farmer Catcher", // Some reason they get initialized not at init?
            "Init",
            CreateShiftToBaseDelegate(),
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

#region Whisp Thicket
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

#endregion Whisp Thicket

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

        new EnemyStateActionPatch("Bell Goomba", // Fixes them getting stuck and vibrating
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
            CreateShiftToBaseDelegate(rotateAdjust: 180f)),

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

        // TODO: Patch Conch Drillers

#endregion Blasted Steps & Sands of Karak

#region Sinner's Road
        new EnemyStatePatch("Dustroach FG",
            "Init",
            delegate(FsmState action, object[] param)
            {
                GameObject dustroach = (GameObject)param[0];

                string name = CuteRandoCore.CullName((string)param[1]);

                Instance.RandomizeSize(false, dustroach.transform);
                AdjustChildren(dustroach, name);

                CreateShiftToBaseDelegate(backupSize: new(2.6999f, 1.2145f), magicNumber: -0.01f)(action, param);
            },
            fsmName: "Control"),

        new EnemyStateActionPatch("Dustroach", // Fixes scale issues
            "ScrabbleJump Air",
            typeof(RayCast2dV2),
            delegate (FsmStateAction action, object[] param)
            {
                ((RayCast2dV2)action).distance.Value *= Math.Abs(((GameObject)param[0]).transform.GetScaleX());
            },
            fsmName: "Control"),

        new EnemyStateActionPatch("Roachfeeder Tall",
            "Watch",
            typeof(DistanceFly),
            delegate(FsmStateAction action, object[] param)
            {
                ((DistanceFly)action).distance.Value *= Math.Abs(((GameObject) param[0]).transform.GetScaleX());
            })
#endregion Sinner's Road

#region Bilewater

        // TODO Check These Zones
#endregion Bilewater
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
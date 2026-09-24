using System;
using System.Collections.Generic;
using System.Linq;

using HarmonyLib;

using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;

using Smol_Randomizer.Patchers.Enemy;
using Smol_Randomizer.Patchers.FSM_Actions;

using UnityEngine;

using Translate = HutongGames.PlayMaker.Actions.Translate;

namespace Smol_Randomizer.Randomizers;

internal partial class Enemy_Size_Rando
{
    /// <summary>Returns an Action to shift an enemy to its base</summary>
    public static Action<FsmState, object[]?> CreateShiftToBaseDelegate(
        bool atStart = true,
        float rotateAdjust = 0,
        Vector2? backupSize = null,
        bool reverseX = false,
        bool reverseY = false,
        float magicNumber = float.MinValue,
        bool tempInvincible = false)
    {
        return delegate (FsmState state, object[]? param)
        {
            if (param == null) return;
            Vector2 originalScale = (param.Length >= 3) ? (Vector2)param[2] : ((GameObject)param[0]).transform.localScale;
            GameObject enemy = (GameObject)param[0];
            bool wasInvincible = false;

            if (tempInvincible)
            {
                HealthManager healthManager = enemy.GetComponent<HealthManager>();
                if (healthManager != null)
                {
                    wasInvincible = healthManager.IsInvincible;
                    healthManager.IsInvincible = tempInvincible;
                }
                else
                    tempInvincible = false;
            }

            ShiftPosToBase shift = new()
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
                wasInvincible = wasInvincible
            };
            if (atStart)
            {
                FsmStateAction[] bassAckwards = [shift];
                state.Actions = bassAckwards.AddRangeToArray(state.Actions);
            }
            else state.Actions = state.Actions.AddItem(shift).ToArray();
        };
    }

    //TODO Figure it out
    public static Action<FsmState, object[]?> CreateRayCastBaseDelegate(
        bool atStart = true,
        Vector2? backupSize = null)
    {
        if (backupSize == null) backupSize = Vector2.zero;

        return delegate (FsmState state, object[]? param)
        {
            if (param == null) return;

            GameObject obj = (GameObject)param[0];
            BoxCollider2D col = obj.GetComponent<BoxCollider2D>();

            float height = col?.size.y ?? backupSize.Value.y;
            double rotation = Math.PI * obj.transform.rotation.eulerAngles.z / 180;
            Vector2 point = new()
            {
                x = obj.transform.position.x - (float)Math.Sin(rotation) * (height / 2),
                y = obj.transform.position.y - (float)Math.Cos(rotation) * (height / 2)
            };

            FsmVector2 basePoint = new("Smol Base Point");
            FsmFloat baseX = new("Smol Base Point X");
            FsmFloat baseY = new("Smol Base Point Y");

            state.Fsm.Variables.Vector2Variables = state.Fsm.Variables.Vector2Variables.AddToArray(basePoint);
            state.Fsm.Variables.FloatVariables = state.Fsm.Variables.FloatVariables.AddRangeToArray([baseX, baseY]);

            RayCast2dV2 rayCast = new()
            {
                fromPosition = point,
                direction = new Vector2((float)Math.Sin(rotation),
                (float)Math.Cos(rotation) * -1),
                space = Space.World,
                distance = 10f,
                storeHitPoint = basePoint,
                repeatInterval = 0,
                layerMask = [LayerMask.NameToLayer("Terrain")],
                minDepth = 10,
                maxDepth = 100,
                invertMask = false,
                storeDidHit = true,
                storeHitObject = new(),
                storeHitDistance = 0,
                storeHitNormal = new(),
                storeDistance = 0,
                debug = false
            };

            GetVector2XY getVector = new()
            {
                vector2Variable = basePoint,
                storeX = baseX,
                storeY = baseY
            };

            SetPosition2D setPosition = new()
            {
                GameObject = new()
                {
                    GameObject = obj
                },
                X = baseX,
                Y = baseY,
                Vector = new()
            };

            if (atStart)
            {
                FsmStateAction[] bassAckwards = [rayCast, getVector,/*setPosition*/];
                state.Actions = bassAckwards.AddRangeToArray(state.Actions);
            }
            else state.Actions = state.Actions.AddRangeToArray([rayCast, getVector, /*setPosition*/]).ToArray();
        };
    }

    /// <summary>Dictionary containing various FSM patches.</summary>
    private static readonly HashSet<IEnemyFSMPatch> enemyFSMPatches = new()
    {
#region Many Places

    #region Pilgrims

        new EnemyStatePatch([   // LatePatch - ReInit - pilgrim_behaviour - Init
                                "Pilgrim 01 Judge Buddy",
                                "Pilgrim StaffWielder"],
            "Init",
            CreateShiftToBaseDelegate(),
            latePatch: true,
            fsmName: "pilgrim_behaviour"),

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
                    delegate (FsmState state, object[]? param)
                    {
                        if (param == null) return;

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
                    latePatch: true,
                    reInit: false,
                    fsmName: "Attack"),
                new EnemyStatePatch("", // Fix for squish and scale
                    ["Reset Scale", "Roll Rev", "Bounce Up"],
                    delegate (FsmState state, object[]? param)
                    {
                        if (param == null) return;

                        SetScale setScale = (SetScale)state.Actions.Where(action => action.GetType().Equals(typeof(SetScale))).First();
                        setScale.y = state.Fsm.Variables.FloatVariables.FirstOrDefault(var => var.Name.Equals("Y Scale")) ?? ((GameObject)param[0]).transform.GetScaleZ();
                    },
                    latePatch: true,
                    reInit: false,
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

        new EnemyStatePatch("Pilgrim 05", // TODO Check?
            "Init",
            CreateShiftToBaseDelegate(),
            fsmName: "Attack"),

        new EnemyStatePatch("Pilgrim Fly",
            ["Ambush Ready","Sleep"],
            CreateShiftToBaseDelegate(tempInvincible: true),
            fsmName: "Control"),

        new EnemyStatePatchSet("Pilgrim Bellthrower Fly",
            [new("Pilgrim Bellthrower Fly",
                "Wall Cling",
                CreateShiftToBaseDelegate(rotateAdjust: 90f, magicNumber: 0.009856f),
                fsmName: "Control")
            ]),
    #endregion Pilgrims

        new EnemyStatePatch([   // Hunter's March & Far Fields
                                "Bone Hunter",
                                "Bone Hunter Child",
                                "Bone Hunter Tiny",
                                // Shellwood
                                "Shellwood Goomba", // OK
                                // Greymoor
                                "Mite", // OK
                                // Many Places
                            ],
            "Init",
            CreateShiftToBaseDelegate(),
            fsmName: "Control"),

        new EnemyStatePatch([
                                // Many Places Hunter's March & Far Fields
                                "Bone Hunter Fly",
                                // Shellwood
                                "Pilgrim Fisher Enemy"],
            "Init",
            CreateShiftToBaseDelegate(),
            fsmName: "Control"),

        new EnemyStatePatch([   // Control - Init
                                "Bone Goomba",
                                "Bone Goomba Large"],
            "Init",
            CreateShiftToBaseDelegate(),
            fsmName: "Control"),

        new EnemyStatePatch("Bone Thumper", // OK
            "Init",
            CreateShiftToBaseDelegate(),
            fsmName: "Control"),

        new EnemyStateActionPatch("Rhino",
            ["Charge Down", "Charge Up"],
            typeof(RayCast2dV2),
            delegate (FsmStateAction action, object[]? param)
            {
                if(param == null) return;

                var rayCast = (RayCast2dV2)action;

                rayCast.distance.Value = ((GameObject)param[0]).transform.localScale.x * rayCast.distance.Value;
            },
            latePatch: true,
            reInit: false,
            fsmName: "Control"),

        new EnemyStatePatch("Blade Spider Hang", // OK
            "Init",
            CreateShiftToBaseDelegate(reverseY: true),
            fsmName: "Behaviour"),
#endregion Many Places

#region Weavenests

         new EnemyStatePatch("Weaver Servitor", // OK
            "Init",
            CreateShiftToBaseDelegate(magicNumber: -0.0055f),
            fsmName: "Control"),
#endregion Weavenests

#region Moss Grotto & Mosshome

        new EnemyStatePatch("MossBone Crawler", // OK
            "Init",
             CreateShiftToBaseDelegate(),
            fsmName: "Noise Reaction"),

        new EnemyStatePatch("Aspid Collector", // OK
            "Init",
            CreateShiftToBaseDelegate(rotateAdjust: 90f, magicNumber: -0.004f),
            fsmName: "Control"),

        new EnemyStateActionPatch("Pilgrim Moss Spitter", // OK
            ["Attempt Larger Jump", "Escape Antic"],
            typeof(GetGroundPointClampedToEdge),
            delegate(FsmStateAction action, object[]? param)
            {
                if (param == null) return;

                var minJumpDistance = ((GetGroundPointClampedToEdge)action).MinJumpDistance;

                minJumpDistance.Value = minJumpDistance.Value * Math.Abs(((GameObject)param[0]).transform.localScale.x);
            },
            latePatch: true,
            reInit: false,
            fsmName: "Control"),
#endregion Moss Grotto & Mosshome

#region Bone Bottom & Marrow

        // TODO Check These Zones
        new EnemyStatePatchSet("Bone Crawler",
            [   new("Bone Crawler","Init",CreateShiftToBaseDelegate(),fsmName: "Control"),
                new("Bone Crawler","Check Roof",CreateShiftToBaseDelegate(atStart: false),fsmName: "Control")]),
#endregion Bone Bottom & Marrow

#region Wormways

        new EnemyStatePatch("Bone Worm", // Burrow height after drop
            "Position",
            delegate(FsmState state, object[]? param)
            {
                if (param == null) return;
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
            "Wall?",
            CreateShiftToBaseDelegate(magicNumber: -0.004f),
            fsmName: "Control",
            latePatch: true,
            reInit: false),

        new EnemyStatePatch("Roof Crab",
            "Init",
            CreateShiftToBaseDelegate(magicNumber: 0.039928f),
            fsmName: "Control"),
#endregion Wormways

#region Deep Docks

        new EnemyStatePatch(["Dock Flyer", "Shield Dockworker"],
            "Init",
            CreateShiftToBaseDelegate(),
            fsmName: "Behaviour"),

        new EnemyStatePatch("Dock Bomber",
            "Init",
            CreateShiftToBaseDelegate(rotateAdjust: 180f),
            fsmName: "Behaviour"),

        new EnemyStatePatchSet("Dock Worker", // Halt velocity and shift
            [new("Dock Worker",
                "Init",
                delegate(FsmState state, object[]? param)
                {
                    if(param == null) return;

                    SetVelocity2d velocity = new()
                    {
                        gameObject = new()
                        {
                            GameObject = (GameObject)param[0]
                        },
                        vector = Vector2.zero,
                        x = 0,
                        y = 0,
                    };

                    CreateShiftToBaseDelegate()(state, param);

                    FsmStateAction[] bassAckwards = [velocity];
                    state.Actions = bassAckwards.AddRangeToArray(state.Actions);
                },
                fsmName: "Behaviour"),
            new("Dock Worker",
                "Battle Dormant",
                CreateShiftToBaseDelegate(),
                fsmName: "Behaviour"),
            new("Dock Worker",
                "Start Dig",
                delegate(FsmState action, object[]? param)
                {
                    if(param == null) return;

                    if (((GameObject)param[0]).name.Equals("Dock Worker (1)") &&((GameObject)param[0]).scene.name.Equals("Bone_East_03"))
                    { // One worker is too close to coals
                        CreateShiftToBaseDelegate(rotateAdjust: 320f)(action, param);
                    }
                    else
                    {
                        CreateShiftToBaseDelegate(rotateAdjust: 270f)(action, param);
                    }
                },
                fsmName: "Behaviour"),
            new("Dock Worker", // Signis & Gronn fight intro
                "Idle",
                CreateShiftToBaseDelegate(),
                fsmName: "Control")]),

        new EnemyStatePatch("Tar Slug Huge",
            "Init",
            delegate (FsmState action, object[]? param)
            {
                if(param == null) return;

                CreateShiftToBaseDelegate()(action, param);
                if (((GameObject)param[0]).name.Equals("Tar Slug Huge") &&((GameObject)param[0]).scene.name.Equals("Dock_11"))
                { // One Tar slug is special
                    CreateShiftToBaseDelegate()(action, param);
                }
            }),
#endregion Deep Docks

#region Hunters March & Far Fields

        // TODO Check These Zones
        new EnemyStateActionPatch("Bone Hunter Buzzer",
            "Roost Start",
            typeof(FloatAdd),
            delegate (FsmStateAction action, object[]? param)
            {
                if(param == null) return;
                ((FloatAdd)action).add.Value *= Math.Abs(((GameObject)param[0]).transform.GetScaleX());
            },
            latePatch: true,
            reInit: false,
            fsmName: ""),

        new EnemyStatePatch("Bone Hunter",
            "Ambush Ready",
            CreateShiftToBaseDelegate(atStart: false),
            fsmName: "Control"),

        new EnemyStatePatch("Bone Hunter Child",
            ["Reset", "Dormant"],
            CreateShiftToBaseDelegate(atStart: false),
            fsmName: "Control"),
#endregion Hunters March & Far Fields

#region Greymoor

        new EnemyStateActionPatch("Farmer Scissors", // Fixes distance check
            "Do Step",
            typeof(RayCast2dV2),
            delegate (FsmStateAction action, object[]? param)
            {
                if (param == null) return;
                ((RayCast2dV2)action).distance.Value *= Math.Abs(((GameObject)param[0]).transform.GetScaleX());
            },
            latePatch: true,
            reInit: false,
            fsmName: "Control"),

        new EnemyStateActionPatch("Farmer Centipede", // Fixes distance check
            "Idle Chase",
            typeof(DistanceWalk),
            delegate (FsmStateAction action, object[]? param)
            {
                if (param == null) return;
                ((DistanceWalk)action).distance.Value *= Math.Abs(((GameObject)param[0]).transform.GetScaleX());
            },
            latePatch: true,
            reInit: false,
            fsmName: "Control"),

        new EnemyStateActionPatch(["Crowman", "Crowman Juror"], // Fixes scale issues
            "Start Rest",
            typeof(RandomFloatEither),
            delegate (FsmStateAction action, object[]? param)
            {
                if (param == null) return;
                Transform transform = ((GameObject)param[0]).transform;
                RandomFloatEither rfe = (RandomFloatEither)action;

                rfe.value1.Value = rfe.value1.Value > 0
                    ? transform.localScale.x
                    : transform.localScale.x * -1;
                rfe.value2.Value = rfe.value2.Value > 0
                    ? transform.localScale.x
                    : transform.localScale.x * -1; ;
            },
            latePatch: true,
            reInit: false,
            fsmName: "Control"),

        new EnemyStateActionPatch(["Crow", "Crowman Juror Tiny"], // Fix Swoop
            "Swoop Down",
            typeof(FloatOperator),
            delegate (FsmStateAction action, object[]? param)
            {
                if (param == null) return;
                Transform transform = ((GameObject)param[0]).transform;
                FloatOperator fo = (FloatOperator)action;

                fo.float1.Value = fo.float1.Value * (1/Math.Abs(transform.localScale.x));
            },
            latePatch: true,
            reInit: false,
            fsmName: "Behaviour"),
#endregion Greymoor

#region Bellways

        new EnemyStateActionPatch("Bell Goomba", // Good
            ["Set To Ground","Set To Wall L", "Set To Wall R", "Set To Roof"],
            typeof(Translate),
            delegate(FsmStateAction action, object[]? param)
            {
                if(param == null) return;

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
            latePatch: true,
            fsmName: "Control"),

        new EnemyStateActionPatch("Bell Goomba", // Fixes them getting stuck and vibrating
            "Surface Dig",
            typeof(CheckXPosition),
            delegate(FsmStateAction action, object[]? param)
            {
                ((CheckXPosition)action).everyFrame = false;
            },
            fsmName: "Control"),
#endregion Bellways

#region Shellwood

        new EnemyStatePatch("Shellwood Goomba Flyer", // OK
            "Init",
            CreateShiftToBaseDelegate(rotateAdjust: 180f),
            latePatch: true),

        new EnemyStatePatch("Bloom Puncher", // OK
            "Init",
            CreateShiftToBaseDelegate(rotateAdjust: 270f),
            fsmName: "Control"),

        new EnemyStatePatchSet("Stick Insect Charger", // Shellwood_20 stick insect doesn't start with a collider
            [new("",
                "Init",
                CreateShiftToBaseDelegate(backupSize: new(1.8438f, 1.9375f))),
            new("",
                "Initial Position",
                CreateShiftToBaseDelegate(backupSize: new(1.8438f, 1.9375f), magicNumber: -0.016f))
            ],
            fsmName: "Behaviour Base"),

        new EnemyStatePatch("Stick Insect Flyer", // OK
            "Init",
            CreateShiftToBaseDelegate(rotateAdjust: 270f, magicNumber: -0.005f),
            latePatch: true,
            fsmName: "Control"),

        new EnemyStatePatch("Stick Insect", // OK
            "Init",
            CreateShiftToBaseDelegate(),
            latePatch: true,
            fsmName: "Behaviour Base"),
#endregion Shellwood

#region Blasted Steps

        // TODO Check These Zones
#endregion Blasted Steps

#region Sinner's Road

        // TODO Check These Zones
        new EnemyStateActionPatch("Dustroach", // Fixes scale issues
            "ScrabbleJump Air",
            typeof(RayCast2dV2),
            delegate (FsmStateAction action, object[]? param)
            {
                if (param == null) return;
                ((RayCast2dV2)action).distance.Value *= Math.Abs(((GameObject)param[0]).transform.GetScaleX());
            },
            latePatch: true,
            reInit: false,
            fsmName: "Control"),
#endregion Sinner's Road

#region Bilewater

        // TODO Check These Zones
#endregion Bilewater
    };

    /// <summary>Dictionary containing various enemy non-FSM patches</summary>
    private static readonly List<IEnemyObjectPatch> enemyObjectPatches =
    [
        new EnemyObjectPatch("Farmer Centipede", // Fix emerge point
            delegate(GameObject patchTarget, object[]? param)
            {
                var height = patchTarget.GetComponent<BoxCollider2D>().size.y;
                var digEmerge = patchTarget.transform.Find("Pt DigEmerge");

                digEmerge.transform.localPosition = digEmerge.transform.localPosition with { y = height * Math.Abs(patchTarget.transform.localScale.y) * -1 };
            }),
        new EnemyObjectPatch(["Crowman Juror", "Crowman Dagger Juror", "Crowman Juror Tiny"], // Fix height constrain
            delegate(GameObject patchTarget, object[]? param)
            {
                if(!patchTarget.scene.name.Equals("Room_CrowCourt_02")) return;

                var height = patchTarget.GetComponent<BoxCollider2D>().size.y;
                var constrain = patchTarget.GetComponent<ConstrainPosition>();

                constrain.yMin = 20f + (height * Math.Abs(patchTarget.transform.localScale.y)) / 2;
            }),
        new EnemyObjectPatch("Pond Skater", // Fix skate height
            delegate(GameObject patchTarget, object[]? param)
            {
                patchTarget.transform.position = patchTarget.transform.position with
                {
                    y = ShiftPosToBase.ShiftN(
                        patchTarget.transform.position.y,
                        patchTarget.transform.localScale.y,
                        patchTarget.GetComponent<BoxCollider2D>().size.y / 2,
                        patchTarget.GetComponent<BoxCollider2D>().offset.y,
                        Math.Cos(Math.PI * patchTarget.transform.rotation.eulerAngles.z / 180))
                };
            }),
        new EnemyObjectPatch("Bone Crawler", // One has a bad constrain position, fixed by increasing range and setting Crawler's rayDownFrontPadding higher
            delegate(GameObject patchTarget, object[]? param)
            {
                if(patchTarget.scene.name.Equals("Bone_10") && patchTarget.name.Equals("Bone Crawler (4)")){
                    ConstrainPosition constrain = patchTarget.GetComponent<ConstrainPosition>();
                    constrain.xMax = 45f;
                    constrain.xMin = 25f;
                    CuteRandoCore.TraverseCreator(patchTarget.GetComponent<Crawler>(), "rayDownFrontPadding").SetValue(3f);
                }
            }),
        new EnemyObjectPatch("Fields Flock Flyer", // Drop Flock Flyers to ground
            delegate(GameObject patchTarget, object[]? param)
            {
                ShiftPosToBase.Shift(patchTarget, Vector2.one);
            }),
        /*new EnemyObjectPatch("Bone Roller", //TODO: Needs a lot of fixing!
            delegate(GameObject patchTarget, object[]? param)
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
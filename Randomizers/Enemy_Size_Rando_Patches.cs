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
    /// <param name="rotateAdjust">    Optional: Changes the direction the shift should come from in degrees</param>
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
    /// <param name="overrideAdjusted">
    /// Optional: <see langword="true" /> if the method will be intentionally called multiple times
    /// </param>
    /// <returns>The <see langword="delegate" /> for creating a new <see cref="ShiftPosToBase" /><see cref="FsmStateAction" /></returns>
    public static Action<FsmState, object[]> ShiftToBaseDelegate(
        bool atStart = true,
        float rotateAdjust = 0,
        Vector2? backupSize = null,
        bool reverseX = false,
        bool reverseY = false,
        float magicNumber = 0f,
        bool skipRaycast = false,
        bool cancelYVelocity = true,
        bool overrideAdjusted = false)
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
                cancelYVelocity = cancelYVelocity,
                overrideAdjusted = overrideAdjusted
            };

            if (atStart)
            {
                FsmStateAction[] bassAckwards = [shift];
                state.Actions = bassAckwards.AddRangeToArray(state.Actions);
            }
            else state.Actions = state.Actions.AddToArray(shift);
        };
    }

    /// <summary>Record containing the settings for adjusting a field within an <see cref="FsmStateAction" /></summary>
    public record ScaleValueSetting
    {
        /// <summary>Enum containing the <see cref="Type" /> of <see cref="FsmStateAction" /> to be patched</summary>
        public TargetType OperatorType;

        /// <summary>The target value field to be adjusted</summary>
        public ValueTarget Target;

        /// <summary>
        /// <see langword="true" /> if the value should scale with the object, <see langword="false" /> if it should
        /// stay the same regardless of scale
        /// </summary>
        public bool ScaleWithObject = false;

        /// <summary>If only objects with a scale less than 1 should have the value be scaled</summary>
        public bool OnlyShort = false;

        /// <summary>The</summary>
        public record ValueTarget
        {
            /// <summary>The value to be scaled</summary>
            public string Target;
            /// <summary>The axis to scale by</summary>
            public AxisDirections Axis;

            /// <summary>The axis that can be adjusted</summary>
            public enum AxisDirections
            {
                x, y, z
            }

            /// <summary>Returns the absolute scale of the axis</summary>
            /// <param name="transform">The <see cref="Transform" /> to get the scale from</param>
            /// <returns>The absolute value of the axis</returns>
            /// <exception cref="ArgumentException">Thrown if <see cref="AxisDirections" /> is null or invalid</exception>
            public float GetAxisResult(Transform transform)
            {
                switch (Axis)
                {
                    case AxisDirections.x:
                        return transform.GetAbsScaleX();

                    case AxisDirections.y:
                        return transform.GetAbsScaleY();

                    case AxisDirections.z:
                        return transform.GetAbsScaleZ();

                    default:
                        throw new ArgumentException("GetAxisResult failed. Axis was null or invalid?", nameof(AxisDirections));
                }
            }
        }

        /// <summary>The various types of actions that can be adjusted</summary>
        public enum TargetType
        {
            FloatOperator,
            FloatTestToBool,
            Translate,
            RayCast2dV2,
            DistanceWalk,
            FloatAdd,
            FloatMultiply
        }
    }

    /// <summary>Creates a delegate patch that scales an <see cref="FsmStateAction" /> by the size of the object</summary>
    /// <param name="setting">A set of settings for adjusting the <see cref="FsmStateAction" /></param>
    /// <returns>A patch that adjusts the <see cref="FsmStateAction" /></returns>
    /// <exception cref="ArgumentException">
    /// Thrown if a setting within the <see cref="ScaleValueSetting" /> is invalid
    /// </exception>
    /// <exception cref="NotImplementedException">
    /// Thrown if the <see cref="ScaleValueSetting.OperatorType" /> has not been implemented yet
    /// </exception>
    public static Action<FsmStateAction, object[]> ScaleValueDelegate(ScaleValueSetting setting)
    {
        switch (setting.OperatorType)
        {
            case ScaleValueSetting.TargetType.FloatOperator:
                return delegate (FsmStateAction action, object[] param)
                {
                    float f = setting.Target.GetAxisResult(((GameObject)param[0]).transform);
                    FloatOperator act = (FloatOperator)action;

                    switch (setting.Target.Target)
                    {
                        case "float1":
                            act.float1.Value *= setting.ScaleWithObject ? f : 1 / f;
                            break;

                        case "float2":
                            act.float2.Value *= setting.ScaleWithObject ? f : 1 / f;
                            break;

                        default:
                            throw new ArgumentException($"Invalid Target for FloatOperator: {setting.Target.Target}", nameof(setting.Target.Target));
                    }
                };
            case ScaleValueSetting.TargetType.FloatTestToBool:
                return delegate (FsmStateAction action, object[] param)
                {
                    float f = setting.Target.GetAxisResult(((GameObject)param[0]).transform);
                    FloatTestToBool act = (FloatTestToBool)action;

                    switch (setting.Target.Target)
                    {
                        case "float1":
                            act.float1.Value *= setting.ScaleWithObject ? f : 1 / f;
                            break;

                        case "float2":
                            act.float2.Value *= setting.ScaleWithObject ? f : 1 / f;
                            break;

                        default:
                            throw new ArgumentException($"Invalid Target for FloatTestToBool: {setting.Target.Target}", nameof(setting.Target.Target));
                    }
                };
            case ScaleValueSetting.TargetType.Translate:
                return delegate (FsmStateAction action, object[] param)
                {
                    float f = setting.Target.GetAxisResult(((GameObject)param[0]).transform);
                    Translate act = (Translate)action;

                    switch (setting.Target.Target)
                    {
                        case "x":
                            act.x.Value *= setting.ScaleWithObject ? f : 1 / f;
                            break;

                        case "y":
                            act.y.Value *= setting.ScaleWithObject ? f : 1 / f;
                            break;

                        case "z":
                            act.z.Value *= setting.ScaleWithObject ? f : 1 / f;
                            break;

                        default:
                            throw new ArgumentException($"Invalid Target for Translate: {setting.Target.Target}", nameof(setting.Target.Target));
                    }
                };
            case ScaleValueSetting.TargetType.RayCast2dV2:
                return delegate (FsmStateAction action, object[] param)
                {
                    float f = setting.Target.GetAxisResult(((GameObject)param[0]).transform);
                    RayCast2dV2 act = (RayCast2dV2)action;

                    if (setting.OnlyShort && f >= 1f)
                        return;

                    act.distance.Value *= setting.ScaleWithObject ? f : 1 / f;
                };
            case ScaleValueSetting.TargetType.DistanceWalk:
                return delegate (FsmStateAction action, object[] param)
                {
                    float f = setting.Target.GetAxisResult(((GameObject)param[0]).transform);
                    DistanceWalk act = (DistanceWalk)action;

                    switch (setting.Target.Target)
                    {
                        case "distance":
                            act.distance.Value *= setting.ScaleWithObject ? f : 1 / f;
                            break;

                        case "speed":
                            act.speed.Value *= setting.ScaleWithObject ? f : 1 / f;
                            break;

                        case "range":
                            act.range.Value *= setting.ScaleWithObject ? f : 1 / f;
                            break;

                        default:
                            throw new ArgumentException($"Invalid Target for DistanceWalk: {setting.Target.Target}", nameof(setting.Target.Target));
                    }
                };
            case ScaleValueSetting.TargetType.FloatAdd:
                return delegate (FsmStateAction action, object[] param)
                {
                    float f = setting.Target.GetAxisResult(((GameObject)param[0]).transform);

                    ((FloatAdd)action).add.Value *= setting.ScaleWithObject ? f : 1 / f;
                };
            case ScaleValueSetting.TargetType.FloatMultiply:
                return delegate (FsmStateAction action, object[] param)
                {
                    float f = setting.Target.GetAxisResult(((GameObject)param[0]).transform);

                    ((FloatMultiply)action).multiplyBy.Value *= setting.ScaleWithObject ? f : 1 / f;
                };
            default:
                throw new NotImplementedException();
        }
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
            ShiftToBaseDelegate(),
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
            ShiftToBaseDelegate(),
            fsmName: "Control"),

        new EnemyStatePatch([   // Attack - Init
                                "Pilgrim 05",
                                "Pilgrim Stomper Song",
                                "Song Pilgrim 03"],
            "Init",
            ShiftToBaseDelegate(),
            fsmName: "Attack"),

        new EnemyStatePatchSet("Pilgrim 01",
            [new EnemyStatePatch("",    // Pilgrim 01 drops from celing in Wanderer Chapel
                "Drop Pause",
                ShiftToBaseDelegate(reverseY: true)),
            new EnemyStatePatch("",
                "Init",
                ShiftToBaseDelegate())
            ],
            fsmName: "pilgrim_behaviour"),

        new EnemyStatePatchSet("Pilgrim 02", // Pilgrim 02 exists
            [
                new EnemyStatePatch("",
                    ["Init", "Dormant"],
                    ShiftToBaseDelegate()),
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
                            state.Fsm.Variables.AddVariableLookup(yScale);
                        }

                        if(ySquash == null)
                        {
                            ySquash = new("Y Squash")
                                {
                                    UseVariable = true
                                };
                            state.Fsm.Variables.FloatVariables = state.Fsm.Variables.FloatVariables.AddToArray(ySquash);
                            state.Fsm.Variables.AddVariableLookup(ySquash);
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
            ShiftToBaseDelegate(magicNumber: -0.01f),
            fsmName: "pilgrim_behaviour"),

        new EnemyStatePatch("Pilgrim 04", // Pilgrin 04 has no collider in Wanderer Chapel before ambush
            "Init",
            ShiftToBaseDelegate(backupSize: new(1.1094f, 1.8125f)),
            fsmName: "pilgrim_behaviour"),

        new EnemyStatePatch("Pilgrim Fly",
            "Start Check",
            ShiftToBaseDelegate(),
            fsmName: "Control"),

        new EnemyStatePatch("Pilgrim Bellthrower Fly",
            "Wall Cling",
            ShiftToBaseDelegate(rotateAdjust: 90f, magicNumber: 0.009856f),
            fsmName: "Control"),

        new EnemyStateActionPatch("Song Pilgrim 03",
            "Move Start",
            typeof(RandomFloat),
            delegate(FsmStateAction action, object[] param)
            {
                RandomFloat rf = (RandomFloat)action;
                GameObject gameObject = (GameObject)param[0];
                rf.min.Value *= gameObject.transform.GetAbsScaleX();
                rf.max.Value *= gameObject.transform.GetAbsScaleX();
            },
            fsmName: "Attack"),

        new EnemyStatePatch("Song Pilgrim 01",
            "Init",
            delegate(FsmState state, object[] param)
            {
                if(((GameObject)param[0]).scene.name.Equals("Hang_04_boss")) // One in the HHG falls through the floor at minimum size
                    ShiftToBaseDelegate(magicNumber: -0.02f)(state, param);
                else
                    ShiftToBaseDelegate()(state, param);
            },
            fsmName: "Attack"),

        new EnemyStatePatch("Pilgrim 03 Understore",
            ["Init", "Dormant"],
            delegate (FsmState state, object[] param)
            {
                if(((GameObject)param[0]).scene.name.Equals("Under_10"))
                    ShiftToBaseDelegate(magicNumber: -0.012f)(state, param);
                else
                    ShiftToBaseDelegate()(state, param);
            },
            fsmName: "pilgrim_behaviour"),

        new EnemyStatePatch("Pilgrim Staff Understore",
            ["Init", "Falling"],
            delegate (FsmState state, object[] param)
            {
                if(((GameObject)param[0]).scene.name.Equals("Under_10"))
                    ShiftToBaseDelegate(magicNumber: -0.023f)(state, param);
                else
                    ShiftToBaseDelegate()(state, param);
            },
            fsmName: "pilgrim_behaviour"),

        new EnemyStateActionPatch("Pilgrim Staff Understore",
            "Jump",
            typeof(RandomFloatEither),
            delegate(FsmStateAction action, object[] param)
            {
                RandomFloatEither randomFloat = (RandomFloatEither)action;
                float scale = ((GameObject)param[0]).transform.GetAbsScaleX();

                randomFloat.value1.Value *= scale;
                // randomFloat.value2.Value *= scale; Only scale the first value because it seems to work better that way
            },
            fsmName:"Attack"),

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
                                // Underworks Citadel
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
            ShiftToBaseDelegate(),
            fsmName: "Control"),

        new EnemyStatePatch([   // Control - Init - Rotate 180
                                // Shellwood
                                "Shellwood Goomba Flyer",
                                // Many Places
                                "Citadel Bat",],
            "Init",
            ShiftToBaseDelegate(rotateAdjust: 180f),
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
            ShiftToBaseDelegate(),
            fsmName:  "Behaviour"),

        new EnemyStateActionPatch("Rhino",// TODO: check
            ["Charge Down", "Charge Up"],
            typeof(RayCast2dV2),
             ScaleValueDelegate(new(){
                        OperatorType = ScaleValueSetting.TargetType.RayCast2dV2,
                        Target = new(){
                            Axis = ScaleValueSetting.ValueTarget.AxisDirections.y } }),
            fsmName: "Control"),

        new EnemyStatePatch("Blade Spider",
            "Init",
            ShiftToBaseDelegate(magicNumber: -0.014f),
            fsmName: "Behaviour"),

        new EnemyStatePatch("Blade Spider Hang",
            "Init",
            ShiftToBaseDelegate(reverseY: true),
            fsmName: "Behaviour"),

        new EnemyStateActionPatch("Mite Heavy",
            "Set Bot",
            typeof(FloatAdd),
            ScaleValueDelegate(new(){
                OperatorType = ScaleValueSetting.TargetType.FloatAdd,
                ScaleWithObject = true,
                Target = new(){
                    Axis = ScaleValueSetting.ValueTarget.AxisDirections.y } }),
            fsmName: "Control"),
#endregion Many Places

#region Weavenests

         new EnemyStatePatch("Weaver Servitor",
            "Init",
            ShiftToBaseDelegate(magicNumber: -0.0055f),
            fsmName: "Control"),

         new EnemyStatePatch("Weaver Servitor Large",
            "Init",
            ShiftToBaseDelegate(magicNumber: -0.022f),
            fsmName: "Control"),
#endregion Weavenests

#region Moss Grotto & Mosshome

        new EnemyStatePatch("MossBone Crawler",
            "Init",
             ShiftToBaseDelegate(),
            fsmName: "Noise Reaction"),

        new EnemyStatePatch("Aspid Collector",
            "Init",
            ShiftToBaseDelegate(rotateAdjust: 90f, magicNumber: -0.004f, skipRaycast: true),
            fsmName: "Control"),

        new EnemyStateActionPatch("Pilgrim Moss Spitter",
            ["Attempt Larger Jump", "Escape Antic"],
            typeof(GetGroundPointClampedToEdge),
            delegate(FsmStateAction action, object[] param)
            {
                var minJumpDistance = ((GetGroundPointClampedToEdge)action).MinJumpDistance;

                minJumpDistance.Value = minJumpDistance.Value * ((GameObject)param[0]).transform.GetAbsScaleX();
            },
            fsmName: "Control"),

        new EnemyStateActionPatch("Mossbone Mother",
            "Swoop",
            typeof(FloatOperator),
            delegate(FsmStateAction action, object[] param)
            {
                if(((FloatOperator)action).float1.Name.Equals("Swoop X Speed"))
                    ScaleValueDelegate(new(){
                        OperatorType = ScaleValueSetting.TargetType.FloatOperator,
                        Target = new(){
                            Target = "float1",
                            Axis = ScaleValueSetting.ValueTarget.AxisDirections.x } })(action, param);
            },
            fsmName: "Control"),
#endregion Moss Grotto & Mosshome

#region Bone Bottom & Marrow
        new EnemyStatePatch("Bone Beast",
            "Init",
            delegate (FsmState state, object[] param)
            {
                Vector2 backupSize = new(9.1875f, 2.9063f);
                Vector2 backupOffset = new(-1.5625f, -2.2969f);

                ShiftVar shiftGroundY = new()
                {
                    gameObject = new()
                    {
                        GameObject = (GameObject)param[0]
                    },
                    variableName = "Ground Y",
                    magicNumber = -0.03f,
                    backupSize = backupSize,
                    backupOffset = backupOffset,
                    callOnce = true
                };

                ShiftVar submergeGroundY = new()
                {
                    gameObject = new()
                    {
                        GameObject = (GameObject)param[0]
                    },
                    variableName = "Submerge Y",
                    magicNumber = 0.16f,
                    backupSize = backupSize,
                    backupOffset =backupOffset,
                    callOnce = true
                };

                state.Actions = state.Actions.AddRangeToArray([shiftGroundY,submergeGroundY]);
            },
            fsmName: "Control"),

        new EnemyFSMPatch("Bone Beast Corpse",
            delegate(PlayMakerFSM fsm, object[] param)
            {
                GameObject enemy = (GameObject)param[0];
                ConstrainPosition constrain = enemy.GetComponent<ConstrainPosition>();

                constrain.yMin = ShiftPosToBase.ShiftN((float)constrain.yMin, enemy.transform.GetAbsScaleY(), -0.03f);
            },
            fsmName: "Death"),

        new EnemyStatePatch("Skull King",
            "Init",
            delegate (FsmState state, object[] param)
            {
                ShiftToBaseDelegate(magicNumber: -0.025f)(state, param);
            },
            fsmName: "Behaviour"),

#endregion Bone Bottom & Marrow

#region Wormways

        new EnemyStatePatch("Bone Worm", // Burrow height after drop
            "Position",
            delegate(FsmState state, object[] param)
            {
                ShiftVar shift = new()
                {
                    gameObject = new()
                    {
                        GameObject = (GameObject)param[0]
                    },
                    variableName = "Ground Y",
                    callOnce = true
                };

                state.Actions = state.Actions.AddItem(shift).ToArray();
            },
            fsmName: "Control"),

        new EnemyStatePatch("Crypt Worm", // unburrow point
            ["Static Ready", "Ambush Ready"],
            ShiftToBaseDelegate(magicNumber: -0.004f),
            fsmName: "Control"),

        new EnemyStatePatch("Roof Crab",
            "Init",
            ShiftToBaseDelegate(magicNumber: -0.039928f, reverseY: true),
            fsmName: "Control"),
#endregion Wormways

#region Deep Docks
        new EnemyStatePatch("Dock Flyer",
            "Init",
            ShiftToBaseDelegate(magicNumber: -0.015f, skipRaycast: true),
            fsmName: "Behaviour"),

        new EnemyStatePatchSet("Dock Worker", // Halt velocity and shift
            [new("Dock Worker",
                "Init",
                ShiftToBaseDelegate(),
                fsmName: "Behaviour"),
            new("Dock Worker",
                "Start Dig",
                delegate(FsmState action, object[] param)
                {
                    GameObject enemy = (GameObject)param[0];

                    if ((enemy.name.Equals("Dock Worker (1)") && enemy.scene.name.Equals("Bone_East_03")) || // Several workers spawn too close to coals
                        (enemy.name.Equals("Dock Worker (5)") && enemy.scene.name.Equals("Dock_02b")) ||
                        (enemy.name.Equals("Dock Worker") && enemy.scene.name.Equals("Dock_03")))
                        ShiftToBaseDelegate(rotateAdjust: 90f, skipRaycast: true, magicNumber: -0.01f)(action, param);
                    else
                        ShiftToBaseDelegate(rotateAdjust: 90f, skipRaycast: true, magicNumber: -0.02f)(action, param);
                },
                fsmName: "Behaviour"),
            new("Dock Worker", // Signis & Gronn fight intro
                "Idle",
                ShiftToBaseDelegate(),
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

                ShiftToBaseDelegate(magicNumber: magic)(action, param);
            },
            fsmName: "Control"),
#endregion Deep Docks

#region Hunters March & Far Fields

        new EnemyStateActionPatch("Bone Hunter Buzzer",
            "Roost Start",
            typeof(FloatAdd),
            ScaleValueDelegate(new(){
                OperatorType = ScaleValueSetting.TargetType.FloatAdd,
                ScaleWithObject = true,
                Target = new(){
                    Axis = ScaleValueSetting.ValueTarget.AxisDirections.x } }),
            fsmName: ""),

        new EnemyStatePatch("Bone Hunter Fly",
            "Init",
            ShiftToBaseDelegate(magicNumber: -0.015f, skipRaycast: true),
            fsmName: "Control"),
#endregion Hunters March & Far Fields

#region Greymoor
        new EnemyStateActionPatch("Farmer Scissors", // Fixes distance check
            "Do Step",
            typeof(RayCast2dV2),
            ScaleValueDelegate(new(){
                        OperatorType = ScaleValueSetting.TargetType.RayCast2dV2,
                        Target = new(){
                            Axis = ScaleValueSetting.ValueTarget.AxisDirections.y } }),
            fsmName: "Control"),

        new EnemyStateActionPatch("Farmer Scissors", // Cut up end check
            "Cut Through U",
            typeof(RayCast2dV2),
            delegate(FsmStateAction action, object[] param)
            {
                RayCast2dV2 rc2dv2 = (RayCast2dV2)action;
                GameObject enemy = (GameObject)param[0];
                float height = enemy.GetComponent<BoxCollider2D>()?.size.y ?? 2.5837f;

                float newDistance = (rc2dv2.distance.Value - height) + height * enemy.transform.GetAbsScaleY();
                rc2dv2.distance.Value = newDistance;
            },
            fsmName: "Control"),

        new EnemyStateActionPatch("Farmer Centipede", // Fixes distance check
            "Idle Chase",
            typeof(DistanceWalk),
            ScaleValueDelegate(new(){
                OperatorType = ScaleValueSetting.TargetType.DistanceWalk,
                ScaleWithObject = true,
                Target = new(){
                    Target = "distance",
                    Axis = ScaleValueSetting.ValueTarget.AxisDirections.x } }),
            fsmName: "Control"),

        new EnemyStatePatch("FlyAway Crow",
            "Init",
            delegate(FsmState state, object[] param)
            {
                GameObject crow = (GameObject)param[0];

                string name = CuteRandoCore.CullName((string)param[1]);

                Instance.RandomizeSize(false, crow.transform);
                AdjustChildren(crow, name);

                ShiftToBaseDelegate(backupSize: new(0.99f, 1.3624f), magicNumber: -0.01f)(state, param);
            },
            fsmName: "Control"),

        new EnemyFSMPatch(["Crowman Juror Crowd Idle", "Crowman Juror Crowd Rest"],
            delegate (PlayMakerFSM fsm, object[] param)
            {
                Instance.RandomizeSize(false, ((GameObject)param[0]).transform);
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
                    ? transform.GetScaleX()
                    : transform.GetScaleX() * -1;
                rfe.value2.Value = rfe.value2.Value > 0
                    ? transform.GetScaleX()
                    : transform.GetScaleX() * -1; ;
            },
            fsmName: "Control"),

        new EnemyStatePatch("Crowman Juror",
            "Init",
            ShiftToBaseDelegate(backupSize: new(1.2188f, 2.7656f)),
            fsmName: "Control"),

        new EnemyStateActionPatch(["Crow", "Crowman Juror Tiny"], // Fix Swoop
            "Swoop Down",
            typeof(FloatOperator),
            ScaleValueDelegate(new(){
                OperatorType = ScaleValueSetting.TargetType.FloatOperator,
                Target = new(){
                    Target = "float1",
                    Axis = ScaleValueSetting.ValueTarget.AxisDirections.x } }),
            fsmName: "Behaviour"),

        new EnemyStateActionPatchSet("Vampire Gnat",
            [new("",
                "Active",
                typeof(FloatOperator),
                delegate(FsmStateAction action, object[] param)
                {
                    FloatOperator fo = (FloatOperator)action;

                    if(fo.operation != FloatOperator.Operation.Subtract) return;

                    GameObject enemy = (GameObject)param[0];

                    if(enemy.transform.GetAbsScaleY() > 1) return;

                    fo.float2.Value += enemy.GetComponent<BoxCollider2D>().size.y * enemy.transform.GetAbsScaleY();
                },
                fsmName:"Y Control"),
            new("",
                "Charge",
                typeof(FloatMultiply),
                ScaleValueDelegate(new(){
                    OperatorType = ScaleValueSetting.TargetType.FloatMultiply,
                    Target = new(){
                        Axis = ScaleValueSetting.ValueTarget.AxisDirections.x}})),
            new("",
                "Slash Dive",
                typeof(FloatOperator),
                delegate(FsmStateAction action, object[] param)
                {
                    FloatOperator fo = (FloatOperator)action;

                    if (fo.float2.Value !=2 )return;

                    GameObject enemy = (GameObject)param[0];

                    if(enemy.transform.GetAbsScaleY() > 1) return;

                    fo.float2.Value += (1-enemy.transform.GetAbsScaleY()) * enemy.GetComponent<BoxCollider2D>().size.y;
                })
            ],
            fsmName: "Control"),

#endregion Greymoor

#region Wisp Thicket
        new EnemyStatePatch("Farmer Wisp",
            "State",
            ShiftToBaseDelegate(),
            fsmName: "Control"),

        new EnemyStateActionPatch("Farmer Wisp",
            "Tele Pos",
            typeof(Vector2AddXY),
            delegate(FsmStateAction action, object[] param)
            {
                ((Vector2AddXY)action).addY.Value *= ((GameObject)param[0]).transform.GetAbsScaleY();
            }),

#endregion Wisp Thicket

#region Bellways

        new EnemyStateActionPatch("Bell Goomba",
            ["Set To Ground","Set To Wall L", "Set To Wall R", "Set To Roof"],
            typeof(Translate),
            delegate(FsmStateAction action, object[] param)
            {
                GameObject gameObject = (GameObject)param[0];
                float halfSize = gameObject.GetComponent<BoxCollider2D>().size.y * gameObject.transform.GetAbsScaleY();
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

        new EnemyStatePatch("Spinner Boss",
            "Dormant",
            delegate(FsmState state, object[] param)
            {
                ShiftVar shiftGroundY = new()
                {
                    gameObject = new()
                    {
                        GameObject = (GameObject)param[0]
                    },
                    variableName = "Ground Y",
                    backupOffset = new(-0.0391f, -0.9375f),
                    backupSize = new(1.2031f, 4.1875f),
                    magicNumber = -0.04f,
                    callOnce = true
                };

                state.Actions = state.Actions.AddRangeToArray([shiftGroundY]);
            },
            fsmName: "Control"),

        new EnemyStateActionPatchSet("Spinner Boss",
            [new("",
                ["Close Pos Low", "Set Scream", "Set Strum Shot", "Set Chase Slam"],
                typeof(RandomFloat),
                delegate(FsmStateAction action, object[] param)
                {
                    RandomFloat rf = (RandomFloat)action;
                    float y = ShiftPosToBase.ShiftN(rf.min.Value, ((GameObject)param[0]).transform.GetAbsScaleY(), -0.04f);
                    rf.min.Value = y;
                    rf.max.Value = y;
                }),
            new("",
                ["DashSlash Aim L", "DashSlash Aim R"],
                typeof(SetPosition),
                delegate(FsmStateAction action, object[] param)
                {
                    SetPosition setPosition = (SetPosition)action;
                    float y = ShiftPosToBase.ShiftN(setPosition.y.Value, ((GameObject)param[0]).transform.GetAbsScaleY(), -0.04f);

                    setPosition.y = y;
                }),
            new("",
                "Set Scuttle",
                typeof(SetPosition),
                delegate(FsmStateAction action, object[] param)
                {
                    SetPosition setPosition = (SetPosition)action;
                    float y = ShiftPosToBase.ShiftN(setPosition.y.Value, ((GameObject)param[0]).transform.GetAbsScaleY(), -0.04f);

                    setPosition.y = y;
                })
            ],
            fsmName: "Control"),

#endregion Bellways

#region Shellwood

        new EnemyStatePatch("Shellwood Goomba Flyer",
            "Init",
            ShiftToBaseDelegate(rotateAdjust: 180f),
            fsmName: "Control"),

        new EnemyStatePatch("Shellwood Goomba",
            "Init",
            delegate(FsmState action, object[] param)
            {
                GameObject enemy = (GameObject)param[0];
                if (enemy.scene.name.Equals("Shellwood_01") && enemy.name.Equals("Shellwood Goomba (2)"))
                    ShiftToBaseDelegate(rotateAdjust: 180f)(action, param);
                else
                    ShiftToBaseDelegate()(action, param);
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
                        enemy.transform.position.y + (0.2f * (enemy.transform.GetAbsScaleY())));

                ShiftToBaseDelegate(rotateAdjust: 270f, skipRaycast: true)(action, param);
            },
            fsmName: "Control"),

        new EnemyStatePatch("Stick Insect Charger", // Shellwood_20 stick insect doesn't start with a collider
            "Init",
            ShiftToBaseDelegate(backupSize: new(1.8438f, 1.9375f)),
            fsmName: "Behaviour Base"),

        new EnemyStatePatch("Stick Insect Flyer",
            "Init",
            ShiftToBaseDelegate(rotateAdjust: 270f, magicNumber: -0.005f),
            fsmName: "Control"),

        new EnemyStatePatch("Stick Insect",
            "Init",
            ShiftToBaseDelegate(),
            fsmName: "Behaviour Base"),

        new EnemyStatePatch("Splinter Queen",
            "Init",
            delegate(FsmState state, object[] param)
            {
                ShiftToBaseDelegate(magicNumber: 0.01f)(state, param);
                ShiftVar shiftGroundY = new()
                    {
                        gameObject = new()
                        {
                            GameObject = (GameObject)param[0]
                        },
                        variableName = "Ground Y",
                        magicNumber = 0.0055f,
                    callOnce = true
                    };

                state.Actions = state.Actions.AddToArray(shiftGroundY);
            },
            fsmName: "Control"),

        new EnemyStatePatch("Seth",
            "Init",
            delegate(FsmState state, object[] param)
            {
                GameObject seth = (GameObject)param[0];

                ShiftVar groundY = new()
                    {
                        gameObject = new()
                        {
                            GameObject = seth
                        },
                        variableName = "Ground Y",
                        magicNumber = -0.02331f,
                        callOnce = true
                    };

                ShiftToBaseDelegate(magicNumber: -0.008024f, skipRaycast: true)(state, param);
            },
            fsmName: "Control"),

        new EnemyStateActionPatch("Seth",
            "Roar Antic",
            typeof(SetPosition),
            delegate(FsmStateAction action, object[] param)
            {
                SetPosition setPos = (SetPosition)action;
                GameObject seth = (GameObject)param[0];

                setPos.y = ShiftPosToBase.ShiftN(setPos.y.Value, seth.transform.GetAbsScaleY(), -0.008024f);
            },
            fsmName: "Control"),

        new EnemyFSMPatch("Seth",
            delegate(PlayMakerFSM fsm, object[] param)
            {
                GameObject seth = (GameObject)param[0];
                ConstrainPosition constrain = seth.GetComponent<ConstrainPosition>();

                constrain.yMin = ShiftPosToBase.ShiftN((float)constrain.yMin, seth.transform.GetAbsScaleY(), -0.0115f);

                BoxCollider2D aboveRange = seth.transform.Find("Above Range").GetComponent<BoxCollider2D>();
                BoxCollider2D sethCollider = seth.GetComponent<BoxCollider2D>();
                float sethHeight = sethCollider.size.y * seth.transform.GetAbsScaleY();
                if(seth.transform.GetAbsScaleY() < 1f)
                {
                    aboveRange.size = aboveRange.size with {y = aboveRange.size.y + (sethCollider.size.y - sethHeight) / 4 };
                    aboveRange.offset = aboveRange.offset with {y = aboveRange.offset.y - (sethCollider.size.y - sethHeight) / 4 };
                }
            },
            fsmName: "Control"),

#endregion Shellwood

#region Blasted Steps
        new EnemyStatePatch("Coral Judge", // Has a delay at the start of Init that causes issues, so we have to target something other than Init
            "Check Scale",
            ShiftToBaseDelegate(),
            fsmName: "Control"),

        new EnemyStatePatch("Last Judge",
            "Init",
            delegate(FsmState state, object[] param)
            {
                Vector2 backupSize = new(2.3125f, 6.625f);
                Vector2 backupOffset = new(-0.0313f, -0.9219f);
                GameObject enemy = (GameObject)param[0];

                ShiftVar shiftGroundY = new()
                {
                    gameObject = new()
                    {
                        GameObject = (GameObject)param[0]
                    },
                    variableName = "Ground Y",
                    magicNumber = -0.042f,
                    backupSize = backupSize,
                    backupOffset = backupOffset,
                    callOnce = true
                };

                state.Actions = state.Actions.AddToArray(shiftGroundY);

                var constrain = enemy.GetComponent<ConstrainPosition>();
                constrain.yMin = ShiftPosToBase.ShiftN((float)constrain.yMin, enemy.transform.GetAbsScaleY(), -0.042f);
            },
            fsmName: "Control"),

        new EnemyStateActionPatchSet("Corpse Last Judge",
            [new("",
                "Fall",
                typeof(CheckYPosition),
                delegate(FsmStateAction action, object[] param)
                {
                    GameObject enemy = (GameObject)param[0];
                    CheckYPosition check = (CheckYPosition)action;

                    check.compareTo.Value = ShiftPosToBase.ShiftN(check.compareTo.Value, enemy.transform.GetAbsScaleY(), -0.042f);
                }),
            new("",
                "Land",
                typeof(SetPosition),
                delegate(FsmStateAction action, object[] param)
                {
                    GameObject enemy = (GameObject)param[0];
                    SetPosition setPos = (SetPosition)action;

                    setPos.y.Value = ShiftPosToBase.ShiftN(setPos.y.Value, enemy.transform.GetAbsScaleY(), -0.042f);
                })
            ],
            fsmName: "Control"),

#endregion Blasted Steps

#region Conch Driller Bosses

        new EnemyStatePatch(["Driller A", "Driller B", "Coral Conch Driller Giant Solo"],
                "Init",
                delegate(FsmState state, object[] param)
                {
                    GameObject enemy = (GameObject)param[0];
                    bool coral_11;

                    if(enemy.scene.name.Equals("Coral_11"))
                        coral_11 = true;
                    else if(enemy.name.Equals("Coral Conch Driller Giant Solo") && enemy.scene.name.Equals("Coral_27"))
                        coral_11 = false;
                    else if(enemy.name.Equals("Driller A") && enemy.scene.name.Equals("Coral_27"))
                    {
                        ShiftVar chargeY = new()
                        {
                            gameObject = new()
                            {
                                GameObject = enemy
                            },
                            variableName = "Charge Y",
                            magicNumber = 0.016f,
                            backupSize = new(1.4368f, 1.1568f),
                            backupOffset = new(0.0847f, 0.0871f),
                            callOnce = true
                        };

                        state.Actions = state.Actions.AddToArray(chargeY);
                        return;
                    }
                    else
                        return;

                    Vector2 backupSize = new(2.3363f, 1.6283f);
                    Vector2 backupOffset = new(-0.0981f, 0.1592f);

                    ShiftVar drillFloorY = new()
                    {
                        gameObject = new()
                        {
                            GameObject = enemy
                        },
                        variableName = "Drill Floor Y",
                        magicNumber = coral_11 ? -0.0413f : -0.0416f,
                        backupSize = backupSize,
                        backupOffset = backupOffset,
                        callOnce = true
                    };

                    ShiftVar drillRoofY = new()
                    {
                        gameObject = new()
                        {
                            GameObject = enemy
                        },
                        variableName = "Drill Roof Y",
                        magicNumber = coral_11 ? 0.0453f: 0.058f,
                        backupSize = backupSize,
                        backupOffset = backupOffset,
                        callOnce = true
                    };

                    ShiftVar drillWallL = new()
                    {
                        gameObject = new()
                        {
                            GameObject = enemy
                        },
                        variableName = "Drill Wall L",
                        magicNumber = coral_11 ? -0.0413f : -0.0329f,
                        backupSize = backupSize,
                        backupOffset = backupOffset,
                        callOnce = true
                    };

                    ShiftVar drillWallR = new()
                    {
                        gameObject = new()
                        {
                            GameObject = enemy
                        },
                        variableName = "Drill Wall R",
                        magicNumber = coral_11 ? 0.042f : 0.0365f,
                        backupSize = backupSize,
                        backupOffset = backupOffset,
                        callOnce = true
                    };

                    ShiftToBaseDelegate(magicNumber: -0.042f, skipRaycast: true)(state, param);

                    state.Actions = state.Actions.AddRangeToArray([drillFloorY,drillRoofY,drillWallL,drillWallR]);
                },
            fsmName: "Control"),

        new EnemyStateActionPatchSet(["Driller A", "Driller B", "Coral Conch Driller Giant Solo", "Driller C"],
            [new("", // Confly 1 & 2
                ["Pos U", "Pos D", "Pos L", "Pos R"],
                typeof(Translate),
                delegate(FsmStateAction action, object[] param)
                {
                    Translate translate = (Translate)action;
                    GameObject enemy = (GameObject)param[0];

                    if(!enemy.scene.name.Equals("Coral_11") && !(enemy.name.Equals("Coral Conch Driller Giant Solo") && enemy.scene.name.Equals("Coral_27"))) return;

                    translate.x.Value *= enemy.transform.GetAbsScaleX();
                    translate.y.Value *= enemy.transform.GetAbsScaleY();
                }),
            new("", // Confly 1 & 2
                ["Emerge 1 D", "Emerge 1 L", "Emerge 1 R", "Emerge 1 U"],
                typeof(AnimatePositionBy),
                delegate(FsmStateAction action, object[] param)
                {
                    GameObject enemy = (GameObject)param[0];

                    if(enemy.transform.GetScaleY() <= 1)
                        return;
                    AnimatePositionBy animatePos = (AnimatePositionBy)action;
                    animatePos.shiftBy.Value = animatePos.shiftBy.Value.MultiplyElements(enemy.transform.GetAbsScale());
                }),
            new("", // Vanilla bugfix for wrong velocity direction, Conchfly 1
                "Emerge 2 D",
                typeof(SetVelocity2d),
                delegate(FsmStateAction action, object[] param)
                {
                    SetVelocity2d setVeocity = (SetVelocity2d)action;
                    GameObject enemy = (GameObject)param[0];

                    if(!enemy.scene.name.Equals("Coral_11")) return;

                    if(setVeocity.y.Value > 0)
                        setVeocity.y.Value *= -1;
                }),
            new("",
                "Intro R 2", // Conchfly 1
                typeof(AnimatePositionTo),
                delegate(FsmStateAction action, object[] param)
                {
                    GameObject enemy = (GameObject)param[0];

                    if(!enemy.scene.name.Equals("Coral_11")) return;

                    if(enemy.transform.GetAbsScaleY() <= 1)
                        return;
                    AnimatePositionTo animatePos = (AnimatePositionTo)action;
                    var startY = ShiftPosToBase.ShiftN(animatePos.toValue.Value.y, enemy.transform.GetAbsScaleY(), -0.042f);

                    animatePos.toValue.Value = animatePos.toValue.Value with { y = startY };
                }),
            new("",
                "Intro G 2", // Conchfly 1
                typeof(AnimatePositionTo),
                delegate(FsmStateAction action, object[] param)
                {
                    GameObject enemy = (GameObject)param[0];

                    if(!enemy.scene.name.Equals("Coral_11")) return;

                    if(enemy.transform.GetAbsScaleY() <= 1)
                        return;
                    AnimatePositionTo animatePos = (AnimatePositionTo)action;
                    var startY = ShiftPosToBase.ShiftN(animatePos.toValue.Value.y, enemy.transform.GetAbsScaleY(), 0.042f);

                    animatePos.toValue.Value = animatePos.toValue.Value with { y = startY };
                }),
            new("",
                ["Intro L", "Intro R"], // Conchfly 2
                typeof(AnimatePositionBy),
                delegate(FsmStateAction action, object[] param)
                {
                    GameObject enemy = (GameObject)param[0];
                    AnimatePositionBy animatePos = (AnimatePositionBy)action;

                    var startY = ShiftPosToBase.ShiftN(animatePos.shiftBy.Value.y, enemy.transform.GetAbsScaleY(), -0.042f);

                    animatePos.shiftBy.Value = animatePos.shiftBy.Value with { y = startY };
                }),
            new("", // Conchfly 2 Adds
                "Set Underground",
                typeof(SetPosition2d),
                delegate(FsmStateAction action, object[] param)
                {
                    GameObject enemy = (GameObject)param[0];
                    SetPosition2d setPos = (SetPosition2d)action;

                    if(enemy.transform.GetAbsScaleY() <= 1) return;

                    setPos.y = ShiftPosToBase.ShiftN(setPos.y.Value, enemy.transform.GetAbsScaleY(), 0.042f);
                }),
            new("",
                "M Charge Pos",
                typeof(SetPosition),
                delegate(FsmStateAction action, object[] param)
                {
                    GameObject enemy = (GameObject)param[0];
                    SetPosition setPos = (SetPosition)action;

                    if(enemy.transform.GetAbsScaleY() <= 1) return;

                    setPos.y = ShiftPosToBase.ShiftN(setPos.y.Value, enemy.transform.GetAbsScaleY(), -0.042f);
                })
            ],
            fsmName: "Control"),

        new EnemyStatePatch("Coral Conch Driller Roar",
            "Init",
            delegate(FsmState state, object[] param)
            {
                GameObject enemy = (GameObject)param[0];
                Vector2 backupSize = new(1.4368f, 1.1568f);
                Vector2 backupOffset = new(0.0847f, 0.0871f);

                ShiftVar topY = new()
                {
                    gameObject = new()
                    {
                        GameObject = enemy
                    },
                    variableName = "Top Y",
                    magicNumber =  -0.0216f,
                    backupSize = backupSize,
                    backupOffset = backupOffset,
                    callOnce = true
                };

                ShiftVar botY = new()
                {
                    gameObject = new()
                    {
                        GameObject = enemy
                    },
                    variableName = "Bot Y",
                    magicNumber =  0.0216f,
                    backupSize = backupSize,
                    backupOffset = backupOffset,
                    callOnce = true
                };

                state.Actions = state.Actions.AddRangeToArray([topY, botY]);
            },
            fsmName: "Control"),

        new EnemyStateActionPatch("Coral Conch Driller Roar",
            ["Shoot U", "Shoot D"],
            typeof(RayCast2dV2),
            delegate (FsmStateAction action, object[] param)
                {
                    RayCast2dV2 act = (RayCast2dV2)action;

                    act.distance.Value *= 4;
                },
            fsmName: "Control"),

        new EnemyFSMPatch("Coral Conch Driller BG Intro",
            delegate (PlayMakerFSM fsm, object[] param)
            {
                Instance.RandomizeSize(false, ((GameObject)param[0]).transform);
            },
            fsmName: "Control"),

#endregion Conch Driller Bosses

#region Sands of Karak
        new EnemyStatePatch("Coral Conch Driller",
            "Init",
            ShiftToBaseDelegate(skipRaycast: true),
            fsmName: "Control"),

        new EnemyStateActionPatchSet("Coral Conch Driller",
            [new("",
                ["Aim D", "Antic1 D"],
                typeof(ChaseObjectVertical),
                delegate(FsmStateAction action, object[] param)
                {
                    ((ChaseObjectVertical)action).offset.Value *= ((GameObject)param[0]).transform.GetAbsScaleY();
                }),
            new("",
                ["Aim F", "Antic1 L", "Antic2 R"],
                typeof(DistanceFly),
                delegate(FsmStateAction action, object[] param)
                {
                    ((DistanceFly)action).distance.Value *= ((GameObject)param[0]).transform.GetAbsScaleY();
                }),
            new("",
                ["Drill In D", "Drill In L", "Drill In R"],
                typeof(AnimatePositionBy),
                delegate(FsmStateAction action, object[] param)
                {
                    AnimatePositionBy animatePos = (AnimatePositionBy)action;
                    animatePos.shiftBy.Value = animatePos.shiftBy.Value.MultiplyElements(((GameObject)param[0]).transform.GetAbsScale());
                }),
            new("",
                ["Emerge 1 D", "Emerge 1 L", "Emerge 1 R"],
                typeof(AnimatePositionBy),
                delegate(FsmStateAction action, object[] param)
                {
                    GameObject enemy = (GameObject)param[0];

                    if(enemy.transform.GetScaleY() < 1)
                        return;
                    AnimatePositionBy animatePos = (AnimatePositionBy)action;
                    animatePos.shiftBy.Value = animatePos.shiftBy.Value.MultiplyElements(enemy.transform.GetAbsScale());
                }),
            new("",
                ["Under D", "Under L", "Under R"],
                typeof(Translate),
                delegate(FsmStateAction action, object[] param)
                {
                    Translate translate = (Translate)action;
                    GameObject enemy = (GameObject)param[0];

                    translate.x.Value *= enemy.transform.GetAbsScaleX();
                    translate.y.Value *= enemy.transform.GetAbsScaleY();
                }),
            new("",
                ["Under Aim D", "Under Aim L", "Under Aim R", "Shoot L", "Shoot R", "Shoot D"],
                typeof(RayCast2dV2),
                ScaleValueDelegate(new(){
                        OperatorType = ScaleValueSetting.TargetType.RayCast2dV2,
                        OnlyShort = true,
                        Target = new(){
                            Axis = ScaleValueSetting.ValueTarget.AxisDirections.y } }))
            ],
            fsmName: "Control"),

#endregion Sands of Karak

#region Sinner's Road & Mist
        new EnemyStatePatch("Dustroach FG",
            "Init",
            delegate(FsmState state, object[] param)
            {
                GameObject dustroach = (GameObject)param[0];

                string name = CuteRandoCore.CullName((string)param[1]);

                Instance.RandomizeSize(false, dustroach.transform);
                AdjustChildren(dustroach, name);

                ShiftToBaseDelegate(backupSize: new(2.6999f, 1.2145f), magicNumber: -0.01f)(state, param);
            },
            fsmName: "Control"),

        new EnemyStateActionPatch("Dustroach", // Fixes scale issues
            "ScrabbleJump Air",
            typeof(RayCast2dV2),
            ScaleValueDelegate(new(){
                        OperatorType = ScaleValueSetting.TargetType.RayCast2dV2,
                        Target = new(){
                            Axis = ScaleValueSetting.ValueTarget.AxisDirections.y } }),
            fsmName: "Control"),

        new EnemyStateActionPatch("Roachfeeder Tall",
            "Watch",
            typeof(DistanceFly),
            delegate(FsmStateAction action, object[] param)
            {
                ((DistanceFly)action).distance.Value *= ((GameObject) param[0]).transform.GetAbsScaleX();
            }),

        new EnemyFSMPatch("Phantom",
            delegate(PlayMakerFSM fsm, object[] param)
            {
                GameObject enemy = (GameObject)param[0];
                ConstrainPosition constrain = enemy.GetComponent<ConstrainPosition>();

                constrain.yMin = 103f;

                // Dragoon Damager Height
                var dragoonDamager = enemy.transform.Find("Dragoon Down/Dragoon Down Damager");
                var dragoonDamagerCollider = dragoonDamager?.GetComponent<BoxCollider2D>();

                if(dragoonDamagerCollider != null)
                {
                    var yScale = enemy.transform.GetAbsScaleY();

                    dragoonDamager.SetScaleY(1/yScale);
                    AdjustChildPosition(dragoonDamager.GetPositionY(), dragoonDamagerCollider.offset.y, yScale);
                }

                // BG Phantom
                Transform organPhantom = enemy.transform.parent.Find("Organ Phantom");

                organPhantom.localScale = enemy.transform.GetAbsScale();
                organPhantom.position = organPhantom.position with { y = ShiftPosToBase.ShiftN(organPhantom.position.y, Math.Abs(organPhantom.GetScaleY()), -0.0082f) };
            },
            fsmName: "Control"),

        new EnemyStateActionPatchSet("Phantom",
            [new("",
                "G Throw",
                typeof(FloatCompare),
                delegate(FsmStateAction action, object[] param)
                {
                    FloatCompare fc = (FloatCompare)action;

                    if(fc.float2.Value == 72.5f)
                        fc.float2.Value = 72.0f;
                    else if(fc.float2.Value == 96.5f)
                        fc.float2.Value = 97.0f;
                }),
            new("",
                "G Pull Wait",
                typeof(FloatCompare),
                delegate(FsmStateAction action, object[] param)
                {
                    GameObject enemy = (GameObject)param[0];
                    FloatCompare fc = (FloatCompare)action;

                    if(fc.float2.Value == 73.5f)
                        fc.float2.Value = 73.5f - 0.011f * (1-enemy.transform.GetAbsScaleX()) * 100;
                    else if(fc.float2.Value == 95.5f)
                        fc.float2.Value = 95.5f + 0.011f * (1-enemy.transform.GetAbsScaleX()) * 100;
                }),
            new("",
                "G Thunk",
                typeof(FloatClamp),
                delegate(FsmStateAction action, object[] param)
                {
                    GameObject enemy = (GameObject)param[0];
                    FloatClamp fc = (FloatClamp)action;

                    fc.minValue.Value = 73.5f - 0.011f * (1-enemy.transform.GetAbsScaleX()) * 100;
                    fc.maxValue.Value = 95.5f + 0.011f * (1-enemy.transform.GetAbsScaleX()) * 100;
                })
            ],
            fsmName: "Control"),

#endregion Sinner's Road & Mist

#region Bilewater & Putrified Ducts
        new EnemyStatePatch(["Swamp Mosquito", "Swamp Mosquito Skinny"],
            "Wall Cling",
            ShiftToBaseDelegate(rotateAdjust: 270f),
            fsmName: "Control"),

        new EnemyStateActionPatch("Swamp Muckman",
            ["Stick To Floor", "Stick to Wall R", "Stick to Wall L"],
            typeof(Translate),
            delegate(FsmStateAction action, object[] param)
            {
                GameObject gameObject = (GameObject)param[0];
                float halfSize = gameObject.GetComponent<BoxCollider2D>().size.y * Math.Abs(gameObject.transform.GetScaleY());
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

                teleMin.SetLocalPositionX(teleMin.localPosition.x / gameObject.transform.GetScaleX());
                teleMax.SetLocalPositionX(teleMax.localPosition.x / gameObject.transform.GetScaleX());
                boundsMin.SetLocalPositionX(boundsMin.localPosition.x / gameObject.transform.GetScaleX());
                boundsMax.SetLocalPositionX(boundsMax.localPosition.x / gameObject.transform.GetScaleX());

                fsm.FsmVariables.FindFsmFloat("Tele Min X").Value = teleMin.position.x;
                fsm.FsmVariables.FindFsmFloat("Tele Max X").Value = teleMax.position.x;
                fsm.FsmVariables.FindFsmFloat("X Pos Min").Value = boundsMin.position.x;
                fsm.FsmVariables.FindFsmFloat("X Pos Max").Value = boundsMax.position.x;
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
            ShiftToBaseDelegate(rotateAdjust: 33f, magicNumber: -0.022f),
            fsmName: "Bouncer Control"),

        #endregion Slab

#region Citadel
        new EnemyStatePatch("Song Reed Grand",
            "Init",
            ShiftToBaseDelegate(backupSize: new(1.3594f, 5.8281f)),
            fsmName: "Control"),

        new EnemyStateActionPatchSet("Song Reed Grand",
            [new("",
                ["Set Cast", "Set Slash", "Set Jump Attack"],
                typeof(SetFloatValue),
                delegate(FsmStateAction action, object[] param)
                {
                    var sfv = (SetFloatValue)action;

                    if(sfv.floatVariable.Name.Equals("Max Distance"))
                        sfv.floatValue.Value *= Math.Abs(((GameObject)param[0]).transform.GetScaleX());
                }),
            new("",
                "Crawl F",
                typeof(FloatOperator),
                ScaleValueDelegate(new(){
                    OperatorType = ScaleValueSetting.TargetType.FloatOperator,
                    ScaleWithObject = true,
                    Target = new(){
                        Target = "float2",
                        Axis = ScaleValueSetting.ValueTarget.AxisDirections.x } })),
            new("",
                "Hop",
                typeof(FloatTestToBool),
                ScaleValueDelegate(new(){
                    OperatorType = ScaleValueSetting.TargetType.FloatTestToBool,
                    ScaleWithObject = true,
                    Target = new(){
                        Target = "float2",
                        Axis = ScaleValueSetting.ValueTarget.AxisDirections.x } })),
            new("",
                "Stomp Land",
                typeof(Translate),
                ScaleValueDelegate(new(){
                    OperatorType = ScaleValueSetting.TargetType.Translate,
                    ScaleWithObject = true,
                    Target = new(){
                        Target = "y",
                        Axis = ScaleValueSetting.ValueTarget.AxisDirections.y } }))
            ],
            fsmName: "Control"),

        new EnemyFSMPatch(["Hand L", "Hand R"],
            delegate(PlayMakerFSM fsm, object[] param)
            {
                GameObject hand = (GameObject)param[0];

                Transform[] fingers = [];
                for(int i = 0; i < hand.transform.childCount; i++)
                {
                    var child = hand.transform.GetChild(i);
                    if(child.name.StartsWith("Finger Blade"))
                        fingers = fingers.AddToArray(child);
                }

                foreach(var finger in fingers)
                {
                    var fingerFsm = finger.GetComponents<PlayMakerFSM>().FirstOrDefault(fsm => fsm.FsmName.Equals("Control"));
                    var swipeL = fingerFsm.FsmStates.FirstOrDefault(state => state.Name.Equals("Set Swipe L"));
                    var swipeR = fingerFsm.FsmStates.FirstOrDefault(state => state.Name.Equals("Set Swipe R"));

                    foreach(var action in swipeL.Actions)
                    {
                        if(action is SetFloatValue setFloat && setFloat.floatVariable.Name.Equals("Attack Y Scale"))
                        {
                            setFloat.floatValue.Value *= finger.transform.GetAbsLossyScaleY();
                        }
                    }

                    foreach(var action in swipeR.Actions)
                    {
                        if(action is SetFloatValue setFloat && setFloat.floatVariable.Name.Equals("Attack Y Scale"))
                        {
                            setFloat.floatValue.Value *= finger.transform.GetAbsLossyScaleY();
                        }
                    }
                }
            }),

        new EnemyFSMPatchSet("Silk Boss",
            [new("",
                delegate(PlayMakerFSM fsm, object[] param)
                {
                    FsmFloat dashY = new("Dash Y")
                    {
                        Value = 0f
                    };

                    fsm.FsmVariables.FloatVariables = fsm.FsmVariables.FloatVariables.AddToArray(dashY);
                    fsm.FsmVariables.AddVariableLookup(dashY);

                    GameObject gms = (GameObject)param[0];

                    gms.transform.parent.Find("Silk_Hair").transform.localScale = new Vector3(gms.transform.GetAbsScaleX(), gms.transform.GetAbsScaleY(), 1f);
                },
                fsmName: "Attack Control"),
            new("",
                delegate(PlayMakerFSM fsm, object[] param)
                {
                    GameObject gms = (GameObject)param[0];

                    var bellStunY = fsm.FsmVariables.FindFsmFloat("Bell Stun Y");
                    bellStunY.Value = ShiftPosToBase.ShiftN(bellStunY.Value, gms.transform.GetAbsScaleY(), -0.05583f);
                },
                fsmName: "Control")
            ]),

        new EnemyStatePatchSet("Silk Boss",
            [new("",
                "Dash Attack Antic",
                delegate(FsmState state, object[] param)
                {
                    var dashvar = state.Fsm.Variables.FindFsmFloat("Dash Y");
                    ShiftGMS shift = new(){
                        GMS = (GameObject)param[0],
                        YPosition = dashvar
                    };

                    FsmStateAction[] bassAckwards = [shift];
                    state.Actions = bassAckwards.AddRangeToArray(state.Actions);
                }),
            new("",
                "Dash Attack",
                delegate(FsmState state, object[] param)
                {
                    ShiftGMS shift = new(){
                        GMS = (GameObject)param[0],
                        YPosition = state.Fsm.Variables.FindFsmFloat("Dash Y"),
                        Antic = false
                    };

                    FsmStateAction[] bassAckwards = [shift];
                    state.Actions = bassAckwards.AddRangeToArray(state.Actions);
                })
            ],
            fsmName: "Attack Control"),

        new EnemyStateActionPatchSet("Silk Boss",
            [new("",
                "Dash Attack Antic",
                typeof(AnimateYPositionTo),
                delegate(FsmStateAction action, object[] param)
                {
                    ((AnimateYPositionTo)action).ToValue = action.Fsm.Variables.FindFsmFloat("Dash Y");
                },
                fsmName: "Attack Control"),
            new("",
                "Dash Attack",
                typeof(AnimateYPositionTo),
                delegate(FsmStateAction action, object[] param)
                {
                    ((AnimateYPositionTo)action).ToValue = action.Fsm.Variables.FindFsmFloat("Dash Y");
                },
                fsmName: "Attack Control"),
            new("",
                "Stagger Fall",
                typeof(AccelerateToY),
                delegate(FsmStateAction action, object[] param)
                {
                    var accelerate = (AccelerateToY)action;
                    var gms = (GameObject)param[0];

                    if(gms.transform.GetAbsScaleY() <= 1) return;

                    accelerate.targetSpeed.Value *= gms.transform.GetAbsScaleY();
                    accelerate.accelerationFactor.Value *= gms.transform.GetAbsScaleY();
                },
                fsmName: "Phase Control")
            ]),

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
                        newActionArray[j++] = new ShiftVar()
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
            ShiftToBaseDelegate(magicNumber: -0.02f),
            fsmName: "Control"),

        new EnemyStateActionPatch("Song Automaton Goomba",
            "Attack Check",
            typeof(RayCast2dV2),
            ScaleValueDelegate(new(){
                        OperatorType = ScaleValueSetting.TargetType.RayCast2dV2,
                        Target = new(){
                            Axis = ScaleValueSetting.ValueTarget.AxisDirections.y } }),
            fsmName: "Control"),

        new EnemyStateActionPatch(["Song Automaton 01", "Song Automaton 02", "Song Automaton Goomba"],
            ["Do Step S", "Do Step F", "Stepped"],
            typeof(RayCast2dV2),
            ScaleValueDelegate(new(){
                        OperatorType = ScaleValueSetting.TargetType.RayCast2dV2,
                        Target = new(){
                            Axis = ScaleValueSetting.ValueTarget.AxisDirections.y } }),
            fsmName: "Walk"),

        new EnemyStateActionPatch("Song Automaton Shield",
            ["Do Step S", "Do Step F", "Stepped"],
            typeof(RayCast2dV2),
            ScaleValueDelegate(new(){
                        OperatorType = ScaleValueSetting.TargetType.RayCast2dV2,
                        Target = new(){
                            Axis = ScaleValueSetting.ValueTarget.AxisDirections.y } }),
            fsmName: "Automaton Walk"),

        new EnemyStatePatchSet(["Dancer A", "Dancer B"],
            [new("",
                ["Dash v2",
                "Drop Dash",
                "Jump v2",
                "Emerge",
                "Drop To Start"],
                delegate(FsmState state, object[] param)
                {
                    Vector2 backupOffset = new(0.0078f, -0.7578f);
                    Vector2 backupSize = new(1.0156f, 3.8281f);

                    ShiftVar shift = new()
                    {
                        gameObject = new()
                        {
                            GameObject = (GameObject)param[0]
                        },
                        variableName = "Tween Pos",
                        magicNumber = -0.03189f,
                        backupOffset = backupOffset,
                        backupSize = backupSize,
                        shiftValues = [false, true, false]
                    };

                    FsmStateAction[] newArray = new FsmStateAction[state.Actions.Length+1];
                    int j = 0;

                    for (int i = 0; i < state.Actions.Length; i++)
                    {
                        if(state.Actions[i].GetType() == typeof(AnimatePositionTo))
                            newArray[j++] = shift;
                        newArray[j++] = state.Actions[i];
                    }

                    state.Actions = newArray;
                }),
            new("",
                ["Set Eye Beam",
                "Stomp Slash 1",
                "Bow"],
                delegate(FsmState state, object[] param)
                {
                    GameObject enemy = (GameObject)param[0];
                    HealthManager healthManager = enemy.GetComponent<HealthManager>();
                    Vector3 originalScale = Vector3.one;
                    Vector2 backupSize = new(1.0156f, 3.8281f);
                    if (healthManager != null && !Instance.currentEnemyHealthManagers.TryGetValue(healthManager, out originalScale))
                        originalScale = Vector3.one;

                    FsmStateAction shift = new ShiftPosToBase
                    {
                        gameObject = new()
                        {
                            GameObject = enemy
                        },
                        backupSize = backupSize,
                        magicNumber = -0.03189f,
                        originalScale = originalScale,
                        skipRaycast = true,
                        overrideAdjusted = true
                    };

                    FsmStateAction[] newArray = new FsmStateAction[state.Actions.Length+1];
                    int j = 0;

                    for (int i = 0; i < state.Actions.Length; i++)
                    {
                        if( i > 1 && state.Actions[i-1].GetType() == typeof(SetPositionToObject))
                            newArray[j++] = shift;
                        newArray[j++] = state.Actions[i];
                    }

                    state.Actions = newArray;
                })
            ],
            fsmName: "Control"),

        new EnemyStateActionPatchSet(["Dancer A", "Dancer B"],
            [new("",
                "Stun Land",
                typeof(SetPosition),
                delegate(FsmStateAction action, object[] param)
                {
                    SetPosition setPosition = (SetPosition)action;
                    float y = ShiftPosToBase.ShiftN(setPosition.y.Value, Math.Abs(((GameObject)param[0]).transform.GetScaleY()), -0.03189f);

                    setPosition.y = y;
                }),
            new("",
                "Stun Fall",
                typeof(CheckYPosition),
                delegate(FsmStateAction action, object[] param)
                {
                    CheckYPosition checkPos = (CheckYPosition)action;
                    float y = ShiftPosToBase.ShiftN(checkPos.compareTo.Value, Math.Abs(((GameObject)param[0]).transform.GetScaleY()), -0.03189f);

                    checkPos.compareTo = y;
                })
            ],
            fsmName: "Control"),

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
                    ShiftToBaseDelegate(magicNumber: -0.02f, skipRaycast: true)(state, param);
                else
                    ShiftToBaseDelegate(rotateAdjust: 270f)(state, param);
            },
            fsmName: "Control"),

         new EnemyStatePatch("Song Knight",
            "Init",
            ShiftToBaseDelegate(),
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
                    ShiftToBaseDelegate(magicNumber: -0.015f)(state, param);
                else if(scene.Equals("Library_14"))
                    ShiftToBaseDelegate(rotateAdjust: 180f, magicNumber: -0.02f)(state, param);
                else
                    ShiftToBaseDelegate(rotateAdjust: 180f)(state, param);
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

                ShiftToBaseDelegate(backupSize: new(0.7904f, 2.747f), magicNumber: -0.01f)(action, param);
            },
            fsmName: "Control"),

        new EnemyStatePatchSet("Song Scholar Acolyte",
            [new("",
                [   "Voluntary Sing",
                    "Sleep",
                    "Walk Start Frame",
                    "Church BG",
                    "Popup Ready"],
                ShiftToBaseDelegate()),
            new("",
                "Wall Idle",
                ShiftToBaseDelegate(rotateAdjust:270f)),
            new("",
                "Roof Idle",
                ShiftToBaseDelegate(rotateAdjust:180f))
            ],
            fsmName: "Control"),

        new EnemyStatePatch(["Trobbio", "Tormented Trobbio"],
            "Init",
            delegate(FsmState state, object[] param)
            {
                float magicNumber = -0.03f;
                ShiftVar shiftFloorY = new()
                {
                    gameObject = new()
                    {
                        GameObject = (GameObject)param[0]
                    },
                    variableName = "Floor Y",
                    magicNumber = magicNumber,
                    callOnce = true
                };

                // Fix graphical issue when Trobbio spawns with a negative x scale
                SetScale setTrapdoorL = new()
                {
                    gameObject = new()
                    {
                        GameObject = state.Fsm.Variables.FindFsmGameObject("Trapdoor L"),
                        OwnerOption = OwnerDefaultOption.SpecifyGameObject
                    },
                    x = 1f,
                    y = new()
                    {
                        UseVariable = true,
                    },
                    z = new()
                    {
                        UseVariable = true,
                    },
                    vector = new()
                    {
                        UseVariable = true,
                    },
                    everyFrame =false,
                    lateUpdate = true
                };

                SetScale setTrapdoorR = new()
                {
                    gameObject = new()
                    {
                        GameObject = state.Fsm.Variables.FindFsmGameObject("Trapdoor R"),
                        OwnerOption = OwnerDefaultOption.SpecifyGameObject
                    },
                    x = -1f,
                    y = new()
                    {
                        UseVariable = true,
                    },
                    z = new()
                    {
                        UseVariable = true,
                    },
                    vector = new()
                    {
                        UseVariable = true,
                    },
                    everyFrame =false,
                    lateUpdate = true
                };

                ShiftToBaseDelegate(magicNumber: magicNumber, skipRaycast: true)(state, param);

                state.Actions = state.Actions.AddRangeToArray([shiftFloorY, setTrapdoorL, setTrapdoorR]);
            },
            fsmName: "Control"),

        new EnemyStateActionPatch(["Trobbio", "Tormented Trobbio"],
            ["Enter 1", "Flurry Shot", "Appear 1"],
            typeof(SetPositionToObject),
            delegate(FsmStateAction action, object[] param)
            {
                SetPositionToObject spto = (SetPositionToObject)action;

                if(spto.yOffset.Value != -2.93f)
                    return;

                spto.yOffset.Value = 13.3f - ShiftPosToBase.ShiftN(16.34f, Math.Abs(((GameObject)param[0]).transform.GetScaleY()), -0.03f);
            },
            fsmName: "Control"),

#endregion Library

#endregion Citadel

#region Underworks
        new EnemyStateActionPatchSet("Understore Small",
            [new("",
                ["Attack Pt1", "Flurry 2", "Flurry 8"],
                typeof(SetVelocityByScale),
                delegate(FsmStateAction action, object[] param)
                {
                    SetVelocityByScale scaler = (SetVelocityByScale)action;

                    scaler.speed.Value *= Math.Abs(((GameObject)param[0]).transform.GetScaleX());
                })
            ],
            fsmName: "Behaviour"),

        new EnemyStatePatchSet("Understore Poker",
            [new("",
                "Rest Wall",
                ShiftToBaseDelegate(rotateAdjust: 270f, magicNumber: -0.004f, skipRaycast: true)),
            new("",
                ["Start Work", "Sleep"],
                ShiftToBaseDelegate(magicNumber: -0.015f, skipRaycast: true))
            ],
            fsmName:"Control"),
        new EnemyStateActionPatchSet("Understore Poker",
            [new("",
                "Position",
                typeof(FloatTestToBool),
                delegate(FsmStateAction action, object[] param)
                {
                    FloatTestToBool test = (FloatTestToBool)action;

                    if(test.float2.Value == 5.25f)
                    {
                        test.float2.Value *= Math.Abs(((GameObject)param[0]).transform.GetScaleX());
                    }
                }),
            new("",
                "Position",
                typeof(DistanceFly),
                delegate(FsmStateAction action, object[] param)
                {
                    DistanceFly distanceFly = (DistanceFly)action;

                    distanceFly.distance.Value *= Math.Abs(((GameObject)param[0]).transform.GetScaleX());
                })
            ],
            fsmName: "Control"),

        new EnemyStatePatchSet("Understore Thrower",
            [new("",
                ["Rest Wall", "Rest Wall Work"],
                ShiftToBaseDelegate(rotateAdjust: 270f, magicNumber: -0.01f, skipRaycast: true)),
            new("",
                "Rest Ground",
                ShiftToBaseDelegate(skipRaycast: true))
            ],
            fsmName:"Control"),

        new EnemyStatePatchSet("Understore Heavy",
            [new("",
                ["Battle Dormant", "Sleeping", "Start Pray", "Start Work"],
                ShiftToBaseDelegate()),
            new("",
                "Drop Wait",
                ShiftToBaseDelegate(magicNumber: -0.01f))
            ],
            fsmName:"Behaviour"),

        new EnemyStateActionPatchSet("Understore Heavy",
            [new("",
                "Choice",
                typeof(RayCast2dV2),
                ScaleValueDelegate(new(){
                        OperatorType = ScaleValueSetting.TargetType.RayCast2dV2,
                        Target = new(){
                            Axis = ScaleValueSetting.ValueTarget.AxisDirections.y } })),
            new("",
                "Jump",
                typeof(RandomFloat),
                delegate(FsmStateAction action, object[] param)
                {
                    RandomFloat randomFloat = (RandomFloat)action;
                    float scale = Math.Abs(((GameObject)param[0]).transform.GetScaleX());

                    randomFloat.min.Value *= scale;
                    randomFloat.max.Value *= scale;
                })
            ],
            fsmName: "Behaviour"),

#endregion Underworks

#region Abyss
        new EnemyStatePatch("Abyss Crawler",
            "Check",
            ShiftToBaseDelegate(),
            fsmName: "Fall"),

        new EnemyFSMPatch("Gloomfly Cocoon S",
            delegate(PlayMakerFSM fsm, object[] param)
            {
                var cocoon = ((GameObject)param[0]);
                var gloomfly = fsm.FsmVariables.FindFsmGameObject("Gloomfly");
                fsm.FsmVariables.FindFsmGameObject("Gloomfly").OnChange += delegate()
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
                var gloomfly1 = fsm.FsmVariables.FindFsmGameObject("Gloomfly 1");
                var gloomfly2 = fsm.FsmVariables.FindFsmGameObject("Gloomfly 2");
                var gloomfly3 = fsm.FsmVariables.FindFsmGameObject("Gloomfly 3");

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
                        maxScale = gloomfly1.Value.transform.GetAbsScale();
                    if(gloomfly2.Value != null)
                        maxScale = Math.Abs(gloomfly2.Value.transform.GetScaleX()) > maxScale.x ? gloomfly2.Value.transform.GetAbsScale() : maxScale;
                    if(gloomfly3.Value != null)
                        maxScale = Math.Abs(gloomfly3.Value.transform.GetScaleX()) > maxScale.x ? gloomfly3.Value.transform.GetAbsScale() : maxScale;

                    if(maxScale == Vector3.zero) return;

                    cocoon.transform.localScale =  maxScale.MultiplyElements(cocoon.transform.localScale.normalized);
                    Enemy_Size_Rando.AdjustChildren(cocoon, cocoon.name);
                };

                fsm.FsmVariables.FindFsmGameObject("Gloomfly 1").OnChange += scaler;
                fsm.FsmVariables.FindFsmGameObject("Gloomfly 2").OnChange += scaler;
                fsm.FsmVariables.FindFsmGameObject("Gloomfly 3").OnChange += scaler;

                Vector3 maxScale = Vector3.zero;
                    if(gloomfly1.Value != null)
                        maxScale = gloomfly1.Value.transform.GetAbsScale();
                    if(gloomfly2.Value != null)
                        maxScale = Math.Abs(gloomfly2.Value.transform.GetScaleX()) > maxScale.x ? gloomfly2.Value.transform.GetAbsScale() : maxScale;
                    if(gloomfly3.Value != null)
                        maxScale = Math.Abs(gloomfly3.Value.transform.GetScaleX()) > maxScale.x ? gloomfly3.Value.transform.GetAbsScale() : maxScale;
                    if(maxScale == Vector3.zero) return;
                    cocoon.transform.localScale =  maxScale.MultiplyElements(cocoon.transform.localScale.normalized);
                    Enemy_Size_Rando.AdjustChildren(cocoon, cocoon.name);
            },
            fsmName: "Control"),
#endregion Abyss

#region Coral Tower
        new EnemyStatePatch("Coral Big Jellyfish",
            "Init",
            ShiftToBaseDelegate(rotateAdjust: 180f, magicNumber: -0.60f),
            fsmName: "Control"),

#endregion Coral Tower
    };

    /// <summary>Dictionary containing various enemy non-FSM patches</summary>
    private static readonly List<IEnemyObjectPatch> enemyObjectPatches =
    [
        new EnemyObjectPatch("Bone Flyer Giant", // fix arena bounds
            delegate(GameObject patchTarget, object[] param)
            {
                if(!patchTarget.scene.name.Equals("Ant_19")) return;

                var constrain = patchTarget.GetComponent<ConstrainPosition>();

                if(constrain == null) return;

                constrain.xMin = 39f;
                constrain.xMax = 66f;
            }),

        new EnemyObjectPatch("Farmer Centipede", // Fix emerge point
            delegate(GameObject patchTarget, object[] param)
            {
                var collider = patchTarget.GetComponent<BoxCollider2D>();

                if(collider == null) return;

                var height = collider.size.y;
                var digEmerge = patchTarget.transform.Find("Pt DigEmerge");

                digEmerge.transform.localPosition = digEmerge.transform.localPosition with { y = height * patchTarget.transform.GetAbsScaleY() * -1 };
            }),

        new EnemyObjectPatch(["Crowman Juror", "Crowman Dagger Juror", "Crowman Juror Tiny"], // Fix height constrain
            delegate(GameObject patchTarget, object[] param)
            {
                var collider = patchTarget.GetComponent<BoxCollider2D>();
                string name = (string)param[0];

                if(!patchTarget.scene.name.Equals("Room_CrowCourt_02")) return;

                var constrain = patchTarget.GetComponent<ConstrainPosition>();

                if(constrain == null)
                    return;

                float height = 0;

                if(collider == null)
                {
                    if(name.Equals("Crowman Juror"))
                        height = 2.7656f;
                    else if(name.Equals("Crowman Dagger Juror"))
                        height = 2.1786f;
                    else
                        height = 1.0922f;
                }
                else
                    height = collider.size.y;

                constrain.yMin = 20f + (height * patchTarget.transform.GetAbsScaleY()) / 2;
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
                        patchTarget.transform.GetScaleY(),
                        collider.size.y / 2,
                        collider.offset.y,
                        Mathf.Cos(Mathf.Deg2Rad * patchTarget.transform.rotation.eulerAngles.z))
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

                moveConstrain.Value *= Math.Abs(patchTarget.transform.GetScaleX());
            })*/
    ];
}
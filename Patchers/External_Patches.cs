#if TESTING

using System;
using System.Collections.Generic;
using System.Linq;

using HarmonyLib;

using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Smol_Randomizer.Patchers.Enemy;
using Smol_Randomizer.Patchers.Scene;
using Smol_Randomizer.Randomizers;

using UnityEngine;

using static Smol_Randomizer.CuteRandoCore;

namespace Smol_Randomizer.Patchers;

internal class External_Patches
{
    internal static Dictionary<ExternalPatchTypes, HashSet<ISmolPatch>> externalPatches = [];

    public class ExternalPatchConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(ISmolExternalPatch);
        }

        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            JObject obj = JObject.Load(reader);
            ExternalPatchTypes.TryParse<ExternalPatchTypes>(obj["PatchType"]?.Value<string>(), true, out ExternalPatchTypes result);
            switch (result)
            {
                case ExternalPatchTypes.Scene_State_FSM:
                    return obj.ToObject<SceneStateExternalPatch>(serializer);

                case ExternalPatchTypes.Scene_State_Action_FSM:
                    return obj.ToObject<SceneStateActionExternalPatch>(serializer);

                case ExternalPatchTypes.Enemy_Object:
                    return obj.ToObject<EnemyObjectExternalPatch>(serializer);

                case ExternalPatchTypes.Enemy_State_FSM:
                    return obj.ToObject<EnemyStateExternalPatch>(serializer);

                case ExternalPatchTypes.Enemy_State_Action_FSM:
                    return obj.ToObject<EnemyStateActionExternalPatch>(serializer);

                default:
                    return null;
            }
        }

        public override bool CanWrite
        {
            get { return false; }
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }

    public class ExternalPatchTextConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType.Name.Equals("PatchInfo_Base`1");
        }

        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            JObject obj = JObject.Load(reader);
            string target = obj["PatchType"]?.Value<string>() ?? "";
            switch (target)
            {
                case "State":
                    return obj.ToObject<StatePatchInfo>(serializer);

                case "StateAction":
                    return obj.ToObject<StateActionPatchInfo>(serializer);

                case "GameObject":
                    return obj.ToObject<ObjectPatchInfo>(serializer);

                default:
                    return null;
            }
        }

        public override bool CanWrite
        {
            get { return false; }
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }

    internal static void LoadPatches()
    {
        CuteRandoCore.Log.LogInfo("Loading External Patches");
        foreach (var patchGroup in externalPatches)
        {
            switch (patchGroup.Key)
            {
                case ExternalPatchTypes.Scene_State_FSM:
                    foreach (SceneStatePatch patch in patchGroup.Value)
                        SceneFSMPatches.sceneStateCollection.UnregisterPatchInCollection(patch.SceneName, patch.ObjectName, patch);
                    break;

                case ExternalPatchTypes.Scene_State_Action_FSM:
                    foreach (SceneStateActionPatch patch in patchGroup.Value)
                        SceneFSMPatches.sceneStateActionCollection.UnregisterPatchInCollection(patch.SceneName, patch.ObjectName, patch);
                    break;

                case ExternalPatchTypes.Enemy_Object:
                    foreach (EnemyObjectPatch patch in patchGroup.Value)
                        EnemyObjectPatchCollection.Instance.UnregisterPatchInCollection(patch.Name, patch);
                    break;

                case ExternalPatchTypes.Enemy_State_FSM:
                    foreach (EnemyStatePatch patch in patchGroup.Value)
                        EnemyFSMPatches.enemyState.UnregisterPatchInCollection(patch.EnemyName, patch);
                    break;

                case ExternalPatchTypes.Enemy_State_Action_FSM:
                    foreach (EnemyStateActionPatch patch in patchGroup.Value)
                        EnemyFSMPatches.enemyStateAction.UnregisterPatchInCollection(patch.EnemyName, patch);
                    break;

                default: throw new NotImplementedException();
            }
        }

        externalPatches.Clear();

        ImportJsonFile("External Patches", out string PatchString);

        JsonSerializerSettings settings = new JsonSerializerSettings()
        {
            Converters = {
                new ExternalPatchConverter(),
                new ExternalPatchTextConverter()
            }
        };
        HashSet<ISmolExternalPatch> importedPatches = JsonConvert.DeserializeObject<HashSet<ISmolExternalPatch>>(PatchString, settings) ?? [];

        foreach (var rawPatch in importedPatches)
        {
            bool appliedPatch = false;

            switch (rawPatch.PatchType)
            {
                case ExternalPatchTypes.Scene_State_FSM:
                    if (rawPatch is SceneStateExternalPatch sceneStatePatch)
                    {
                        var p = sceneStatePatch.Patch.GeneratePatch();
                        if (p == null)
                        {
                            Log.LogError($"Patch is null or malformed within Scene State FSM Patch for {sceneStatePatch.SceneName}.{sceneStatePatch.ObjectName}.{sceneStatePatch.Name}");
                            return;
                        }
                        var u = sceneStatePatch.Unpatch?.GeneratePatch();
                        SceneStatePatch compiledPatch = new SceneStatePatch(sceneStatePatch.SceneName, sceneStatePatch.ObjectName, sceneStatePatch.Name, p, u, sceneStatePatch.FSMName);
                        if (rawPatch.Overwrite)
                            appliedPatch = SceneFSMPatches.OverwritePatch(compiledPatch);
                        if (!appliedPatch)
                            SceneFSMPatches.sceneStateCollection.RegisterPatchInCollection(sceneStatePatch.SceneName, sceneStatePatch.ObjectName, compiledPatch);
                        AddPatchToDictionary(rawPatch.PatchType, compiledPatch);
                    }
                    break;

                case ExternalPatchTypes.Scene_State_Action_FSM:
                    if (rawPatch is SceneStateActionExternalPatch sceneStateActionPatch)
                    {
                        var p = sceneStateActionPatch.Patch.GeneratePatch();
                        var t = sceneStateActionPatch.ActionType;
                        if (p == null || t == null)
                        {
                            Log.LogError($"" +
                                $"{(p == null ? "Patch " : "")}" +
                                $"{(p == null && t == null ? "& " : "")}" +
                                $"{(t == null ? "Target " : "")}" +
                                $"{(p == null && t == null ? "are " : "is ")}" +
                                $"null or malformed within Scene State Action FSM Patch for {sceneStateActionPatch.SceneName}.{sceneStateActionPatch.ObjectName}.{sceneStateActionPatch.Name}");
                            return;
                        }
                        var u = sceneStateActionPatch.Unpatch?.GeneratePatch();
                        SceneStateActionPatch compiledPatch = new SceneStateActionPatch(sceneStateActionPatch.SceneName, sceneStateActionPatch.ObjectName, sceneStateActionPatch.Name, sceneStateActionPatch.ActionType, p, u, sceneStateActionPatch.FSMName);
                        if (rawPatch.Overwrite)
                            appliedPatch = SceneFSMPatches.OverwritePatch(compiledPatch);
                        if (!appliedPatch)
                            SceneFSMPatches.sceneStateActionCollection.RegisterPatchInCollection(sceneStateActionPatch.SceneName, sceneStateActionPatch.ObjectName, compiledPatch);
                        AddPatchToDictionary(rawPatch.PatchType, compiledPatch);
                    }
                    break;

                case ExternalPatchTypes.Enemy_Object:
                    if (rawPatch is EnemyObjectExternalPatch enemyObjectPatch)
                    {
                        var p = enemyObjectPatch.Patch.GeneratePatch();
                        if (p == null)
                        {
                            Log.LogError($"Patch is null or malformed within Enemy Object Patch for {enemyObjectPatch.Name}");
                            return;
                        }
                        var u = enemyObjectPatch.Unpatch?.GeneratePatch();
                        EnemyObjectPatch compiledPatch = new EnemyObjectPatch(enemyObjectPatch.Name, p, u);
                        if (rawPatch.Overwrite)
                            CuteRandoCore.Log.LogError($"Overwrite patch failed, overwrite not implemented for Object Patches; Name: {rawPatch.Name}, Type: {rawPatch.PatchType}");
                        EnemyObjectPatchCollection.Instance.RegisterPatchInCollection(compiledPatch.Name, compiledPatch);
                        AddPatchToDictionary(rawPatch.PatchType, compiledPatch);
                    }
                    break;

                case ExternalPatchTypes.Enemy_State_FSM:
                    if (rawPatch is EnemyStateExternalPatch enemyStatePatch)
                    {
                        var p = enemyStatePatch.Patch.GeneratePatch();
                        if (p == null)
                        {
                            Log.LogError($"Patch is null or malformed within Enemy State FSM Patch for {enemyStatePatch.EnemyName}.{enemyStatePatch.Name}");
                            return;
                        }
                        var u = enemyStatePatch.Unpatch?.GeneratePatch();
                        EnemyStatePatch compiledPatch;

                        if (enemyStatePatch.Names.Length > 0)
                            compiledPatch = new EnemyStatePatch(enemyStatePatch.EnemyName, enemyStatePatch.Names, p, u, enemyStatePatch.FSMName, enemyStatePatch.LatePatch, enemyStatePatch.ReInit);
                        else
                            compiledPatch = new EnemyStatePatch(enemyStatePatch.EnemyName, enemyStatePatch.Name, p, u, enemyStatePatch.FSMName, enemyStatePatch.LatePatch, enemyStatePatch.ReInit);

                        if (rawPatch.Overwrite)
                            appliedPatch = EnemyFSMPatches.OverwritePatch(compiledPatch);
                        if (!appliedPatch)
                            EnemyFSMPatches.enemyState.RegisterPatchInCollection(enemyStatePatch.EnemyName, compiledPatch);
                        AddPatchToDictionary(rawPatch.PatchType, compiledPatch);
                    }
                    break;

                case ExternalPatchTypes.Enemy_State_Action_FSM:
                    if (rawPatch is EnemyStateActionExternalPatch enemyStateActionPatch)
                    {
                        var p = enemyStateActionPatch.Patch.GeneratePatch();
                        var t = enemyStateActionPatch.ActionType;
                        if (p == null || t == null)
                        {
                            Log.LogError($"" +
                                $"{(p == null ? "Patch " : "")}" +
                                $"{(p == null && t == null ? "& " : "")}" +
                                $"{(t == null ? "Target " : "")}" +
                                $"{(p == null && t == null ? "are " : "is ")}" +
                                $"null or malformed within Enemy State Action FSM Patch for {enemyStateActionPatch.EnemyName}.{enemyStateActionPatch.Name}");
                            return;
                        }
                        var u = enemyStateActionPatch.Unpatch?.GeneratePatch();

                        EnemyStateActionPatch compiledPatch = new EnemyStateActionPatch(enemyStateActionPatch.EnemyName, enemyStateActionPatch.Name, enemyStateActionPatch.ActionType, p, u, enemyStateActionPatch.FSMName, enemyStateActionPatch.LatePatch);

                        if (enemyStateActionPatch.Names.Length > 0)
                            compiledPatch = new EnemyStateActionPatch(enemyStateActionPatch.EnemyName, enemyStateActionPatch.Names, enemyStateActionPatch.ActionType, p, u, enemyStateActionPatch.FSMName, enemyStateActionPatch.LatePatch, enemyStateActionPatch.ReInit);
                        else
                            compiledPatch = new EnemyStateActionPatch(enemyStateActionPatch.EnemyName, enemyStateActionPatch.Name, enemyStateActionPatch.ActionType, p, u, enemyStateActionPatch.FSMName, enemyStateActionPatch.LatePatch, enemyStateActionPatch.ReInit);

                        if (rawPatch.Overwrite)
                            appliedPatch = EnemyFSMPatches.OverwritePatch(compiledPatch);
                        if (!appliedPatch)
                            EnemyFSMPatches.enemyStateAction.RegisterPatchInCollection(enemyStateActionPatch.EnemyName, compiledPatch);
                        AddPatchToDictionary(rawPatch.PatchType, compiledPatch);
                    }
                    break;

                default:
                    Log.LogError($"Invalid patch in External Patches {rawPatch.Name}.");
                    return;
            }
        }
        CuteRandoCore.Log.LogInfo("Done Loading External Patches");

        static void AddPatchToDictionary(ExternalPatchTypes key, ISmolPatch patch)
        {
            if (externalPatches.ContainsKey(key))
            {
                externalPatches[key].Add(patch);
            }
            else
            {
                externalPatches.Add(key, [patch]);
            }
        }
    }

    #region State Patches

    public class SceneStateExternalPatch : StateExternalPatch_Base
    {
        public string SceneName = "";

        /// <summary>Name of the patch target</summary>
        public string ObjectName = "";

        public override PatchInfo_Base<FsmState> Patch { get; set; }

        public override PatchInfo_Base<FsmState>? Unpatch { get; set; }
    }

    public class EnemyStateExternalPatch : StateExternalPatch_Base
    {
        public string EnemyName = "";

        public override PatchInfo_Base<FsmState> Patch { get; set; }

        public override PatchInfo_Base<FsmState>? Unpatch { get; set; }
    }

    public class StatePatchInfo : PatchInfo_Base<FsmState>
    {
        public bool StartOfAction = true;

        // specific options
        public bool[] BoolOptions = [];

        public float[] FloatOptions = [];

        public override Action<FsmState, object[]?> GeneratePatch()
        {
            switch (PatchTarget)
            {
                case "ShiftPosToBase":
                    return Enemy_Size_Rando.CreateShiftToBaseDelegate(
                        StartOfAction, // atStart
                        FloatOptions.ElementAtOrDefault(0), // rotateAdjust
                        new Vector2(FloatOptions.ElementAtOrDefault(1), FloatOptions.ElementAtOrDefault(2)), // backupSize
                        BoolOptions.ElementAtOrDefault(0), // reverseX
                        BoolOptions.ElementAtOrDefault(1), // reverseY
                        FloatOptions.ElementAtOrDefault(3), // magicNumber
                        BoolOptions.ElementAtOrDefault(2));  // tempInvincible
                case "SetVelocity2d":
                    return delegate (FsmState state, object[]? param)
                    {
                        if (param == null) return;

                        SetVelocity2d velocity = new()
                        {
                            gameObject = new()
                            {
                                GameObject = (GameObject)param[0]
                            },
                            x = FloatOptions.ElementAtOrDefault(0),
                            y = FloatOptions.ElementAtOrDefault(1),
                            vector = new Vector2(FloatOptions.ElementAtOrDefault(0), FloatOptions.ElementAtOrDefault(1))
                        };

                        if (StartOfAction)
                        {
                            FsmStateAction[] bassAckwards = [velocity];
                            state.Actions = bassAckwards.AddRangeToArray(state.Actions);
                        }
                        else state.Actions = state.Actions.AddItem(velocity).ToArray();
                    };
                default:
                    throw new NotImplementedException();
            }
        }
    }

    public abstract class StateExternalPatch_Base
    : ObjectExternalPatch_Base<FsmState>, IFSMPatch
    {
        public string FSMName { get; set; } = "";

        public bool LatePatch = false;

        public bool ReInit = false;
    }

    #endregion State Patches

    #region State Action

    public class EnemyStateActionExternalPatch : StateActionExternalPatch_Base
    {
        public string EnemyName = "";

        public override PatchInfo_Base<FsmStateAction> Patch { get; set; }

        public override PatchInfo_Base<FsmStateAction>? Unpatch { get; set; }
    }

    public class SceneStateActionExternalPatch : StateActionExternalPatch_Base
    {
        public string SceneName = "";

        /// <summary>Name of the patch target</summary>
        public string ObjectName = "";

        public override PatchInfo_Base<FsmStateAction> Patch { get; set; }

        public override PatchInfo_Base<FsmStateAction>? Unpatch { get; set; }
    }

    public class StateActionPatchInfo : PatchInfo_Base<FsmStateAction>
    {
        public override Action<FsmStateAction, object[]?> GeneratePatch()
        {
            throw new NotImplementedException();
        }
    }

    public abstract class StateActionExternalPatch_Base()
   : ObjectExternalPatch_Base<FsmStateAction>, IFSMPatch
    {
        public string FSMName { get; set; } = "";

        public string ActionTypeString = "";

        public bool LatePatch = false;

        public bool ReInit = false;

        public Type ActionType
        {
            get
            {
                return Type.GetType(ActionTypeString);
            }
        }
    }

    #endregion State Action

    #region Object Patches

    public class EnemyObjectExternalPatch : ObjectExternalPatch_Base<GameObject>
    {
        public override PatchInfo_Base<GameObject> Patch { get; set; }

        public override PatchInfo_Base<GameObject>? Unpatch { get; set; }
    }

    public class ObjectPatchInfo : PatchInfo_Base<FsmStateAction>
    {
        public override Action<FsmStateAction, object[]?> GeneratePatch()
        {
            throw new NotImplementedException();
        }
    }

    #endregion Object Patches

    #region Base Abstract

    public abstract class ObjectExternalPatch_Base<PatchTarget>()
    : ISmolExternalPatch
    {
        public string Name { get; set; } = "";
        public string[] Names { get; set; } = [];
        public ExternalPatchTypes PatchType { get; set; }
        public bool Overwrite { get; set; }
        public abstract PatchInfo_Base<PatchTarget> Patch { get; set; }
        public abstract PatchInfo_Base<PatchTarget>? Unpatch { get; set; }
    }

    public abstract class PatchInfo_Base<Target>
        : ISmolExternalPatchInfo
    {
        public string PatchTarget { get; set; } = "";

        public abstract Action<Target, object[]?> GeneratePatch();
    }

    #endregion Base Abstract

    public interface ISmolExternalPatch
    {
        public string Name { get; set; }
        public string[] Names { get; set; }
        public ExternalPatchTypes PatchType { get; set; }
        public bool Overwrite { get; set; }
    }

    public interface ISmolExternalPatchInfo
    {
        public string PatchTarget { get; set; }
    }

    public enum ExternalPatchTypes
    {
        Scene_State_Action_FSM,
        Scene_State_FSM,
        Enemy_Object,
        Enemy_State_Action_FSM,
        Enemy_State_FSM
    }
}

#endif
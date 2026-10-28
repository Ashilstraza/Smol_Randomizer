#if TESTING

using HutongGames.PlayMaker;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Smol_Randomizer.Patchers.Enemy;
using Smol_Randomizer.Patchers.ScenePatches;
using Smol_Randomizer.Randomizers;

using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;

using static Smol_Randomizer.CuteRandoCore;

namespace Smol_Randomizer.Patchers;

/// <summary>
/// Class to allow for loading external patches. These patches require a specific structure to load that is created beforehand.
/// </summary>
internal class External_Patches
{
    /// <summary>
    /// Dictionary containing all the externally loaded patches. These are removed prior to loading a new set of
    /// external patches.
    /// </summary>
    internal static Dictionary<ExternalPatchTypes, HashSet<ISmolPatch>> externalPatches = [];

    /// <summary>Json Converter for converting external patches into their specific objects.</summary>
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

    /// <summary>Json Converter for seting up the type of patch that the external patch is supposed to be.</summary>
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

    /// <summary>Method to load the external patches.</summary>
    /// <remarks>
    /// To load the external patches, first any previously loaded external patches are unregistered.
    /// <para>Next, the Json file is read and then deserialized.</para>
    /// <para>Following deserialization, the patches are then generated and then registered into their specific collections.</para>
    /// </remarks>
    internal static void LoadPatches()
    {
        CuteRandoCore.Log.LogInfo("Loading External Patches");

        // Unregister patches, then clear the dictionary containing the old patches
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

                default:
                    Log.LogError($"Invalid patch type External Patches {patchGroup.Key}.");
                    break;
            }
        }

        externalPatches.Clear();

        // Import the external patches, then deserialize
        ImportJsonFile("External Patches", out string PatchString);

        JsonSerializerSettings settings = new JsonSerializerSettings()
        {
            Converters = {
                new ExternalPatchConverter(),
                new ExternalPatchTextConverter()
            }
        };
        HashSet<ISmolExternalPatch> importedPatches = JsonConvert.DeserializeObject<HashSet<ISmolExternalPatch>>(PatchString, settings) ?? [];

        // Process the imported patches by generating them and then registering them into their specific collections
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
                        var t = sceneStateActionPatch.Type;
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
                        SceneStateActionPatch compiledPatch = new SceneStateActionPatch(sceneStateActionPatch.SceneName, sceneStateActionPatch.ObjectName, sceneStateActionPatch.Name, sceneStateActionPatch.Type, p, u, sceneStateActionPatch.FSMName);

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
                            compiledPatch = new EnemyStatePatch(enemyStatePatch.EnemyName, enemyStatePatch.Names, p, u, enemyStatePatch.FSMName);
                        else
                            compiledPatch = new EnemyStatePatch(enemyStatePatch.EnemyName, enemyStatePatch.Name, p, u, enemyStatePatch.FSMName);

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
                        var t = enemyStateActionPatch.Type;
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

                        EnemyStateActionPatch compiledPatch = new EnemyStateActionPatch(enemyStateActionPatch.EnemyName, enemyStateActionPatch.Name, enemyStateActionPatch.Type, p, u);

                        if (enemyStateActionPatch.Names.Length > 0)
                            compiledPatch = new EnemyStateActionPatch(enemyStateActionPatch.EnemyName, enemyStateActionPatch.Names, enemyStateActionPatch.Type, p, u, enemyStateActionPatch.FSMName);
                        else
                            compiledPatch = new EnemyStateActionPatch(enemyStateActionPatch.EnemyName, enemyStateActionPatch.Name, enemyStateActionPatch.Type, p, u, enemyStateActionPatch.FSMName);

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

    /// <summary>
    /// Wrapper for patching a scene's <see cref="FsmState" />
    /// <para>No currently implemented <see cref="PatchInfo_Base{Target}.PatchTarget" /> s</para>
    /// </summary>
    public class SceneStateExternalPatch : StateExternalPatch_Base, IScenePatch
    {
        public string SceneName { get; set; } = "";

        public string ObjectName { get; set; } = "";

        public override PatchInfo_Base<FsmState> Patch { get; set; }

        public override PatchInfo_Base<FsmState>? Unpatch { get; set; }
    }

    /// <summary>Wrapper for patching an enemy's <see cref="FsmState" /></summary>
    public class EnemyStateExternalPatch : StateExternalPatch_Base, IEnemyPatch
    {
        public string EnemyName { get; set; } = "";

        public string[] EnemyNames { get; set; } = [];

        public override PatchInfo_Base<FsmState> Patch { get; set; }

        public override PatchInfo_Base<FsmState>? Unpatch { get; set; }
    }

    /// <summary>Contains the patch information to patch an <see cref="FsmState" /> with a new <see cref="FsmStateAction" /></summary>
    public class StatePatchInfo : PatchInfo_Base<FsmState>
    {
        /// <summary>If the patch should add the <see cref="FsmStateAction" /> at the start of the <see cref="FsmState" /></summary>
        public bool StartOfState = true;

        // per PatchTarget options
        /// <summary>Bool arguments for the <see cref="PatchInfo_Base{Target}.PatchTarget" /></summary>
        public bool[] BoolOptions = [];

        /// <summary>Float arguments for the <see cref="PatchInfo_Base{Target}.PatchTarget" /></summary>
        public float[] FloatOptions = [];

        public override Action<FsmState, object[]> GeneratePatch()
        {
            switch (PatchTarget)
            {
                case "ShiftPosToBase":
                    return Enemy_Size_Rando.ShiftToBaseDelegate(
                        StartOfState, // atStart
                        FloatOptions.ElementAtOrDefault(0), // rotateAdjust
                        new Vector2(FloatOptions.ElementAtOrDefault(1), FloatOptions.ElementAtOrDefault(2)), // backupSize
                        BoolOptions.ElementAtOrDefault(0), // reverseX
                        BoolOptions.ElementAtOrDefault(1), // reverseY
                        FloatOptions.ElementAtOrDefault(3), // magicNumber
                        BoolOptions.ElementAtOrDefault(3), // skipRaycast
                        BoolOptions.ElementAtOrDefault(4)); // cancelYVelocity
                default:
                    throw new NotImplementedException();
            }
        }
    }

    /// <summary>Abstract class for wrapping an externally loaded <see cref="FsmState" /> patch</summary>
    public abstract class StateExternalPatch_Base
    : ObjectExternalPatch_Base<FsmState>, IFSMPatch
    {
        public string FSMName { get; set; } = "";
    }

    #endregion State Patches

    #region State Action

    /// <summary>
    /// Wrapper for patching an enemy's <see cref="FsmStateAction" />
    /// <para>Not currently implemented.</para>
    /// </summary>
    public class EnemyStateActionExternalPatch : StateActionExternalPatch_Base, IEnemyPatch
    {
        public string EnemyName { get; set; } = "";

        public string[] EnemyNames { get; set; } = [];

        public override PatchInfo_Base<FsmStateAction> Patch { get; set; }

        public override PatchInfo_Base<FsmStateAction>? Unpatch { get; set; }
    }

    /// <summary>
    /// Wrapper for patching a <see cref="Scene" /><see cref="FsmStateAction" />
    /// <para>Not currently implemented.</para>
    /// </summary>
    public class SceneStateActionExternalPatch : StateActionExternalPatch_Base, IScenePatch
    {
        public string SceneName { get; set; } = "";

        public string ObjectName { get; set; } = "";

        public override PatchInfo_Base<FsmStateAction> Patch { get; set; }

        public override PatchInfo_Base<FsmStateAction>? Unpatch { get; set; }
    }

    /// <summary>
    /// Contains the patch information to patch an <see cref="FsmStateAction" />
    /// <para>Not currently implemented.</para>
    /// </summary>
    public class StateActionPatchInfo : PatchInfo_Base<FsmStateAction>
    {
        public override Action<FsmStateAction, object[]> GeneratePatch()
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>Abstract class for wrapping an externally loaded <see cref="FsmStateAction" /> patch</summary>
    public abstract class StateActionExternalPatch_Base()
   : ObjectExternalPatch_Base<FsmStateAction>, IFSMPatch, IFSMActionPatch
    {
        public string FSMName { get; set; } = "";

        /// <summary>The type as a <see langword="string" /></summary>
        public string ActionTypeString = "";

        public Type Type
        {
            get
            {
                return Type.GetType(ActionTypeString);
            }
        }
    }

    #endregion State Action

    #region Object Patches

    /// <summary>
    /// Wrapper for patching a <see cref="GameObject" />
    /// <para>Not currently implemented.</para>
    /// </summary>
    public class EnemyObjectExternalPatch : ObjectExternalPatch_Base<GameObject>
    {
        public override PatchInfo_Base<GameObject> Patch { get; set; }

        public override PatchInfo_Base<GameObject>? Unpatch { get; set; }
    }

    /// <summary>
    /// Contains the patch information to patch a <see cref="GameObject" />
    /// <para>Not currently implemented.</para>
    /// </summary>
    public class ObjectPatchInfo : PatchInfo_Base<GameObject>
    {
        public override Action<GameObject, object[]> GeneratePatch()
        {
            throw new NotImplementedException();
        }
    }

    #endregion Object Patches

    #region Base Abstract

    /// <summary>Abstract class for the patch wrapper</summary>
    /// <typeparam name="PatchTarget">The thing that is being patched</typeparam>
    public abstract class ObjectExternalPatch_Base<PatchTarget>()
    : ISmolExternalPatch
    {
        public string Name { get; set; } = "";
        public string[] Names { get; set; } = [];
        public ExternalPatchTypes PatchType { get; set; }

        /// <summary>Information to create the patch that will be appiled</summary>
        public abstract PatchInfo_Base<PatchTarget> Patch { get; set; }

        /// <summary>Information to create the unpatcher that will remove the applied patch</summary>
        public abstract PatchInfo_Base<PatchTarget>? Unpatch { get; set; }
    }

    /// <summary>Abstract class for the patch</summary>
    /// <typeparam name="Target">The type of thing that is being patched</typeparam>
    public abstract class PatchInfo_Base<Target>
        : ISmolExternalPatchInfo
    {
        public string PatchTarget { get; set; } = "";

        /// <summary>Called to generate the patch</summary>
        /// <returns>The generated patch</returns>
        public abstract Action<Target, object[]> GeneratePatch();
    }

    #endregion Base Abstract

    /// <summary>Interface for external patch setup</summary>
    public interface ISmolExternalPatch
    {
        /// <summary>Name of what is being patched</summary>
        public string Name { get; set; }

        /// <summary><see cref="Array" /> of names for what is being patched</summary>
        public string[] Names { get; set; }

        /// <summary>The type of patch that is being imported</summary>
        public ExternalPatchTypes PatchType { get; set; }
    }

    /// <summary>Interface for external patch information</summary>
    public interface ISmolExternalPatchInfo
    {
        /// <summary>The target of the patch</summary>
        public string PatchTarget { get; set; }
    }

    /// <summary>The types of patches that can be imported.</summary>
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
using HarmonyLib;

using HutongGames.PlayMaker;

using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using UnityEngine.SceneManagement;

using static Smol_Randomizer.Patchers.Enemy.EnemyFSMPatches;

namespace Smol_Randomizer.Patchers.ScenePatches;

/// <summary>Handles patching <see cref="Scene" /> <see cref="PlayMakerFSM" />s</summary>
internal class SceneFSMPatches
{
    private static readonly Lazy<SceneFSMPatches> instance = new(() => new SceneFSMPatches());

    /// <summary>Class for containing a set of <see cref="SceneStateActionPatch" />es</summary>
    public class SceneStateActionCollection : SceneFSMPatchCollections_Base<SceneStateActionPatch, SceneStateActionPatchSet, FsmStateAction>;
    
    /// <summary>Class for containing a set of <see cref="SceneStatePatch" />es</summary>
    public class SceneStateCollection : SceneFSMPatchCollections_Base<SceneStatePatch, SceneStatePatchSet, FsmState>;

    /// <summary>A set of <see cref="SceneFSMPatches" /></summary>
    public static SceneFSMPatches Instance => instance.Value;

    /// <summary>A set of <see cref="SceneStateActionPatch" /> patches</summary>
    public static readonly SceneStateActionCollection sceneStateActionCollection = new();

    /// <summary>As set of <see cref="SceneStatePatch" /> patches</summary>
    public static readonly SceneStateCollection sceneStateCollection = new();

    /// <summary>Apply the various <see cref="Scene" /> patches</summary>
    /// <param name="scene">The <see cref="Scene" /> to patch</param>
    internal static void ApplyPatches(Scene scene)
    {
        if (!scene.IsValid()) // Something is fishy, abort.
            return;

        HashSet<GameObject> objects = scene.GetRootGameObjects().ToHashSet();

        sceneStateActionCollection.ApplyPatches(scene, objects);
        sceneStateCollection.ApplyPatches(scene, objects);
    }

    /// <summary>Remove the various <see cref="Scene" /> patches</summary>
    /// <param name="scene">The <see cref="Scene" /> to unpatch</param>
    internal static void RemovePatches(Scene scene)
    {
        if (!scene.IsValid()) // Something is fishy, abort.
            return;
        HashSet<GameObject> objects = scene.GetRootGameObjects().ToHashSet();

        sceneStateActionCollection.RemovePatches(scene, objects);
        sceneStateCollection.RemovePatches(scene, objects);
    }

    /// <summary>Registers a collection of patches</summary>
    /// <param name="scenePatchCollection">The collection of patches to add</param>
    public static void RegisterPatchCollection(List<ISceneFSMPatch> scenePatchCollection)
    {
        int stateActionPatches = 0;
        int statePatches = 0;

        foreach (var patch in scenePatchCollection)
        {
            if (patch is SceneStateActionPatch or SceneStateActionPatchSet)
                stateActionPatches += sceneStateActionCollection.RegisterPatchInCollection(patch.Name, patch, patch.SceneName);
            else if (patch is SceneStatePatch or SceneStatePatchSet)
                statePatches += sceneStateCollection.RegisterPatchInCollection(patch.Name, patch, patch.SceneName);
            else
                CuteRandoCore.DebugLog($"Register patch called with invalid patch type, Name: {patch.Name}, Type: {patch.GetType()} Scene: {patch.SceneName}", DebugLogOptions.Error, true);
        }

        CuteRandoCore.Log.LogInfo($"Registered {stateActionPatches} Scene State Action Patches and {statePatches} Scene State Patches");
    }

    /// <summary>Unregisters a collection of patches</summary>
    /// <param name="scenePatchCollection">The collection of patches to remove</param>
    public static void UnregisterPatchCollection(List<ISceneFSMPatch> scenePatchCollection)
    {
        int stateActionPatches = 0;
        int statePatches = 0;

        foreach (var patch in scenePatchCollection)
        {
            if (patch is SceneStateActionPatch or SceneStateActionPatchSet)
                stateActionPatches += sceneStateActionCollection.UnregisterPatchInCollection(patch.Name, patch, patch.SceneName);
            else if (patch is SceneStatePatch or SceneStatePatchSet)
                statePatches += sceneStateCollection.UnregisterPatchInCollection(patch.Name, patch, patch.SceneName);
            else
                CuteRandoCore.DebugLog($"Unregister patch called with invalid patch type, Name: {patch.Name}, Type: {patch.GetType()} Scene: {patch.SceneName}", DebugLogOptions.Error, true);
        }

        CuteRandoCore.Log.LogInfo($"Unregistered {stateActionPatches} Scene State Action Patches and {statePatches} Scene State Patches");
    }

    /// <summary>Registers a patch and overwrites any existing ones with the new one</summary>
    /// <param name="patch">The patch to add</param>
    /// <returns>If the patch was added</returns>
    public static bool OverwritePatch(ISceneFSMPatch patch)
    {
        if (patch is SceneStateActionPatch actionPatch)
        {
            sceneStateActionCollection.Patches.TryGetValue(actionPatch.SceneName, out var scenePatchSet);
            scenePatchSet.TryGetValue(actionPatch.Name, out var patchSet);

            var oldPatch = patchSet?.Where(p => p.FSMName.Equals(actionPatch.FSMName)
                                                && p.Name.Equals(actionPatch.Name)
                                                && p.TypeString.Equals(actionPatch.TypeString)).FirstOrDefault();
            if (oldPatch != null && patchSet != null)
            {
                patchSet.Remove(oldPatch);
                patchSet.Add(actionPatch);
                return true;
            }

            oldPatch = patchSet?.Where(p => p.FSMName.Equals(actionPatch.FSMName)
                                                && p.NameArray.Contains(actionPatch.Name)
                                                && p.TypeString.Equals(actionPatch.TypeString)).FirstOrDefault();

            if (oldPatch != null && patchSet != null)
            {
                oldPatch.NameArray = oldPatch.NameArray.Except([actionPatch.Name]).ToArray();
                patchSet.Add(actionPatch);
                return true;
            }
        }
        else if (patch is SceneStatePatch statePatch)
        {
            sceneStateCollection.Patches.TryGetValue(statePatch.SceneName, out var scenePatchSet);
            scenePatchSet.TryGetValue(statePatch.Name, out var patchSet);

            var oldPatch = patchSet?.Where(p => p.FSMName.Equals(statePatch.FSMName)
                                                && p.Name.Equals(statePatch.Name)).FirstOrDefault();
            if (oldPatch != null && patchSet != null)
            {
                patchSet.Remove(oldPatch);
                patchSet.Add(statePatch);
                return true;
            }

            oldPatch = patchSet?.Where(p => p.FSMName.Equals(statePatch.FSMName)
                                                && p.NameArray.Contains(statePatch.Name)).FirstOrDefault();

            if (oldPatch != null && patchSet != null)
            {
                oldPatch.NameArray = oldPatch.NameArray.Except([statePatch.Name]).ToArray();
                patchSet.Add(statePatch);
                return true;
            }
        }
        else
            CuteRandoCore.DebugLog($"Overwrite patch called with invalid patch type, Name: {patch.Name}, Type: {patch.GetType()} Scene: {patch.SceneName}", DebugLogOptions.Error, true);

        return false;
    }
}

#region FSM State Action
/// <inheritdoc />
/// <param name="sceneName"> Name of the <see cref="Scene" /> that the <see cref="GameObject" /> is within</param>
/// <param name="objectName">Name of the <see cref="GameObject" /> to be patched</param>
/// <param name="patches">   The set of patches for the <see cref="GameObject" /></param>
/// <param name="fsmName">   Optional: Name of the <see cref="PlayMakerFSM" /> to patch, the first found
/// <see cref="PlayMakerFSM" /> is used if not provided</param>
/// <param name="objectNames"><see cref="Array" /> of <see cref="GameObject" /> names to patch</param>
/// <param name="sceneNames"><see cref="Array" /> of <see cref="Scene" /> names to patch</param>
public class SceneStateActionPatchSet(string sceneName, string objectName, List<SceneStateActionPatch> patches, string fsmName = "")
    : StateActionPatchSet_Base<SceneStateActionPatch>(objectName, patches, fsmName), ISceneFSMPatchSet
{
    /// <inheritdoc cref="SceneStateActionPatchSet" />
    private SceneStateActionPatchSet(string sceneName, string[] objectNames, List<SceneStateActionPatch> patches, string fsmName = "")
        : this(sceneName, "", patches, fsmName)
    {
        NameArray = objectNames;
    }

    /// <inheritdoc cref="SceneStateActionPatchSet" />
    private SceneStateActionPatchSet(string[] sceneNames, string objectName, List<SceneStateActionPatch> patches, string fsmName = "")
        : this("", objectName, patches, fsmName)
    {
        SceneArray = sceneNames;
    }

    /// <inheritdoc cref="SceneStateActionPatchSet" />
    private SceneStateActionPatchSet(string[] sceneNames, string[] objectNames, List<SceneStateActionPatch> patches, string fsmName = "")
        : this("", "", patches, fsmName)
    {
        NameArray = objectNames;
        SceneArray = sceneNames;
    }

    public string SceneName { get; set; } = sceneName;

    public string[] SceneArray { get; set; } = [];

    public override object Clone()
    {
        List<SceneStateActionPatch> newList = [];

        foreach (var patch in Patches)
            newList.Add(patch);

        if(NameArray.Length > 0 && SceneArray.Length > 0)
            return new SceneStateActionPatchSet(SceneArray, NameArray, newList, FSMName);
        else if(SceneArray.Length > 0)
            return new SceneStateActionPatchSet(SceneArray, Name, newList, FSMName);
        else if (NameArray.Length > 0)
            return new SceneStateActionPatchSet(SceneName, NameArray, newList, FSMName);
        else
            return new SceneStateActionPatchSet(SceneName, Name, newList, FSMName);
    }
}

/// <inheritdoc/>
/// <param name="sceneName"> Name of <see cref="Scene" /> that the <see cref="GameObject" /> to be patched is within</param>
/// <param name="stateName"> Name of the <see cref="FsmState" /> to patch</param>
/// <param name="actionType">The <see cref="Type" /> of <see cref="FsmStateAction" /> being patched</param>
/// <param name="patch">     The patch for modifying the <see cref="FsmStateAction" /></param>
/// <param name="unpatch">   Optional: The patch for removing the <see cref="FsmStateAction" /> modifictation</param>
/// <param name="fsmName">   Optional: Name of the <see cref="PlayMakerFSM" /> to patch, the first found
/// <see cref="PlayMakerFSM" /> is used if not provided</param>
/// <param name="objectNames"><see cref="Array" /> of <see cref="GameObject" /> names to patch</param>
/// <param name="sceneNames"><see cref="Array" /> of <see cref="Scene" /> names to patch</param>
/// <param name="stateNames"><see cref="Array" /> of <see cref="FsmState" /> names to be patched</param>
public class SceneStateActionPatch(string sceneName, string objectName, string stateName, Type actionType, Action<FsmStateAction, object[]> patch, Action<FsmStateAction, object[]>? unpatch = null, string fsmName = "")
    : StateActionPatch_Base(objectName, stateName, actionType, patch, unpatch, fsmName), ISceneFSMPatch
{
    /// <inheritdoc cref="SceneStateActionPatch" />
    public SceneStateActionPatch(string sceneName, string[] objectNames, string stateName, Type actionType, Action<FsmStateAction, object[]> patch, Action<FsmStateAction, object[]>? unpatch = null, string fsmName = "")
        : this(sceneName, "", stateName, actionType, patch, unpatch, fsmName)
    {
        NameArray = objectNames;
    }

    /// <inheritdoc cref="SceneStateActionPatch" />
    public SceneStateActionPatch(string[] sceneNames, string objectName, string stateName, Type actionType, Action<FsmStateAction, object[]> patch, Action<FsmStateAction, object[]>? unpatch = null, string fsmName = "")
        : this("", objectName, stateName, actionType, patch, unpatch, fsmName)
    {
        SceneArray = sceneNames;
    }

    /// <inheritdoc cref="SceneStateActionPatch" />
    public SceneStateActionPatch(string sceneName, string objectName, string[] stateNames, Type actionType, Action<FsmStateAction, object[]> patch, Action<FsmStateAction, object[]>? unpatch = null, string fsmName = "")
        : this(sceneName, objectName, "", actionType, patch, unpatch, fsmName)
    {
        StateArray = stateNames;
    }

    /// <inheritdoc cref="SceneStateActionPatch" />
    public SceneStateActionPatch(string sceneName, string[] objectNames, string[] stateNames, Type actionType, Action<FsmStateAction, object[]> patch, Action<FsmStateAction, object[]>? unpatch = null, string fsmName = "")
        : this(sceneName, "", "", actionType, patch, unpatch, fsmName)
    {
        StateArray = stateNames;
        NameArray = objectNames;
    }

    /// <inheritdoc cref="SceneStateActionPatch" />
    public SceneStateActionPatch(string[] sceneNames, string objectName, string[] stateNames, Type actionType, Action<FsmStateAction, object[]> patch, Action<FsmStateAction, object[]>? unpatch = null, string fsmName = "")
        : this("", objectName, "", actionType, patch, unpatch, fsmName)
    {
        StateArray = stateNames;
        SceneArray = sceneNames;
    }

    /// <inheritdoc cref="SceneStateActionPatch" />
    public SceneStateActionPatch(string[] sceneNames, string[] objectNames, string stateName, Type actionType, Action<FsmStateAction, object[]> patch, Action<FsmStateAction, object[]>? unpatch = null, string fsmName = "")
        : this("", "", stateName, actionType, patch, unpatch, fsmName)
    {
        SceneArray = sceneNames;
        NameArray = objectNames;
    }

    /// <inheritdoc cref="SceneStateActionPatch" />
    public SceneStateActionPatch(string[] sceneNames, string[] objectNames, string[] stateNames, Type actionType, Action<FsmStateAction, object[]> patch, Action<FsmStateAction, object[]>? unpatch = null, string fsmName = "")
        : this("", "", "", actionType, patch, unpatch, fsmName)
    {
        StateArray = stateNames;
        SceneArray = sceneNames;
        NameArray = objectNames;
    }

    public string SceneName { get; set; } = sceneName;

    public string[] SceneArray { get; set; } = [];

    public override string[] StateArray { get; set; } = [];

    public override object Clone()
    {
        if (NameArray.Length > 0 && SceneArray.Length > 0 && StateArray.Length > 0)
            return new SceneStateActionPatch(SceneArray, NameArray, StateArray, Type, (Action<FsmStateAction, object[]>)Patch.Clone(), (Action<FsmStateAction, object[]>?)Unpatch?.Clone(), FSMName);
        else if (StateArray.Length > 0 && SceneArray.Length > 0)
            return new SceneStateActionPatch(SceneArray, Name, StateArray, Type, (Action<FsmStateAction, object[]>)Patch.Clone(), (Action<FsmStateAction, object[]>?)Unpatch?.Clone(), FSMName);
        else if (NameArray.Length > 0 && SceneArray.Length > 0)
            return new SceneStateActionPatch(SceneArray, NameArray, State, Type, (Action<FsmStateAction, object[]>)Patch.Clone(), (Action<FsmStateAction, object[]>?)Unpatch?.Clone(), FSMName);
        else if (NameArray.Length > 0 && StateArray.Length > 0)
            return new SceneStateActionPatch(SceneName, NameArray, StateArray, Type, (Action<FsmStateAction, object[]>)Patch.Clone(), (Action<FsmStateAction, object[]>?)Unpatch?.Clone(), FSMName);
        else if (NameArray.Length > 0)
            return new SceneStateActionPatch(SceneName, NameArray, State, Type, (Action<FsmStateAction, object[]>)Patch.Clone(), (Action<FsmStateAction, object[]>?)Unpatch?.Clone(), FSMName);
        else if (SceneArray.Length > 0)
            return new SceneStateActionPatch(SceneArray, Name, State, Type, (Action<FsmStateAction, object[]>)Patch.Clone(), (Action<FsmStateAction, object[]>?)Unpatch?.Clone(), FSMName);
        else if (StateArray.Length > 0)
            return new SceneStateActionPatch(SceneName, Name, StateArray, Type, (Action<FsmStateAction, object[]>)Patch.Clone(), (Action<FsmStateAction, object[]>?)Unpatch?.Clone(), FSMName);
        else
            return new SceneStateActionPatch(SceneName, Name, State, Type, (Action<FsmStateAction, object[]>)Patch.Clone(), (Action<FsmStateAction, object[]>?)Unpatch?.Clone(), FSMName);
    }
}

#endregion FSM State Action

#region FSM State
/// <inheritdoc />
/// <param name="sceneName"> Name of the <see cref="Scene" /> that the <see cref="GameObject" /> is within</param>
/// <param name="objectName">Name of the <see cref="GameObject" /> to be patched</param>
/// <param name="patches">   The set of patches for the <see cref="GameObject" /></param>
/// <param name="fsmName">   Optional: Name of the <see cref="PlayMakerFSM" /> to patch, the first found
/// <see cref="PlayMakerFSM" /> is used if not provided</param>
/// <param name="objectNames"><see cref="Array" /> of <see cref="GameObject" /> names to patch</param>
/// <param name="sceneNames"><see cref="Array" /> of <see cref="Scene" /> names to patch</param>
public class SceneStatePatchSet(string sceneName, string objectName, List<SceneStatePatch> patches, string fsmName = "")
    : StatePatchSet_Base<SceneStatePatch>(objectName, patches, fsmName), ISceneFSMPatchSet
{
    /// <inheritdoc cref="SceneStatePatchSet" />
    private SceneStatePatchSet(string sceneName, string[] objectNames, List<SceneStatePatch> patches, string fsmName = "")
        : this(sceneName, "", patches, fsmName)
    {
        NameArray = objectNames;
    }

    /// <inheritdoc cref="SceneStatePatchSet" />
    private SceneStatePatchSet(string[] sceneNames, string objectName, List<SceneStatePatch> patches, string fsmName = "")
        : this("", objectName, patches, fsmName)
    {
        SceneArray = sceneNames;
    }

    /// <inheritdoc cref="SceneStatePatchSet" />
    private SceneStatePatchSet(string[] sceneNames, string[] objectNames, List<SceneStatePatch> patches, string fsmName = "")
        : this("", "", patches, fsmName)
    {
        NameArray = objectNames;
        SceneArray = sceneNames;
    }

    public string SceneName { get; set; } = sceneName;

    public string[] SceneArray { get; set; } = [];

    public override object Clone()
    {
        List<SceneStatePatch> newList = [];

        foreach (var patch in Patches)
            newList.Add(patch);

        if (NameArray.Length > 0 && SceneArray.Length > 0)
            return new SceneStatePatchSet(SceneArray, NameArray, newList, FSMName);
        else if (SceneArray.Length > 0)
            return new SceneStatePatchSet(SceneArray, Name, newList, FSMName);
        else if (NameArray.Length > 0)
            return new SceneStatePatchSet(SceneName, NameArray, newList, FSMName);
        else
            return new SceneStatePatchSet(SceneName, Name, newList, FSMName);
    }
}

/// <inheritdoc/>
/// <param name="sceneName"> Name of the <see cref="Scene" /> that the <see cref="GameObject" /> is within</param>
/// <param name="objectName">Name of the <see cref="GameObject" /> being patched</param>
/// <param name="stateName">Name of the <see cref="FsmState" /> to patch</param>
/// <param name="patch">    The patch for adding the <see cref="FsmStateAction" /></param>
/// <param name="unpatch">  Optional: The patch for removing the <see cref="FsmStateAction" /></param>
/// <param name="fsmName">   Optional: Name of the <see cref="PlayMakerFSM" /> to patch, the first found
/// <see cref="PlayMakerFSM" /> is used if not provided</param>
/// <param name="objectNames"><see cref="Array" /> of <see cref="GameObject" /> names to patch</param>
/// <param name="sceneNames"><see cref="Array" /> of <see cref="Scene" /> names to patch</param>
/// <param name="stateNames"><see cref="Array" /> of <see cref="FsmState" /> names to be patched</param>
public class SceneStatePatch(string sceneName, string objectName, string stateName, Action<FsmState, object[]> patch, Action<FsmState, object[]>? unpatch = null, string fsmName = "")
: StatePatch_Base(objectName, stateName, patch, unpatch, fsmName), ISceneFSMPatch
{
    /// <inheritdoc cref="SceneStatePatch"/>
    public SceneStatePatch(string sceneName, string[] objectNames, string stateName, Action<FsmState, object[]> patch, Action<FsmState, object[]>? unpatch = null, string fsmName = "")
        : this(sceneName, "", stateName, patch, unpatch, fsmName)
    {
        NameArray = objectNames;
    }

    /// <inheritdoc cref="SceneStatePatch" />
    public SceneStatePatch(string[] sceneNames, string objectName, string stateName, Action<FsmState, object[]> patch, Action<FsmState, object[]>? unpatch = null, string fsmName = "")
        : this("", objectName, stateName, patch, unpatch, fsmName)
    {
        SceneArray = sceneNames;
    }

    /// <inheritdoc cref="SceneStatePatch" />
    public SceneStatePatch(string sceneName, string objectName, string[] stateNames, Action<FsmState, object[]> patch, Action<FsmState, object[]>? unpatch = null, string fsmName = "")
        : this(sceneName, objectName, "", patch, unpatch, fsmName)
    {
        StateArray = stateNames;
    }

    /// <inheritdoc cref="SceneStatePatch" />
    public SceneStatePatch(string sceneName, string[] objectNames, string[] stateNames, Action<FsmState, object[]> patch, Action<FsmState, object[]>? unpatch = null, string fsmName = "")
        : this(sceneName, "", "", patch, unpatch, fsmName)
    {
        StateArray = stateNames;
        NameArray = objectNames;
    }

    /// <inheritdoc cref="SceneStatePatch" />
    public SceneStatePatch(string[] sceneNames, string objectName, string[] stateNames, Action<FsmState, object[]> patch, Action<FsmState, object[]>? unpatch = null, string fsmName = "")
        : this("", objectName, "", patch, unpatch, fsmName)
    {
        StateArray = stateNames;
        SceneArray = sceneNames;
    }

    /// <inheritdoc cref="SceneStatePatch" />
    public SceneStatePatch(string[] sceneNames, string[] objectNames, string stateName, Action<FsmState, object[]> patch, Action<FsmState, object[]>? unpatch = null, string fsmName = "")
        : this("", "", stateName, patch, unpatch, fsmName)
    {
        SceneArray = sceneNames;
        NameArray = objectNames;
    }

    /// <inheritdoc cref="SceneStatePatch" />
    public SceneStatePatch(string[] sceneNames, string[] objectNames, string[] stateNames, Action<FsmState, object[]> patch, Action<FsmState, object[]>? unpatch = null, string fsmName = "")
        : this("", "", "", patch, unpatch, fsmName)
    {
        StateArray = stateNames;
        SceneArray = sceneNames;
        NameArray = objectNames;
    }

    public string SceneName { get; set; } = sceneName;

    public string[] SceneArray { get; set; } = [];

    public override string[] StateArray { get; set; } = [];

    public override object Clone()
    {
        if (NameArray.Length > 0 && SceneArray.Length > 0 && StateArray.Length > 0)
            return new SceneStatePatch(SceneArray, NameArray, StateArray, (Action<FsmState, object[]>)Patch.Clone(), (Action<FsmState, object[]>?)Unpatch?.Clone(), FSMName);
        else if (StateArray.Length > 0 && SceneArray.Length > 0)
            return new SceneStatePatch(SceneArray, Name, StateArray, (Action<FsmState, object[]>)Patch.Clone(), (Action<FsmState, object[]>?)Unpatch?.Clone(), FSMName);
        else if (NameArray.Length > 0 && SceneArray.Length > 0)
            return new SceneStatePatch(SceneArray, NameArray, State, (Action<FsmState, object[]>)Patch.Clone(), (Action<FsmState, object[]>?)Unpatch?.Clone(), FSMName);
        else if (NameArray.Length > 0 && StateArray.Length > 0)
            return new SceneStatePatch(SceneName, NameArray, StateArray, (Action<FsmState, object[]>)Patch.Clone(), (Action<FsmState, object[]>?)Unpatch?.Clone(), FSMName);
        else if (NameArray.Length > 0)
            return new SceneStatePatch(SceneName, NameArray, State, (Action<FsmState, object[]>)Patch.Clone(), (Action<FsmState, object[]>?)Unpatch?.Clone(), FSMName);
        else if (SceneArray.Length > 0)
            return new SceneStatePatch(SceneArray, Name, State, (Action<FsmState, object[]>)Patch.Clone(), (Action<FsmState, object[]>?)Unpatch?.Clone(), FSMName);
        else if (StateArray.Length > 0)
            return new SceneStatePatch(SceneName, Name, StateArray, (Action<FsmState, object[]>)Patch.Clone(), (Action<FsmState, object[]>?)Unpatch?.Clone(), FSMName);
        else
            return new SceneStatePatch(SceneName, Name, State, (Action<FsmState, object[]>)Patch.Clone(), (Action<FsmState, object[]>?)Unpatch?.Clone(), FSMName);
    }
}

#endregion FSM State

#region Abstraction

/// <summary>Abstract class for <see cref="Scene" /> FSM Patch Collections</summary>
/// <typeparam name="PatchType">The type of FSM Patch</typeparam>
/// <typeparam name="PatchSetType">The type of FSM Patch Set</typeparam>
/// <typeparam name="PatchTarget">The type of FSM Object we are targeting (<see cref="FsmStateAction"/>
/// or <see cref="FsmState">)</typeparam>
public abstract class SceneFSMPatchCollections_Base<PatchType, PatchSetType, PatchTarget>
    : ScenePatchCollections_Base<PatchType, PatchSetType, PlayMakerFSM, PatchTarget>
    where PatchType : FSMPatch_Base<PatchTarget>, ISceneFSMPatch
    where PatchSetType : FSMPatchSet_Base<PatchType, PatchTarget>, ISceneFSMPatchSet
{
    /// <summary>Applies the patches to the given <see cref="Scene" /></summary>
    /// <param name="scene">  The <see cref="Scene" /> to be patched</param>
    /// <param name="objects">The <see cref="GameObject" />s within the <see cref="Scene" /></param>
    public override bool ApplyPatches(Scene scene, HashSet<GameObject> objects)
    {
        if (Patches.TryGetValue(scene.name, out Dictionary<string, HashSet<PatchType>> patches))
        {
            foreach (var patchGroup in patches)
            {
                foreach (var setPatch in patchGroup.Value)
                {
                    var filteredObjects = objects.Where(obj => CuteRandoCore.CullName(obj.name).Equals(patchGroup.Key)).ToArray();

                    foreach (var obj in filteredObjects)
                        setPatch.ApplyPatch(obj.GetComponents<PlayMakerFSM>(), [obj, patchGroup.Key, scene, scene.name]);
                }
            }

            return true;
        }

        return false;
    }

    /// <summary>Removes the patches applied to the given <see cref="Scene" /></summary>
    /// <param name="scene">  The <see cref="Scene" /> that the patches were applied to</param>
    /// <param name="objects">The <see cref="GameObject" />s within the <see cref="Scene" /></param>
    public override bool RemovePatches(Scene scene, HashSet<GameObject> objects)
    {
        if (Patches.TryGetValue(scene.name, out Dictionary<string, HashSet<PatchType>> patches))
        {
            foreach (var patchGroup in patches)
            {
                foreach (var setPatch in patchGroup.Value)
                {
                    var filteredObjects = objects.Where(obj => CuteRandoCore.CullName(obj.name).Equals(patchGroup.Key)).ToArray();

                    foreach (var obj in filteredObjects)
                        setPatch.RemovePatch(obj.GetComponents<PlayMakerFSM>(), [obj, patchGroup.Key, scene, scene.name]);
                }
            }

            return true;
        }

        return false;
    }
}

/// <summary>Abstract class for <see cref="Scene" /> Patch Collections</summary>
/// <typeparam name="PatchType">The type of Patch</typeparam>
/// <typeparam name="PatchSetType">The type of Patch Set</typeparam>
/// <typeparam name="ObjectType">The object we are targeting</typeparam>
/// <typeparam name="PatchTarget">The type of Object we are targeting
public abstract class ScenePatchCollections_Base<PatchType, PatchSetType, ObjectType, PatchTarget>
    : ObjectPatchCollection_Base<PatchType, PatchSetType, ObjectType, PatchTarget>
    where PatchType : ObjectPatch_Base<PatchTarget, ObjectType>, IScenePatch
    where PatchSetType : ObjectPatchSet_Base<PatchType, ObjectType, PatchTarget>, IScenePatchSet
{
    private readonly Dictionary<string, Dictionary<string, int>> patchedObjects = [];

    /// <summary>
    /// Dictionary containing the names of <see cref="GameObject" />s within scenes to patch and how many patches are to be applied to them
    /// </summary>
    public new Dictionary<string, Dictionary<string, int>> PatchedObjects => patchedObjects;

    /// <summary>The collection of patches that this contains</summary>
    public new Dictionary<string, Dictionary<string, HashSet<PatchType>>> Patches { get; } = [];

    /// <summary>Applies the patches to the given <see cref="Scene" /></summary>
    /// <param name="scene">  The <see cref="Scene" /> to be patched</param>
    /// <param name="objects">The <see cref="GameObject" />s within the <see cref="Scene" /></param>
    public abstract bool ApplyPatches(Scene scene, HashSet<GameObject> objects);

    /// <summary>Removes the patches applied to the given <see cref="Scene" /></summary>
    /// <param name="scene">  The <see cref="Scene" /> that the patches were applied to</param>
    /// <param name="objects">The <see cref="GameObject" />s within the <see cref="Scene" /></param>
    public abstract bool RemovePatches(Scene scene, HashSet<GameObject> objects);

    /// <summary>Register a collection of patches</summary>
    /// <param name="patchCollection">The dictionary containing the patches to be applied to <see cref="GameObject" />s</param>
    /// <returns>The number of patches registered</returns>
    public int RegisterPatchCollection(Dictionary<string, Dictionary<string, PatchType>> patchCollection)
    {
        int patchTotal = 0;

        foreach (var sceneGroup in patchCollection)
        {
            foreach (var patchSet in sceneGroup.Value)
            {
                patchTotal += RegisterPatchInCollection(patchSet.Key, patchSet.Value, sceneGroup.Key);
            }
        }

        return patchTotal;
    }

    /// <summary>Register a collection of patches</summary>
    /// <param name="sceneName">      Name of the <see cref="Scene" /> that the <see cref="GameObject" /> is within</param>
    /// <param name="objectName">     Name of the <see cref="GameObject" /></param>
    /// <param name="patchCollection">List containing patches to apply</param>
    /// <returns>The number of patches registered</returns>
    public int RegisterPatchCollection(List<PatchType> patchCollection, string objectName, string sceneName)
    {
        int patchTotal = 0;

        foreach (var patch in patchCollection)
        {
            string name = patch.Name is "" ?
                                patch.NameArray.Length > 0 ?
                                    ""
                                    : objectName
                                : patch.Name;

            string scene = patch.SceneName is "" ?
                                patch.SceneArray.Length > 0 ?
                                    ""
                                    : objectName
                                : patch.SceneName;

            patchTotal += RegisterPatchInCollection(name, patch, scene);
        }

        return patchTotal;
    }

    /// <summary>Register a patch for an <see cref="GameObject" /> in a <see cref="Scene" /></summary>
    /// <param name="sceneName"> Name of the <see cref="Scene" /> that the <see cref="GameObject" /> is within</param>
    /// <param name="objectName">Name of the <see cref="GameObject" /></param>
    /// <param name="smolPatch"> Patch to apply</param>
    /// <returns>The number of patches registered</returns>
    public int RegisterPatchInCollection(string objectName, ISmolPatch smolPatch, string sceneName)
    {
        int patchTotal = 0;

        if (smolPatch is PatchType patch)
        {
            if(sceneName is "")
            {
                if(patch.SceneArray.Length > 0)
                {
                    foreach (string scene in patch.SceneArray)
                        patchTotal += RegisterPatchInCollection(objectName, smolPatch, scene);

                    return patchTotal;
                }
                else
                    CuteRandoCore.DebugLog($"Register Scene patch called with blank scene and empty scene array; Name: {objectName}, Type: {smolPatch.GetType()}", DebugLogOptions.Error, true);
            }
            if (objectName is "")
            {
                if (patch.NameArray.Length > 0)
                {
                    foreach (string name in patch.NameArray)
                        patchTotal += RegisterPatchInCollection(name, smolPatch, sceneName);

                    return patchTotal;
                }
                else
                    CuteRandoCore.DebugLog($"Register Scene patch called with blank name and empty name array; Scene: {sceneName}, Type: {smolPatch.GetType()}", DebugLogOptions.Error, true);
            }
            if (Patches.ContainsKey(sceneName))
            {
                if (Patches[sceneName].ContainsKey(objectName))
                {
                    if (!Patches[sceneName][objectName].Add(patch))
                        return patchTotal; // The patch is already in there
                }
                else
                    Patches[sceneName][objectName] = new() { { patch } };
            }
            else
                Patches[sceneName] = new() { { objectName, [patch] } };

            if (PatchedObjects.ContainsKey(sceneName))
            {
                if (PatchedObjects[sceneName].ContainsKey(objectName))
                {
                    PatchedObjects[sceneName][objectName]++;
                }
                else
                    PatchedObjects[sceneName].Add(objectName, 1);
            }
            else
                PatchedObjects[sceneName] = new() { { objectName, 1 } };
            patchTotal++;
        }
        else if (smolPatch is PatchSetType patchSet)
        {
            if (sceneName is "")
            {
                if (patchSet.SceneArray.Length > 0)
                {
                    foreach (string scene in patchSet.SceneArray)
                        patchTotal += RegisterPatchCollection(patchSet.Patches, objectName, scene);

                    return patchTotal;
                }
                else
                    CuteRandoCore.DebugLog($"Register Scene patch called with blank scene and empty scene array; Name: {objectName}, Type: {smolPatch.GetType()}", DebugLogOptions.Error, true);
            }
            else if(objectName is "")
            {
                if (patchSet.NameArray.Length > 0)
                {
                    foreach (string name in patchSet.NameArray)
                        patchTotal += RegisterPatchCollection(patchSet.Patches, name, sceneName);

                    return patchTotal;
                }
                else
                    CuteRandoCore.DebugLog($"Register Scene patch called with blank name and empty name array; Scene: {sceneName}, Type: {smolPatch.GetType()}", DebugLogOptions.Error, true);
            }
            else
                patchTotal += RegisterPatchCollection(patchSet.Patches, objectName, sceneName);
        }
        else
            CuteRandoCore.DebugLog($"Register patch called with invalid patch type, Name: {smolPatch.Name}, Type: {smolPatch.GetType()}", DebugLogOptions.Error, true);

        return patchTotal;
    }

    /// <summary>Unregister a collection of patches</summary>
    /// <param name="patchCollection">The dictionary containing the patches to be removed from <see cref="GameObject" />s</param>
    /// <returns>The number of patches unregistered</returns>
    public int UnregisterPatchCollection(Dictionary<string, Dictionary<string, PatchType>> patchCollection)
    {
        int patchTotal = 0;

        foreach (var sceneGroup in patchCollection)
        {
            foreach (var patchSet in sceneGroup.Value)
            {
                patchTotal += UnregisterPatchInCollection(patchSet.Key, patchSet.Value, sceneGroup.Key);
            }
        }

        return patchTotal;
    }

    /// <summary>Unregister a collection of patches</summary>
    /// <param name="sceneName">      Name of the <see cref="Scene" /> that the <see cref="GameObject" /> is within</param>
    /// <param name="objectName">     Name of the <see cref="GameObject" /></param>
    /// <param name="patchCollection">List containing patches to remove</param>
    /// <returns>The number of patches unregistered</returns>
    public int UnregisterPatchCollection(List<PatchType> patchCollection, string objectName, string sceneName)
    {
        int patchTotal = 0;

        foreach (var patch in patchCollection)
        {
            string name = patch.Name is "" ?
                                patch.NameArray.Length > 0 ?
                                    ""
                                    : objectName
                                : patch.Name;

            string scene = patch.SceneName is "" ?
                                patch.SceneArray.Length > 0 ?
                                    ""
                                    : objectName
                                : patch.SceneName;

            patchTotal += UnregisterPatchInCollection(name, patch, scene);
        }

        return patchTotal;
    }

    /// <summary>Unregister a patch for an object in a <see cref="Scene" /></summary>
    /// <param name="sceneName"> Name of the <see cref="Scene" /> that the <see cref="GameObject" /> is within</param>
    /// <param name="objectName">Name of the <see cref="GameObject" /></param>
    /// <param name="smolPatch"> Patch to remove</param>
    /// <returns>The number of patches unregistered</returns>
    public int UnregisterPatchInCollection(string objectName, ISmolPatch smolPatch, string sceneName)
    {
        int unpatchTotal = 0;

        if (smolPatch is PatchType patch)
        {
            if (sceneName is "")
            {
                if (patch.SceneArray.Length > 0)
                {
                    foreach (string scene in patch.SceneArray)
                        unpatchTotal += UnregisterPatchInCollection(objectName, smolPatch, scene);

                    return unpatchTotal;
                }
                else
                    CuteRandoCore.DebugLog($"Unregister Scene patch called with blank scene and empty scene array; Name: {objectName}, Type: {smolPatch.GetType()}", DebugLogOptions.Error, true);
            }
            if (objectName is "")
            {
                if (patch.NameArray.Length > 0)
                {
                    foreach (string name in patch.NameArray)
                        unpatchTotal += UnregisterPatchInCollection(name, smolPatch, sceneName);

                    return unpatchTotal;
                }
                else
                    CuteRandoCore.DebugLog($"Unregister Scene patch called with blank name and empty name array; Scene: {sceneName}, Type: {smolPatch.GetType()}", DebugLogOptions.Error, true);
            }
            if (Patches.ContainsKey(sceneName))
            {
                if (Patches[sceneName][objectName].Remove(patch))
                {
                    if (PatchedObjects[sceneName][objectName] == 1)
                        PatchedObjects[sceneName].Remove(objectName);
                    else
                        PatchedObjects[sceneName][objectName]--;
                }
                if (PatchedObjects[sceneName].Count == 0)
                    PatchedObjects.Remove(sceneName);
                unpatchTotal++;
            }
        }
        else if (smolPatch is PatchSetType patchSet)
        {
            if (sceneName is "")
            {
                if (patchSet.SceneArray.Length > 0)
                {
                    foreach (string scene in patchSet.SceneArray)
                        unpatchTotal += UnregisterPatchCollection(patchSet.Patches, objectName, scene);

                    return unpatchTotal;
                }
                else
                    CuteRandoCore.DebugLog($"Unregister Scene patch called with blank scene and empty scene array; Name: {objectName}, Type: {smolPatch.GetType()}", DebugLogOptions.Error, true);
            }
            else if (objectName is "")
            {
                if (patchSet.NameArray.Length > 0)
                {
                    foreach (string name in patchSet.NameArray)
                        unpatchTotal += UnregisterPatchCollection(patchSet.Patches, name, sceneName);

                    return unpatchTotal;
                }
                else
                    CuteRandoCore.DebugLog($"Unregister Scene patch called with blank name and empty name array; Scene: {sceneName}, Type: {smolPatch.GetType()}", DebugLogOptions.Error, true);
            }
            else
                unpatchTotal += UnregisterPatchCollection(patchSet.Patches, objectName, sceneName);
        }
        else
            CuteRandoCore.DebugLog($"Unregister patch called with invalid patch type, Name: {smolPatch.Name}, Type: {smolPatch.GetType()}", DebugLogOptions.Error, true);

        return unpatchTotal;
    }

    #region Pragma Disable
#pragma warning disable CS0809 // Obsolete member overrides non-obsolete member
    [Obsolete("Scene Collection requires using Dictionary or scene+object+list to register")]

    public override int RegisterPatchCollection(List<PatchType> patchCollection, string objectName)
        => 0;

    [Obsolete("Scene Collection requires using scene+object+patch to register")]
    public override int RegisterPatchInCollection(string objectName, ISmolPatch smolPatch)
        => 0;
    [Obsolete("Scene Collection requires using Dictionary or scene+object+list to unregister")]
    public override int UnregisterPatchCollection(List<PatchType> patchCollection, string objectName)
        => 0;

    [Obsolete("Scene Collection requires using scene+object+patch to unregister")]
    public override int UnregisterPatchInCollection(string objectName, ISmolPatch smolPatch)
        => 0;
#pragma warning restore CS0809 // Obsolete member overrides non-obsolete member
    #endregion
}

#endregion Abstraction

/// <summary>Marks a patch as a Scene patch</summary>
public interface IScenePatch : ISmolPatch
{
    /// <summary>Name of the <see cref="Scene" /> the patch is for</summary>
    public string SceneName { get; }

    /// <summary><see cref="Array"/> of <see cref="Scene"/> names the patch is for</summary>
    public string[] SceneArray { get; }
}

/// <summary>Marks a patch as a Scene patch set</summary>
public interface IScenePatchSet : IScenePatch;

/// <summary>Marks a patch as a Scene FSM Patch</summary>
public interface ISceneFSMPatch : IFSMPatch, IScenePatch;

/// <summary>Marks a patch set as a Scene FSM Patch Set, includes the interface to additionally mark it as a patch</summary>
public interface ISceneFSMPatchSet : ISceneFSMPatch, IFSMPatchSet, IScenePatchSet;
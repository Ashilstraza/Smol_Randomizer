using HutongGames.PlayMaker;

using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using UnityEngine.SceneManagement;

namespace Smol_Randomizer.Patchers.ScenePatches;

/// <summary>Handles patching <see cref="Scene" /> <see cref="PlayMakerFSM" />s</summary>
internal class SceneFSMPatches
{
    private static readonly Lazy<SceneFSMPatches> instance = new(() => new SceneFSMPatches());

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
        foreach (var patchGroup in scenePatchCollection)
        {
            if (patchGroup is SceneStateActionPatch or SceneStateActionPatchSet)
                sceneStateActionCollection.RegisterPatchInCollection(patchGroup.SceneName, patchGroup.ObjectName, patchGroup);
            else if (patchGroup is SceneStatePatch or SceneStatePatchSet)
                sceneStateCollection.RegisterPatchInCollection(patchGroup.SceneName, patchGroup.ObjectName, patchGroup);
            else
                CuteRandoCore.Log.LogWarning($"Register patch called with invalid patch type; Name: {patchGroup.Name}, Scene Name: {patchGroup.SceneName}, Object Name: {patchGroup.ObjectName}, Type: {patchGroup.GetType()}");
        }
    }

    /// <summary>Unregisters a collection of patches</summary>
    /// <param name="scenePatchCollection">The collection of patches to remove</param>
    public static void UnregisterPatchCollection(List<ISceneFSMPatch> scenePatchCollection)
    {
        foreach (var patchGroup in scenePatchCollection)
        {
            if (patchGroup is SceneStateActionPatch or SceneStateActionPatchSet)
                sceneStateActionCollection.UnregisterPatchInCollection(patchGroup.SceneName, patchGroup.ObjectName, patchGroup);
            else if (patchGroup is SceneStatePatch or SceneStatePatchSet)
                sceneStateCollection.UnregisterPatchInCollection(patchGroup.SceneName, patchGroup.ObjectName, patchGroup);
            else
                CuteRandoCore.Log.LogWarning($"Unregister patch called with invalid patch type; Name: {patchGroup.Name}, Scene Name: {patchGroup.SceneName}, Object Name: {patchGroup.ObjectName}, Type: {patchGroup.GetType()}");
        }
    }

    /// <summary>Registers a patch and overwrites any existing ones with the new one</summary>
    /// <param name="patch">The patch to add</param>
    /// <returns>If the patch was added</returns>
    public static bool OverwritePatch(ISceneFSMPatch patch)
    {
        if (patch is SceneStateActionPatch actionPatch)
        {
            sceneStateActionCollection.Patches.TryGetValue(actionPatch.SceneName, out var scenePatchSet);
            scenePatchSet.TryGetValue(actionPatch.ObjectName, out var patchSet);

            var oldPatch = patchSet.Where(p => p.FSMName.Equals(actionPatch.FSMName)
                                                && p.Name.Equals(actionPatch.Name)
                                                && p.TypeString.Equals(actionPatch.TypeString)).FirstOrDefault();
            if (oldPatch == null)
            {
                CuteRandoCore.Log.LogWarning($"Overwrite patch failed, no matching patch; Name: {patch.Name}, Scene Name: {patch.SceneName}, Object Name: {patch.ObjectName}, Type: {patch.GetType()}");
            }
            else
            {
                patchSet.Remove(oldPatch);
                patchSet.Add(actionPatch);
                return true;
            }
        }
        else if (patch is SceneStatePatch statePatch)
        {
            sceneStateCollection.Patches.TryGetValue(statePatch.SceneName, out var scenePatchSet);
            scenePatchSet.TryGetValue(statePatch.ObjectName, out var patchSet);

            var oldPatch = patchSet.Where(p => p.FSMName.Equals(statePatch.FSMName)
                                                && p.Name.Equals(statePatch.Name)).FirstOrDefault();
            if (oldPatch == null)
            {
                CuteRandoCore.Log.LogWarning($"Overwrite patch failed, no matching patch; Name; Name: {patch.Name}, Scene Name: {patch.SceneName}, Object Name: {patch.ObjectName}, Type: {patch.GetType()}");
            }
            else
            {
                patchSet.Remove(oldPatch);
                patchSet.Add(statePatch);
                return true;
            }
        }
        else
            CuteRandoCore.Log.LogWarning($"Overwrite patch called with invalid patch type; Name: {patch.Name}, Scene Name: {patch.SceneName}, Object Name: {patch.ObjectName}, Type: {patch.GetType()}");
        return false;
    }
}

#region FSM State Action

/// <summary>Class for containing a set of <see cref="SceneStateActionPatch" />es</summary>
public class SceneStateActionCollection : SceneFSMPatchCollections_Base<SceneStateActionPatch, SceneStateActionPatchSet, FsmStateAction>;

/// <inheritdoc />
/// <param name="sceneName"> Name of the <see cref="Scene" /> that the <see cref="GameObject" /> is within</param>
/// <param name="objectName">Name of the <see cref="GameObject" /> to be patched</param>
/// <param name="patches">   The set of patches for the <see cref="GameObject" /></param>
/// <param name="fsmName">   Optional: Name of the <see cref="PlayMakerFSM" /> to patch, the first found
/// <see cref="PlayMakerFSM" /> is used if not provided</param>
public class SceneStateActionPatchSet(string sceneName, string objectName, List<SceneStateActionPatch> patches, string fsmName = "")
    : StateActionPatchSet_Base<SceneStateActionPatch>(objectName, patches, fsmName), ISceneFSMPatchSet
{
    /// <inheritdoc cref="SceneStateActionPatchSet" />
    /// <param name="objectNames">Array of names for <see cref="GameObject" /> s to be patched</param>
    private SceneStateActionPatchSet(string sceneName, string[] objectNames, List<SceneStateActionPatch> patches, string fsmName = "")
        : this(sceneName, "", patches, fsmName)
    {
        NameArray = objectNames;
    }

    public string SceneName => sceneName;

    public string ObjectName { get; } = objectName;

    public override object Clone()
    {
        List<SceneStateActionPatch> newList = [];

        foreach (var patch in Patches)
            newList.Add(patch);

        if (NameArray.Length > 0)
            return new SceneStateActionPatchSet(SceneName, NameArray, newList, FSMName);
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
public class SceneStateActionPatch(string sceneName, string objectName, string stateName, Type actionType, Action<FsmStateAction, object[]> patch, Action<FsmStateAction, object[]>? unpatch = null, string fsmName = "")
    : StateActionPatch_Base(stateName, actionType, patch, unpatch, fsmName), ISceneFSMPatch
{
    /// <inheritdoc cref="SceneStateActionPatch" />
    /// <param name="stateNames">Array of names for the <see cref="FsmState" />s to be patched</param>
    public SceneStateActionPatch(string sceneName, string objectName, string[] stateNames, Type actionType, Action<FsmStateAction, object[]> patch, Action<FsmStateAction, object[]>? unpatch = null, string fsmName = "")
        : this(sceneName, objectName, "", actionType, patch, unpatch, fsmName)
    {
        NameArray = stateNames;
    }

    public string SceneName => sceneName;

    public string ObjectName => objectName;

    public override object Clone()
    {
        if (NameArray.Length > 0)
            return new SceneStateActionPatch(SceneName, ObjectName, NameArray, Type, (Action<FsmStateAction, object[]>)Patch.Clone(), (Action<FsmStateAction, object[]>?)Unpatch?.Clone(), FSMName);
        return new SceneStateActionPatch(SceneName, ObjectName, Name, Type, (Action<FsmStateAction, object[]>)Patch.Clone(), (Action<FsmStateAction, object[]>?)Unpatch?.Clone(), FSMName);
    }
}

#endregion FSM State Action

#region FSM State

/// <summary>Class for containing a set of <see cref="SceneStatePatch" /> es</summary>
public class SceneStateCollection : SceneFSMPatchCollections_Base<SceneStatePatch, SceneStatePatchSet, FsmState>;

/// <inheritdoc />
/// <param name="sceneName"> Name of the <see cref="Scene" /> that the <see cref="GameObject" /> is within</param>
/// <param name="objectName">Name of the <see cref="GameObject" /> to be patched</param>
/// <param name="patches">   The set of patches for the <see cref="GameObject" /></param>
/// <param name="fsmName">   Optional: Name of the <see cref="PlayMakerFSM" /> to patch, the first found
/// <see cref="PlayMakerFSM" /> is used if not provided</param>
public class SceneStatePatchSet(string sceneName, string objectName, List<SceneStatePatch> patches, string fsmName = "")
    : StatePatchSet_Base<SceneStatePatch>(objectName, patches, fsmName), ISceneFSMPatchSet
{
    /// <inheritdoc cref="SceneStatePatchSet" />
    /// <param name="objectNames">Array of names for <see cref="GameObject" /> s to be patched</param>
    private SceneStatePatchSet(string sceneName, string[] objectNames, List<SceneStatePatch> patches, string fsmName = "")
        : this(sceneName, "", patches, fsmName)
    {
        NameArray = objectNames;
    }

    public string SceneName => sceneName;

    public string ObjectName { get; } = objectName;

    public override object Clone()
    {
        List<SceneStatePatch> newList = [];

        foreach (var patch in Patches)
            newList.Add(patch);

        if (NameArray.Length > 0)
            return new SceneStatePatchSet(SceneName, NameArray, newList, FSMName);
        return new SceneStatePatchSet(SceneName, Name, newList, FSMName);
    }
}

/// <inheritdoc/>
/// <param name="sceneName"> Name of the <see cref="Scene" /> that the <see cref="GameObject" /> is within</param>
/// <param name="stateName">Name of the <see cref="FsmState" /> to patch</param>
/// <param name="patch">    The patch for adding the <see cref="FsmStateAction" /></param>
/// <param name="unpatch">  Optional: The patch for removing the <see cref="FsmStateAction" /></param>
/// <param name="fsmName">   Optional: Name of the <see cref="PlayMakerFSM" /> to patch, the first found
/// <see cref="PlayMakerFSM" /> is used if not provided</param>
public class SceneStatePatch(string sceneName, string objectName, string stateName, Action<FsmState, object[]> patch, Action<FsmState, object[]>? unpatch = null, string fsmName = "")
: StatePatch_Base(stateName, patch, unpatch, fsmName), ISceneFSMPatch
{
    /// <inheritdoc cref="SceneStatePatch"/>
    /// <param name="stateNames">Array of names for the <see cref="FsmState" />s to be patched</param>
    public SceneStatePatch(string sceneName, string objectName, string[] stateNames, Action<FsmState, object[]> patch, Action<FsmState, object[]>? unpatch = null, string fsmName = "")
        : this(sceneName, objectName, "", patch, unpatch, fsmName)
    {
        NameArray = stateNames;
    }

    public string SceneName { get; } = sceneName;

    /// <summary>Name of the patch target</summary>
    public string ObjectName { get; } = objectName;

    public override object Clone()
    {
        if (NameArray.Length > 0)
            return new SceneStatePatch(SceneName, ObjectName, NameArray, (Action<FsmState, object[]>)Patch.Clone(), (Action<FsmState, object[]>?)Unpatch?.Clone(), FSMName);
        return new SceneStatePatch(SceneName, ObjectName, Name, (Action<FsmState, object[]>)Patch.Clone(), (Action<FsmState, object[]>?)Unpatch?.Clone(), FSMName);
    }
}

#endregion FSM State

#region Abstraction

/// <summary>Abstract class for <see cref="Scene" /> FSM Patche Collections</summary>
/// <typeparam name="PatchType">The type of FSM Patch</typeparam>
/// <typeparam name="PatchSetType">The type of FSM Patch Set</typeparam>
/// <typeparam name="PatchTarget">The type of FSM Object we are targeting (<see cref="FsmStateAction"/>
/// or <see cref="FsmState">)</typeparam>
public abstract class SceneFSMPatchCollections_Base<PatchType, PatchSetType, PatchTarget>
    : ObjectPatchCollection_Base<PatchType, PatchSetType, PlayMakerFSM, PatchTarget>
    where PatchType : FSMPatch_Base<PatchTarget>
    where PatchSetType : FSMPatchSet_Base<PatchType, PatchTarget>
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
    public void ApplyPatches(Scene scene, HashSet<GameObject> objects)
    {
        if (Patches.TryGetValue(scene.name, out Dictionary<string, HashSet<PatchType>> patches))
        {
            foreach (var patchGroup in patches)
            {
                foreach (var setPatch in patchGroup.Value)
                    setPatch.ApplyPatch(objects.FirstOrDefault(obj => obj.name.Equals(patchGroup.Key)).GetComponents<PlayMakerFSM>(), []);
            }
        }
    }

    /// <summary>Removes the patches applied to the given <see cref="Scene" /></summary>
    /// <param name="scene">  The <see cref="Scene" /> that the patches were applied to</param>
    /// <param name="objects">The <see cref="GameObject" />s within the <see cref="Scene" /></param>
    public void RemovePatches(Scene scene, HashSet<GameObject> objects)
    {
        if (Patches.TryGetValue(scene.name, out Dictionary<string, HashSet<PatchType>> patches))
        {
            foreach (var patchGroup in patches)
            {
                foreach (var setPatch in patchGroup.Value)
                    setPatch.RemovePatch(objects.FirstOrDefault(obj => obj.name.Equals(patchGroup.Key)).GetComponents<PlayMakerFSM>(), []);
            }
        }
    }

    /// <summary>Register a collection of patches</summary>
    /// <param name="patchCollection">The dictionary containing the patches to be applied to <see cref="GameObject" />s</param>
    public virtual void RegisterPatchCollection(Dictionary<string, Dictionary<string, PatchType>> patchCollection)
    {
        foreach (var patchGroup in patchCollection)
        {
            foreach (var patchSet in patchGroup.Value)
            {
                RegisterPatchInCollection(patchGroup.Key, patchSet.Key, patchSet.Value);
            }
        }
    }

    /// <summary>Register a collection of patches</summary>
    /// <param name="sceneName">      Name of the <see cref="Scene" /> that the <see cref="GameObject" /> is within</param>
    /// <param name="objectName">     Name of the <see cref="GameObject" /></param>
    /// <param name="patchCollection">List containing patches to apply</param>
    public virtual void RegisterPatchCollection(string sceneName, string objectName, List<PatchType> patchCollection)
    {
        foreach (var patch in patchCollection)
        {
            RegisterPatchInCollection(sceneName, objectName, patch);
        }
    }

    /// <summary>Register a patch for an <see cref="GameObject" /> in a <see cref="Scene" /></summary>
    /// <param name="sceneName"> Name of the <see cref="Scene" /> that the <see cref="GameObject" /> is within</param>
    /// <param name="objectName">Name of the <see cref="GameObject" /></param>
    /// <param name="smolPatch"> Patch to apply</param>
    public virtual void RegisterPatchInCollection(string sceneName, string objectName, ISmolPatch smolPatch)
    {
        if (smolPatch is PatchType patch)
        {
            if (Patches.ContainsKey(sceneName))
            {
                if (Patches[sceneName].ContainsKey(objectName))
                {
                    if (!Patches[sceneName][objectName].Add(patch))
                        return; // The patch is already in there
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
        }
        else if (smolPatch is PatchSetType patchSet)
        {
            RegisterPatchCollection(sceneName, objectName, patchSet.Patches);
        }
        else
            CuteRandoCore.Log.LogWarning($"Register patch called with invalid patch type, Name: {smolPatch.Name}, Type: {smolPatch.GetType()}");
    }

    /// <summary>Unregister a collection of patches</summary>
    /// <param name="patchCollection">The dictionary containing the patches to be removed from <see cref="GameObject" />s</param>
    public virtual void UnregisterPatchCollection(Dictionary<string, Dictionary<string, PatchType>> patchCollection)
    {
        foreach (var patchGroup in patchCollection)
        {
            foreach (var patchSet in patchGroup.Value)
            {
                UnregisterPatchInCollection(patchGroup.Key, patchSet.Key, patchSet.Value);
            }
        }
    }

    /// <summary>Unregister a collection of patches</summary>
    /// <param name="sceneName">      Name of the <see cref="Scene" /> that the <see cref="GameObject" /> is within</param>
    /// <param name="objectName">     Name of the <see cref="GameObject" /></param>
    /// <param name="patchCollection">List containing patches to remove</param>
    public virtual void UnregisterPatchCollection(string sceneName, string objectName, List<PatchType> patchCollection)
    {
        foreach (var patch in patchCollection)
        {
            UnregisterPatchInCollection(sceneName, objectName, patch);
        }
    }

    /// <summary>Unregister a patch for an object in a <see cref="Scene" /></summary>
    /// <param name="sceneName"> Name of the <see cref="Scene" /> that the <see cref="GameObject" /> is within</param>
    /// <param name="objectName">Name of the <see cref="GameObject" /></param>
    /// <param name="smolPatch"> Patch to remove</param>
    public virtual void UnregisterPatchInCollection(string sceneName, string objectName, ISmolPatch smolPatch)
    {
        if (smolPatch is PatchType patch)
        {
            if (Patches.ContainsKey(sceneName))
            {
                if (Patches[sceneName].ContainsKey(objectName))
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
                }
            }
        }
        else if (smolPatch is PatchSetType patchSet)
        {
            RegisterPatchCollection(sceneName, objectName, patchSet.Patches);
        }
        else
            CuteRandoCore.Log.LogWarning($"Register patch called with invalid patch {smolPatch.Name}");
    }

    #region Overridden Register/Unregister Methods

#pragma warning disable CS0809 // Obsolete member overrides non-obsolete member

    [Obsolete("Class Requires Dictionary{string, Dictionary{string, PatchType}} for Scene Detection", true)]
    public override void RegisterPatchCollection(List<PatchType> patchCollection) { }

    [Obsolete("Class Requires Dictionary{string, Dictionary{string, ISmolPatch}} for Scene Detection", true)]
    public override void RegisterPatchInCollection(string objectName, ISmolPatch smolPatch) { }

    [Obsolete("Class Requires Dictionary{string, Dictionary{string, PatchType}} for Scene Detection", true)]
    public override void UnregisterPatchCollection(List<PatchType> patchCollection) { }

    [Obsolete("Class Requires Dictionary{string, Dictionary{string, ISmolPatch}} for Scene Detection", true)]
    public override void UnregisterPatchInCollection(string objectName, ISmolPatch smolPatch) { }

#pragma warning restore CS0809 // Obsolete member overrides non-obsolete member
    #endregion Overridden Register/Unregister Methods
}

#endregion Abstraction

/// <summary>Marks a patch as a Scene patch</summary>
public interface IScenePatch
{
    /// <summary>Name of the <see cref="Scene" /> the patch is for</summary>
    public string SceneName { get; }

    /// <summary>Name of the patch target</summary>
    public string ObjectName { get; }
}

/// <summary>Marks a patch as a Scene FSM Patch</summary>
public interface ISceneFSMPatch : IFSMPatch, ISmolPatch, IScenePatch;

/// <summary>Marks a patch set as a Scene FSM Patch Set, includes the interface to additionally mark it as a patch</summary>
public interface ISceneFSMPatchSet : ISceneFSMPatch, ISmolPatchSet;
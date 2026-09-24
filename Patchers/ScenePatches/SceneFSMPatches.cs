using System;
using System.Collections.Generic;
using System.Linq;

using HutongGames.PlayMaker;

using UnityEngine;

namespace Smol_Randomizer.Patchers.Scene;

/// <summary>Handles patching Scene FSMs</summary>
internal class SceneFSMPatches
{
    private static readonly Lazy<SceneFSMPatches> instance = new(() => new SceneFSMPatches());

    /// <summary>A set of Scene FSM Patches</summary>
    public static SceneFSMPatches Instance => instance.Value;

    /// <summary>A set of Scene State Action patches</summary>
    public static readonly SceneStateActionCollection sceneStateActionCollection = new();

    /// <summary>As set if Scene State Patches</summary>
    public static readonly SceneStateCollection sceneStateCollection = new();

    /// <summary>Apply the various scene patches</summary>
    /// <param name="scene">The scene to patch</param>
    internal static void ApplyPatches(UnityEngine.SceneManagement.Scene scene)
    {
        HashSet<GameObject> objects = scene.GetRootGameObjects().ToHashSet();

        sceneStateActionCollection.ApplyPatches(scene, objects);
        sceneStateCollection.ApplyPatches(scene, objects);
    }

    /// <summary>Remove the various scene patches</summary>
    /// <param name="scene">The scene to unpatch</param>
    internal static void RemovePatches(UnityEngine.SceneManagement.Scene scene)
    {
        HashSet<GameObject> objects = scene.GetRootGameObjects().ToHashSet();

        sceneStateActionCollection.RemovePatches(scene, objects);
        sceneStateCollection.RemovePatches(scene, objects);
    }

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

    /// <summary>Registers a collection of patches that are a mix of State and StateActions</summary>
    /// <param name="scenePatchCollection"></param>
    public static void RegisterPatchCollection(HashSet<ISceneFSMPatch> scenePatchCollection)
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

    /// <summary>Unregisters a collection of patches that are a mix of State and StateActions</summary>
    /// <param name="scenePatchCollection"></param>
    public static void UnregisterPatchCollection(HashSet<ISceneFSMPatch> scenePatchCollection)
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
}

#region FSM State Action

/// <summary>Class for containing a set of Scene State Action patches</summary>
public class SceneStateActionCollection : SceneFSMPatchCollections_Base<SceneStateActionPatch, SceneStateActionPatchSet, FsmStateAction>;

/// <summary>Patch set of Scene State Actions</summary>
/// ///
/// <param name="sceneName"> Name of scene that the object is within</param>
/// <param name="objectName">Name of the object to be patched</param>
/// <param name="patches">   The set of patches for the object</param>
public class SceneStateActionPatchSet(string sceneName, string objectName, List<SceneStateActionPatch> patches, string fsmName = "")
    : StateActionPatchSet_Base<SceneStateActionPatch>(objectName, patches, fsmName), ISceneFSMPatchSet
{
    /// <summary>Patch set of Scene State Actions</summary>
    /// ///
    /// <param name="sceneName">  Name of scene that the objects are within</param>
    /// <param name="objectNames">Array of names for objects to be patched</param>
    /// <param name="patches">    The set of patches for the object</param>
    private SceneStateActionPatchSet(string sceneName, string[] objectNames, List<SceneStateActionPatch> patches, string fsmName = "")
        : this(sceneName, "", patches, fsmName)
    {
        NameArray = objectNames;
    }

    public string SceneName { get; } = sceneName;

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

/// <summary>Patch for a Scene State Action</summary>
/// ///
/// <param name="sceneName"> Name of scene that the object is within</param>
/// <param name="stateName"> The state that is to be patched within</param>
/// <param name="actionType">The action to be patched</param>
/// <param name="patch">     The patch to be applied</param>
/// <param name="unpatch">   Optional patch to remove</param>
/// <param name="fsmName">   
/// Optional fsm name, used when calling either ApplyPatch or RemovePatch with an array of PlayMakerFSMs
/// </param>
public class SceneStateActionPatch(string sceneName, string objectName, string stateName, Type actionType, Action<FsmStateAction, object[]?> patch, Action<FsmStateAction, object[]?>? unpatch = null, string fsmName = "")
    : StateActionPatch_Base(stateName, actionType, patch, unpatch, fsmName), ISceneFSMPatch
{
    /// <summary>Patch for a Scene State Action</summary>
    /// ///
    /// <param name="sceneName"> Name of scene that the object is within</param>
    /// <param name="stateNames">An array of state names to be patched within</param>
    /// <param name="actionType">The action to be patched</param>
    /// <param name="patch">     The patch to be applied</param>
    /// <param name="unpatch">   Optional patch to remove</param>
    /// <param name="fsmName">   
    /// Optional fsm name, used when calling either ApplyPatch or RemovePatch with an array of PlayMakerFSMs
    /// </param>
    public SceneStateActionPatch(string sceneName, string objectName, string[] stateNames, Type actionType, Action<FsmStateAction, object[]?> patch, Action<FsmStateAction, object[]?>? unpatch = null, string fsmName = "")
        : this(sceneName, objectName, "", actionType, patch, unpatch, fsmName)
    {
        NameArray = stateNames;
    }

    public string SceneName { get; } = sceneName;

    /// <summary>Name of the patch target</summary>
    public string ObjectName { get; } = objectName;

    public override object Clone()
    {
        if (NameArray.Length > 0)
            return new SceneStateActionPatch(SceneName, ObjectName, NameArray, Type, (Action<FsmStateAction, object[]?>)Patch.Clone(), (Action<FsmStateAction, object[]?>?)Unpatch?.Clone(), FSMName);
        return new SceneStateActionPatch(SceneName, ObjectName, Name, Type, (Action<FsmStateAction, object[]?>)Patch.Clone(), (Action<FsmStateAction, object[]?>?)Unpatch?.Clone(), FSMName);
    }
}

#endregion FSM State Action

#region FSM State

/// <summary>Class for containing a set of Scene State patches</summary>
public class SceneStateCollection : SceneFSMPatchCollections_Base<SceneStatePatch, SceneStatePatchSet, FsmState>;

/// <summary>Patch set of Scene States</summary>
/// <param name="objectName">Name of the object to be patched</param>
/// <param name="patches">   The set of patches for the object</param>
public class SceneStatePatchSet(string sceneName, string objectName, List<SceneStatePatch> patches, string fsmName = "")
    : StatePatchSet_Base<SceneStatePatch>(objectName, patches, fsmName), ISceneFSMPatchSet
{
    /// <summary>Patch set of Scene States</summary>
    /// <param name="objectNames">Array of names for objects to be patched</param>
    /// <param name="patches">    The set of patches for the object</param>
    private SceneStatePatchSet(string sceneName, string[] objectNames, List<SceneStatePatch> patches, string fsmName = "")
        : this(sceneName, "", patches, fsmName)
    {
        NameArray = objectNames;
    }

    public string SceneName { get; } = sceneName;

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

/// <summary>Patch for a Scene State</summary>
/// ///
/// <param name="sceneName">Name of scene that the object is within</param>
/// <param name="stateName">The state that is to be patched</param>
/// <param name="patch">    The patch to be applied</param>
/// <param name="unpatch">  Optional patch to remove</param>
/// <param name="fsmName">  
/// Optional fsm name, used when calling either ApplyPatch or RemovePatch with an array of PlayMakerFSMs
/// </param>
public class SceneStatePatch(string sceneName, string objectName, string stateName, Action<FsmState, object[]?> patch, Action<FsmState, object[]?>? unpatch = null, string fsmName = "")
: StatePatch_Base(stateName, patch, unpatch, fsmName), ISceneFSMPatch
{
    /// <summary>Patch for a Scene State</summary>
    /// <param name="sceneName"> Name of scene that the object is within</param>
    /// <param name="stateNames">An array of state names to be patched within</param>
    /// <param name="patch">     The patch to be applied</param>
    /// <param name="unpatch">   Optional patch to remove</param>
    /// <param name="fsmName">   
    /// Optional fsm name, used when calling either ApplyPatch or RemovePatch with an array of PlayMakerFSMs
    /// </param>
    public SceneStatePatch(string sceneName, string objectName, string[] stateNames, Action<FsmState, object[]?> patch, Action<FsmState, object[]?>? unpatch = null, string fsmName = "")
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
            return new SceneStatePatch(SceneName, ObjectName, NameArray, (Action<FsmState, object[]?>)Patch.Clone(), (Action<FsmState, object[]?>?)Unpatch?.Clone(), FSMName);
        return new SceneStatePatch(SceneName, ObjectName, Name, (Action<FsmState, object[]?>)Patch.Clone(), (Action<FsmState, object[]?>?)Unpatch?.Clone(), FSMName);
    }
}

#endregion FSM State

#region Abstraction

/// <summary>Abstract class for a collection of Scene FSM patches</summary>
/// <typeparam name="PatchType">The class for the base patch sets</typeparam>
/// <typeparam name="PatchSetType">The class for the base patch</typeparam>
/// <typeparam name="PatchTarget">The type of FSM thing the patches are patching</typeparam>
public abstract class SceneFSMPatchCollections_Base<PatchType, PatchSetType, PatchTarget>
    : ObjectPatchCollection_Base<PatchType, PatchSetType, PlayMakerFSM, PatchTarget>
    where PatchType : FSMPatch_Base<PatchTarget>
    where PatchSetType : FSMPatchSet_Base<PatchType, PatchTarget>
{
    private readonly Dictionary<string, Dictionary<string, int>> patchedObjects = [];

    /// <summary>
    /// Dictionary containing the names of objects within scenes to patch and how many patches are to be applied to them
    /// </summary>
    public new Dictionary<string, Dictionary<string, int>> PatchedObjects => patchedObjects;

    /// <summary>The collection of patches that this contains</summary>
    public new Dictionary<string, Dictionary<string, HashSet<PatchType>>> Patches { get; } = [];

    /// <summary>Applies the patches to the given Scene</summary>
    /// <param name="scene">  The scene to be patched</param>
    /// <param name="objects">The objects within the scene</param>
    public void ApplyPatches(UnityEngine.SceneManagement.Scene scene, HashSet<GameObject> objects)
    {
        if (Patches.TryGetValue(scene.name, out Dictionary<string, HashSet<PatchType>> patches))
        {
            foreach (var patchGroup in patches)
            {
                foreach (var setPatch in patchGroup.Value)
                    setPatch.ApplyPatch(objects.FirstOrDefault(obj => obj.name.Equals(patchGroup.Key)).GetComponents<PlayMakerFSM>());
            }
        }
    }

    /// <summary>Removes the patches applied to the given Scene</summary>
    /// <param name="scene">  The scene that the patches were applied to</param>
    /// <param name="objects">The objects within the scene</param>
    public void RemovePatches(UnityEngine.SceneManagement.Scene scene, HashSet<GameObject> objects)
    {
        if (Patches.TryGetValue(scene.name, out Dictionary<string, HashSet<PatchType>> patches))
        {
            foreach (var patchGroup in patches)
            {
                foreach (var setPatch in patchGroup.Value)
                    setPatch.RemovePatch(objects.FirstOrDefault(obj => obj.name.Equals(patchGroup.Key)).GetComponents<PlayMakerFSM>());
            }
        }
    }

    /// <summary>Register a collection of patches</summary>
    /// <param name="patchCollection">The dictionary containing the patches to be applied to objects</param>
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
    /// <param name="sceneName">      Name of scene that the object is within</param>
    /// <param name="objectName">     Name of the object</param>
    /// <param name="patchCollection">List containing patches to apply</param>
    public virtual void RegisterPatchCollection(string sceneName, string objectName, List<PatchType> patchCollection)
    {
        foreach (var patch in patchCollection)
        {
            RegisterPatchInCollection(sceneName, objectName, patch);
        }
    }

    /// <summary>Register a patch for an object in a scene</summary>
    /// <param name="sceneName"> Name of scene that the object is within</param>
    /// <param name="objectName">Name of the object</param>
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
    /// <param name="patchCollection">The dictionary containing the patches to be removed from objects</param>
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
    /// <param name="sceneName">      Name of scene that the object is within</param>
    /// <param name="objectName">     Name of the object</param>
    /// <param name="patchCollection">List containing patches to remove</param>
    public virtual void UnregisterPatchCollection(string sceneName, string objectName, List<PatchType> patchCollection)
    {
        foreach (var patch in patchCollection)
        {
            UnregisterPatchInCollection(sceneName, objectName, patch);
        }
    }

    /// <summary>Unregister a patch for an object in a scene</summary>
    /// <param name="sceneName"> Name of scene that the object is within</param>
    /// <param name="objectName">Name of the object</param>
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

    #region Overrided Register/Unregister Methods

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
    #endregion Overrided Register/Unregister Methods
}

#endregion Abstraction

/// <summary>Marks a patch as a Scene patch</summary>
public interface IScenePatch
{
    /// <summary>Name of the scene the patch is for</summary>
    public string SceneName { get; }

    /// <summary>Name of the patch target</summary>
    public string ObjectName { get; }
}

/// <summary>Marks a patch as a Scene FSM Patch</summary>
public interface ISceneFSMPatch : IFSMPatch, ISmolPatch, IScenePatch;

/// <summary>Marks a patch set as a Scene FSM Patch Set, includes the interface to additionally mark it as a patch</summary>
public interface ISceneFSMPatchSet : ISceneFSMPatch, ISmolPatchSet;
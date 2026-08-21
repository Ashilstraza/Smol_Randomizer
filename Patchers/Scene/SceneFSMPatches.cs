using System;
using System.Collections.Generic;
using System.Linq;

using HutongGames.PlayMaker;

using UnityEngine;

namespace Smol_Randomizer.Patchers.Scene;

/// <summary>
/// Handles patching Scene FSMs
/// </summary>
internal class SceneFSMPatches
{
    private static readonly Lazy<SceneFSMPatches> instance = new(() => new SceneFSMPatches());
    /// <summary>
    /// A set of Scene FSM Patches
    /// </summary>
    public static SceneFSMPatches Instance => instance.Value;

    /// <summary>
    /// Class for containing a set of Scene State Action patches
    /// </summary>
    public class SceneStateActionCollection : SceneFSMPatchCollections_Base<SceneStateActionPatch, SceneStateActionPatchSet, FsmStateAction>;

    /// <summary>
    /// Class for containing a set of Scene State patches
    /// </summary>
    public class SceneStateCollection : SceneFSMPatchCollections_Base<SceneStatePatch, SceneStatePatchSet, FsmState>;

    /// <summary>
    /// A set of Scene State Action patches
    /// </summary>
    public static readonly SceneStateActionCollection sceneStateActionCollection = new();
    /// <summary>
    /// As set if Scene State Patches
    /// </summary>
    public static readonly SceneStateCollection sceneStateCollection = new();

    /// <summary>
    /// Apply the various scene patches
    /// </summary>
    /// <param name="scene">The scene to patch</param>
    internal static void ApplyPatches(UnityEngine.SceneManagement.Scene scene)
    {
        HashSet<GameObject> objects = scene.GetRootGameObjects().ToHashSet();

        sceneStateActionCollection.ApplyPatches(scene, objects);
        sceneStateCollection.ApplyPatches(scene, objects);
    }

    /// <summary>
    /// Remove the various scene patches
    /// </summary>
    /// <param name="scene">The scene to unpatch</param>
    internal static void RemovePatches(UnityEngine.SceneManagement.Scene scene)
    {
        HashSet<GameObject> objects = scene.GetRootGameObjects().ToHashSet();

        sceneStateActionCollection.RemovePatches(scene, objects);
        sceneStateCollection.RemovePatches(scene, objects);
    }
}

#region FSM State Action
/// <summary>
/// Patch set of Scene State Actions
/// </summary>
/// <param name="objectName">Name of the object to be patched</param>
/// <param name="patches">The set of patches for the object</param>
public class SceneStateActionPatchSet(string objectName, List<SceneStateActionPatch> patches)
    : SceneFSMPatchSet_Base<SceneStateActionPatch, FsmStateAction>(objectName, patches)
{
    /// <summary>
    /// Patch set of Scene State Actions
    /// </summary>
    /// <param name="objectNames">Array of names for objects to be patched</param>
    /// <param name="patches">The set of patches for the object</param>
    SceneStateActionPatchSet(string[] objectNames, List<SceneStateActionPatch> patches)
        : this("", patches)
    {
        NameArray = objectNames;
    }
    public override object Clone()
    {
        List<SceneStateActionPatch> newPatchList = [];

        foreach (var patch in Patches)
        {
            newPatchList.Add((SceneStateActionPatch)patch.Clone());
        }

        if (Name != "")
            return new SceneStateActionPatchSet(Name, newPatchList);
        return new SceneStateActionPatchSet(NameArray, newPatchList);
    }
}

/// <summary>
/// Patch for a Scene State Action
/// </summary>
/// <param name="stateName">The state that is to be patched within</param>
/// <param name="actionType">The action to be patched</param>
/// <param name="patch">The patch to be applied</param>
/// <param name="unpatch">Optional patch to remove</param>
/// <param name="fsmName">Optional fsm name, used when calling either ApplyPatch or RemovePatch with an array of PlayMakerFSMs</param>
public class SceneStateActionPatch(string stateName, Type actionType, Action<FsmStateAction, object[]?> patch, Action<FsmStateAction, object[]?>? unpatch = null, string fsmName = "")
    : SceneFSMPatch_Base<FsmStateAction>(stateName, patch, unpatch), IFSMActionPatch
{
    /// <summary>
    /// Patch for a Scene State Action
    /// </summary>
    /// <param name="stateNames">An array of state names to be patched within</param>
    /// <param name="actionType">The action to be patched</param>
    /// <param name="patch">The patch to be applied</param>
    /// <param name="unpatch">Optional patch to remove</param>
    /// <param name="fsmName">Optional fsm name, used when calling either ApplyPatch or RemovePatch with an array of PlayMakerFSMs</param>
    public SceneStateActionPatch(string[] stateNames, Type actionType, Action<FsmStateAction, object[]?> patch, Action<FsmStateAction, object[]?>? unpatch = null, string fsmName = "")
        : this("", actionType, patch, unpatch, fsmName)
    {
        NameArray = stateNames;
    }

    public Type Type { get; } = actionType;
    public string TypeString => Type.ToString();

    /// <summary>
    /// Patches an FSM
    /// </summary>
    /// <param name="patchTarget">Array that contains the FSM to patch</param>
    /// <param name="param">An array of arguments the patcher may want</param>
    public override void ApplyPatch(PlayMakerFSM[] patchTarget, object[]? param = null)
    {
        ApplyPatch(patchTarget.FirstOrDefault(obj => obj.name.Equals(fsmName)), param);
    }

    public override void ApplyPatch(PlayMakerFSM fsm, object[]? param = null)
    {
        if (NameArray.Length > 0)
        {
            foreach (string name in NameArray)
            {
                foreach (FsmStateAction action in fsm.FsmStates.FirstOrDefault(state => state.Name.Equals(name)).Actions)
                {
                    if (action.GetType().ToString().Equals(TypeString))
                    {
                        Patch(action, param);
                    }
                }
            }
        }
        else
        {
            foreach (FsmStateAction action in fsm.FsmStates.FirstOrDefault(state => state.Name.Equals(Name)).Actions)
            {
                if (action.GetType().ToString().Equals(TypeString))
                {
                    Patch(action, param);
                }
            }
        }
    }

    /// <summary>
    /// Removes patches from an FSM
    /// </summary>
    /// <param name="patchTarget">Array that contains the FSM to remove patches from</param>
    /// <param name="param">An array of arguments the unpatcher may want</param>
    public override void RemovePatch(PlayMakerFSM[] patchTarget, object[]? param = null)
    {
        if (Unpatch == null) return;

        RemovePatch(patchTarget.FirstOrDefault(obj => obj.name.Equals(fsmName)), param);
    }

    public override void RemovePatch(PlayMakerFSM fsm, object[]? param = null)
    {
        if (Unpatch == null) return;

        if (NameArray.Length > 0)
        {
            foreach (string name in NameArray)
            {
                foreach (FsmStateAction action in fsm.FsmStates.FirstOrDefault(state => state.Name.Equals(name)).Actions)
                {
                    if (action.GetType().ToString().Equals(TypeString))
                    {
                        Unpatch(action, param);
                    }
                }
            }
        }
        else
        {
            foreach (FsmStateAction action in fsm.FsmStates.FirstOrDefault(state => state.Name.Equals(Name)).Actions)
            {
                if (action.GetType().ToString().Equals(TypeString))
                {
                    Unpatch(action, param);
                }
            }
        }
    }

    public override object Clone()
    {
        if (Name != "")
            return new SceneStateActionPatch(Name, Type, (Action<FsmStateAction, object[]?>)Patch.Clone(), (Action<FsmStateAction, object[]?>?)Unpatch?.Clone(), FSMName);
        return new SceneStateActionPatch(NameArray, Type, (Action<FsmStateAction, object[]?>)Patch.Clone(), (Action<FsmStateAction, object[]?>?)Unpatch?.Clone(), FSMName);
    }
}
#endregion

#region FSM State
/// <summary>
/// Patch set of Scene States
/// </summary>
/// <param name="objectName">Name of the object to be patched</param>
/// <param name="patches">The set of patches for the object</param>
public class SceneStatePatchSet(string objectName, List<SceneStatePatch> patches)
    : SceneFSMPatchSet_Base<SceneStatePatch, FsmState>(objectName, patches)
{
    /// <summary>
    /// Patch set of Scene States
    /// </summary>
    /// <param name="objectNames">Array of names for objects to be patched</param>
    /// <param name="patches">The set of patches for the object</param>
    SceneStatePatchSet(string[] objectNames, List<SceneStatePatch> patches)
        : this("", patches)
    {
        NameArray = objectNames;
    }
    public override object Clone()
    {
        List<SceneStatePatch> newPatchList = [];

        foreach (var patch in Patches)
        {
            newPatchList.Add((SceneStatePatch)patch.Clone());
        }

        if (Name != "")
            return new SceneStatePatchSet(Name, newPatchList);
        return new SceneStatePatchSet(NameArray, newPatchList);
    }
}

/// <summary>
/// Patch for a Scene State
/// </summary>
/// <param name="stateName">The state that is to be patched</param>
/// <param name="patch">The patch to be applied</param>
/// <param name="unpatch">Optional patch to remove</param>
/// <param name="fsmName">Optional fsm name, used when calling either ApplyPatch or RemovePatch with an array of PlayMakerFSMs</param>
public class SceneStatePatch(string stateName, Action<FsmState, object[]?> patch, Action<FsmState, object[]?>? unpatch = null, string fsmName = "")
: SceneFSMPatch_Base<FsmState>(stateName, patch, unpatch)
{
    /// <summary>
    /// Patch for a Scene State
    /// </summary>
    /// <param name="stateNames">An array of state names to be patched within</param>
    /// <param name="patch">The patch to be applied</param>
    /// <param name="unpatch">Optional patch to remove</param>
    /// <param name="fsmName">Optional fsm name, used when calling either ApplyPatch or RemovePatch with an array of PlayMakerFSMs</param>
    public SceneStatePatch(string[] stateNames, Action<FsmState, object[]?> patch, Action<FsmState, object[]?>? unpatch = null, string fsmName = "")
        : this("", patch, unpatch, fsmName)
    {
        NameArray = stateNames;
    }

    /// <summary>
    /// Patches an FSM, will ignore Name if NameArray is populated
    /// </summary>
    /// <param name="patchTarget">Array that contains the FSM to patch</param>
    /// <param name="param">An array of arguments the patcher may want</param>
    public override void ApplyPatch(PlayMakerFSM[] patchTarget, object[]? param = null)
    {
        ApplyPatch(patchTarget.FirstOrDefault(obj => obj.name.Equals(fsmName)), param);
    }

    public override void ApplyPatch(PlayMakerFSM fsm, object[]? param = null)
    {
        if (NameArray.Length > 0)
        {
            foreach (string name in NameArray)
            {
                Patch(fsm.FsmStates.FirstOrDefault(state => state.Name.Equals(name)), param);
            }
        }
        else
            Patch(fsm.FsmStates.FirstOrDefault(state => state.Name.Equals(Name)), param);
    }

    /// <summary>
    /// Removes patches from an FSM, will ignore Name if NameArray is populated
    /// </summary>
    /// <param name="patchTarget">Array that contains the FSM to remove patches from</param>
    /// <param name="param">An array of arguments the unpatcher may want</param>
    public override void RemovePatch(PlayMakerFSM[] patchTarget, object[]? param = null)
    {
        if (Unpatch == null) return;

        RemovePatch(patchTarget.FirstOrDefault(obj => obj.name.Equals(fsmName)), param);
    }

    public override void RemovePatch(PlayMakerFSM fsm, object[]? param = null)
    {
        if (Unpatch == null) return;

        if (NameArray.Length > 0)
        {
            foreach (string name in NameArray)
            {
                Unpatch(fsm.FsmStates.FirstOrDefault(state => state.Name.Equals(name)), param);
            }
        }
        else
            Unpatch(fsm.FsmStates.FirstOrDefault(state => state.Name.Equals(Name)), param);
    }

    public override object Clone()
    {
        if (Name != "")
            return new SceneStatePatch(Name, (Action<FsmState, object[]?>)Patch.Clone(), (Action<FsmState, object[]?>?)Unpatch?.Clone(), FSMName);
        return new SceneStatePatch(NameArray, (Action<FsmState, object[]?>)Patch.Clone(), (Action<FsmState, object[]?>?)Unpatch?.Clone(), FSMName);
    }
}
#endregion

#region Abstraction
/// <summary>
/// Abstract class for a collection of Scene FSM patches
/// </summary>
/// <typeparam name="PatchType">The class for the base patch sets</typeparam>
/// <typeparam name="PatchSetType">The class for the base patch</typeparam>
/// <typeparam name="PatchTarget">The type of FSM thing the patches are patching</typeparam>
public abstract class SceneFSMPatchCollections_Base<PatchType, PatchSetType, PatchTarget>
    : ObjectPatchCollection_Base<PatchType, PatchSetType, PlayMakerFSM[], PatchTarget>
    where PatchType : SceneFSMPatch_Base<PatchTarget>
    where PatchSetType : SceneFSMPatchSet_Base<PatchType, PatchTarget>
{

    /// <summary>
    /// The collection of patches that this contains
    /// </summary>
    public new Dictionary<string, Dictionary<string, HashSet<PatchType>>> Patches { get; } = [];

    /// <summary>
    /// Applies the patches to the given Scene
    /// </summary>
    /// <param name="scene">The scene to be patched</param>
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

    /// <summary>
    /// Removes the patches applied to the given Scene
    /// </summary>
    /// <param name="scene">The scene that the patches were applied to</param>
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
}

/// <summary>
/// Abstract class for a set of Scene object FSM patches
/// </summary>
/// <typeparam name="PatchType">The Patch type to use</typeparam>
/// <typeparam name="PatchTarget">The type of FSM thing to patch</typeparam>
/// <param name="name">Name of the object to be patched</param>
/// <param name="patches">The patches to apply</param>
/// <param name="fsmName">Optional fsm name</param>
public abstract class SceneFSMPatchSet_Base<PatchType, PatchTarget>(string name, List<PatchType> patches, string fsmName = "")
    : ObjectPatchSet_Base<PatchType, PlayMakerFSM[], PatchTarget>(name, patches), ISceneFSMPatchSet
    where PatchType : SceneFSMPatch_Base<PatchTarget>
{
    public string FSMName { get; } = fsmName;
}

/// <summary>
/// Abstract class for a Scene FSM patch
/// </summary>
/// <typeparam name="PatchTarget">The type of FSM thing to patch</typeparam>
/// <param name="name">Name of the object to patch</param>
/// <param name="patch">The patch to be applied</param>
/// <param name="unpatch">Optional patch to remove the changes</param>
/// <param name="fsmName">Optional name of the FSM to patch</param>
public abstract class SceneFSMPatch_Base<PatchTarget>(string name, Action<PatchTarget, object[]?> patch, Action<PatchTarget, object[]?>? unpatch = null, string fsmName = "")
        : ObjectPatch_Base<PatchTarget, PlayMakerFSM[]>(name, patch, unpatch), ISceneFSMPatch
{
    public string FSMName { get; } = fsmName;

    /// <summary>
    /// Patches an FSM
    /// </summary>
    /// <param name="fsm">The FSM to patch</param>
    /// <param name="param">An array of arguments the patcher may want</param>
    public abstract void ApplyPatch(PlayMakerFSM fsm, object[]? param = null);

    /// <summary>
    /// Removes patches from an FSM
    /// </summary>
    /// <param name="fsm">The FSM to remove patches from</param>
    /// <param name="param">An array of arguments the unpatcher may want</param>
    public abstract void RemovePatch(PlayMakerFSM fsm, object[]? param = null);
}
#endregion

/// <summary>
/// Marks a patch as a Scene FSM Patch
/// </summary>
public interface ISceneFSMPatch : IFSMPatch, ISmolPatch;

/// <summary>
/// Marks a patch set as a Scene FSM Patch Set, includes the interface to additionally mark it as a patch
/// </summary>
public interface ISceneFSMPatchSet : ISceneFSMPatch, ISmolPatchSet;
using System;
using System.Collections.Generic;
using System.Linq;

using HutongGames.PlayMaker;

using UnityEngine;
using UnityEngine.SceneManagement;

using static Smol_Randomizer.Patchers.ObjectPatcher;

namespace Smol_Randomizer.Patchers;

/// <summary>
/// Class for patching various object FSMs
/// </summary>
public static class FSMPatcher
{
    #region Scene
    /// <summary>
    /// Class for containing a set of Scene State Action patches
    /// </summary>
    public class SceneStateActionSet : GenericScenePatchSets<FSMStateActionPatchSet, FSMStateActionPatch, FsmStateAction>;

    /// <summary>
    /// Class for containing a set of Scene State patches
    /// </summary>
    public class SceneStateSet : GenericScenePatchSets<FSMStatePatchSet, FSMStatePatch, FsmState>;

    /// <summary>
    /// A set of Scene State Action patches
    /// </summary>
    public static readonly SceneStateActionSet sceneStateActionSet = new();
    /// <summary>
    /// As set if Scene State Patches
    /// </summary>
    public static readonly SceneStateSet sceneStateSet = new();

    /// <summary>
    /// Apply the various scene patches
    /// </summary>
    /// <param name="scene">The scene to patch</param>
    internal static void ApplyScenePatches(Scene scene)
    {
        HashSet<GameObject> objects = scene.GetRootGameObjects().ToHashSet();

        sceneStateActionSet.ApplyPatches(scene, objects);
        sceneStateSet.ApplyPatches(scene, objects);
    }

    /// <summary>
    /// Remove the various scene patches
    /// </summary>
    /// <param name="scene">The scene to unpatch</param>
    internal static void RemoveScenePatches(Scene scene)
    {
        HashSet<GameObject> objects = scene.GetRootGameObjects().ToHashSet();

        sceneStateActionSet.RemovePatches(scene, objects);
        sceneStateSet.RemovePatches(scene, objects);
    }

    /// <summary>
    /// Abstract class for scene patches
    /// </summary>
    /// <typeparam name="FSMPatchSetBase">The class for the base patch sets</typeparam>
    /// <typeparam name="FSMPatchBase">The class for the base patch</typeparam>
    /// <typeparam name="T">The type of FSM thing the patches are patching</typeparam>
    public abstract class GenericScenePatchSets<FSMPatchSetBase, FSMPatchBase, T>
        : GenericScenePatch<FSMPatchSetBase>
        where FSMPatchSetBase : FSMPatchSet_Base<FSMPatchBase, T>
        where FSMPatchBase : FSMPatch_Base<T>
    {
        /// <summary>
        /// Applies the patches to the given Scene
        /// </summary>
        /// <param name="scene">The scene to be patched</param>
        /// <param name="objects">The objects within the scene</param>
        public override void ApplyPatches(Scene scene, HashSet<GameObject> objects)
        {
            if (Patches.TryGetValue(scene.name, out Dictionary<string, HashSet<FSMPatchSetBase>> patchSets))
            {
                foreach (var patchSet in patchSets)
                {
                    foreach (var setPatch in patchSet.Value)
                        setPatch.ApplyPatches(objects.FirstOrDefault(obj => obj.name.Equals(patchSet.Key)).GetComponents<PlayMakerFSM>());
                }
            }
        }

        /// <summary>
        /// Removes the patches applied to the given Scene
        /// </summary>
        /// <param name="scene">The scene that the patches were applied to</param>
        /// <param name="objects">The objects within the scene</param>
        public override void RemovePatches(Scene scene, HashSet<GameObject> objects)
        {
            if (Patches.TryGetValue(scene.name, out Dictionary<string, HashSet<FSMPatchSetBase>> patchSets))
            {
                foreach (var patchSet in patchSets)
                {
                    foreach (var setPatch in patchSet.Value)
                        setPatch.RemovePatches(objects.FirstOrDefault(obj => obj.name.Equals(patchSet.Key)).GetComponents<PlayMakerFSM>());
                }
            }
        }
    }
    #endregion

    #region Enemy
    /// <summary>
    /// Class for containing a set of Enemy State Action patches
    /// </summary>
    public class EnemyStateAction : GenericEnemyPatchSets<FSMStateActionPatchSet, FSMStateActionPatch, FsmStateAction>;
    /// <summary>
    /// Class for containing a set of Enemy State patches
    /// </summary>
    public class EnemyState : GenericEnemyPatchSets<FSMStatePatchSet, FSMStatePatch, FsmState>;

    /// <summary>
    /// A set of Enemy State Action patches
    /// </summary>
    public static readonly EnemyStateAction enemyStateAction = new();
    /// <summary>
    /// As set of Enemy State patches
    /// </summary>
    public static readonly EnemyState enemyState = new();

    /// <summary>
    /// Set of patched enemies
    /// </summary>
    private static HashSet<HealthManager> patchedEnemies = [];

    /// <summary>
    /// Called when the scene loads to clear out unloaded enemies
    /// </summary>
    internal static void OnSceneLoaded()
    {
        patchedEnemies.RemoveWhere(enemy => enemy == null);
    }

    /// <summary>
    /// Apply the various enemy patches
    /// </summary>
    /// <param name="enemyHealthManager">The enemy to patch</param>
    internal static void ApplyEnemyPatches(HealthManager enemyHealthManager)
    {
        string enemyName = enemyHealthManager.name;

        int cullIndex = enemyName.IndexOf('(') - 1;
        if (cullIndex > 0) enemyName = enemyName[..cullIndex];

        if (!enemyStateAction.PatchedEnemies.ContainsKey(enemyName) && !enemyState.PatchedEnemies.ContainsKey(enemyName))
            return;

        GameObject enemyObject = enemyHealthManager.gameObject;

        enemyStateAction.ApplyPatches(enemyName, enemyObject);
        enemyState.ApplyPatches(enemyName, enemyObject);
    }

    /// <summary>
    /// Called to remove all enemy patches
    /// </summary>
    internal static void RemoveEnemyPatches()
    {
        foreach (var enemy in patchedEnemies)
            RemoveEnemyPatches(enemy);
    }

    /// <summary>
    /// Remove the various enemy patches
    /// </summary>
    /// <param name="enemyHealthManager">The enemy to unpatch</param>
    internal static void RemoveEnemyPatches(HealthManager enemyHealthManager)
    {
        string enemyName = enemyHealthManager.name;

        if (!enemyStateAction.PatchedEnemies.ContainsKey(enemyName) && !enemyState.PatchedEnemies.ContainsKey(enemyName))
            return;

        GameObject enemyObject = enemyHealthManager.gameObject;

        enemyStateAction.RemovePatches(enemyName, enemyObject);
        enemyState.RemovePatches(enemyName, enemyObject);
    }

    /// <summary>
    /// Abstract Class for Enemy Patches
    /// </summary>
    /// <typeparam name="FSMPatchSetBase">The class for the base patch sets</typeparam>
    /// <typeparam name="FSMPatchBase">The class for the base patch</typeparam>
    /// <typeparam name="T">The type of FSM thing the patches are patching</typeparam>
    public abstract class GenericEnemyPatchSets<FSMPatchSetBase, FSMPatchBase, T>
        : GenericEnemyPatch<FSMPatchSetBase>
        where FSMPatchSetBase : FSMPatchSet_Base<FSMPatchBase, T>
        where FSMPatchBase : FSMPatch_Base<T>
    {
        public override void ApplyPatches(string enemyName, GameObject enemyObject)
        {
            if (Patches.TryGetValue(enemyName, out HashSet<FSMPatchSetBase> patchSet))
            {
                foreach (var setPatch in patchSet)
                    setPatch.ApplyPatches(enemyObject.GetComponent<PlayMakerFSM>(), [enemyObject]);
            }
        }

        public override void RemovePatches(string enemyName, GameObject enemyObject)
        {
            if (Patches.TryGetValue(enemyName, out HashSet<FSMPatchSetBase> patchSet))
            {
                foreach (var setPatch in patchSet)
                    setPatch.RemovePatches(enemyObject.GetComponent<PlayMakerFSM>(), [enemyObject]);
            }
        }
    }
    #endregion
}

#region FSMState
/// <summary>
/// Contains a patch for an FSM state
/// </summary>
/// <param name="stateName">Name of the state the patch is for</param>
/// <param name="patch">The patch for the state</param>
/// <param name="FSMName">Optional: The name of the FSM the patch is for, used when FSMStatePatchSet.ApplyPatches is called with an array of FSMs</param>
public class FSMStatePatch(string stateName, Action<FsmState, object[]?> patch, Action<FsmState, object[]?>? unpatch = null, string FSMName = "")
    : FSMPatch_Base<FsmState>(stateName, patch, unpatch, FSMName)
{
    /// <summary>
    /// Contains a patch for several FSM states
    /// </summary>
    /// <param name="stateNames">Names of the states the patch is for</param>
    /// <param name="patch">The patch for the state</param>
    /// <param name="FSMName">Optional: The name of the FSM the patch is for, used when FSMStatePatchSet.ApplyPatches is called with an array of FSMs</param>
    public FSMStatePatch(string[] stateNames, Action<FsmState, object[]?> patch, Action<FsmState, object[]?>? unpatch = null, string FSMName = "") : this("", patch, unpatch, FSMName)
    {
        NameArray = stateNames;
    }

    /// <summary>
    /// Names of the states the patch is for
    /// </summary>
    public string[] NameArray { get; } = [];

    /// <summary>
    /// Patches an FSM state, will ignore StateName if StateNameArray is populated
    /// </summary>
    /// <param name="fsm">The PlayMakerFSM to patch</param>
    /// <param name="param">An array of extra arguments that the patcher may want</param>
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
    /// Unpatches an FSM state, will ignore StateName if StateNameArray is populated
    /// </summary>
    /// <param name="fsm">The PlayMakerFSM to unpatch</param>
    /// <param name="param">An array of extra arguments that the unpatcher may want</param>
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
}

/// <summary>
/// Contains a set of state patches for an object
/// </summary>
/// <param name="objectName">The name of the object the patches are for</param>
/// <param name="patches">The list of patches for the object</param>
public class FSMStatePatchSet(string objectName, List<FSMStatePatch> patches)
    : FSMPatchSet_Base<FSMStatePatch, FsmState>(objectName, patches);
#endregion

#region FSMStateAction
/// <summary>
/// Contains a patch for an action in an FSM state
/// </summary>
/// <param name="stateName">Name of the state the patch is for</param>
/// <param name="actionType">The type of the action the patch is for</param>
/// <param name="patch">The patch for the action</param>
/// <param name="FSMName">Optional: The name of the FSM the patch is for, used when FSMStatePatchSet.ApplyPatches is called with an array of FSMs</param>
public class FSMStateActionPatch(string stateName, Type actionType, Action<FsmStateAction, object[]?> patch, Action<FsmStateAction, object[]?>? unpatch = null, string FSMName = "")
    : FSMPatch_Base<FsmStateAction>(stateName, patch, unpatch, FSMName)
{
    /// <summary>
    /// >The type of the action the patch is for
    /// </summary>
    public Type Type { get; } = actionType;

    /// <summary>
    /// The type of the action the patch is for as a string
    /// </summary>
    public string TypeString => Type.ToString();

    public override void ApplyPatch(PlayMakerFSM fsm, object[]? param)
    {
        foreach (FsmStateAction action in fsm.FsmStates.FirstOrDefault(state => state.Name.Equals(Name)).Actions)
        {
            if (action.GetType().ToString().Equals(TypeString))
            {
                Patch(action, param);
            }
        }
    }

    public override void RemovePatch(PlayMakerFSM fsm, object[]? extra)
    {
        if (Unpatch == null) return;

        foreach (FsmStateAction action in fsm.FsmStates.FirstOrDefault(state => state.Name.Equals(Name)).Actions)
        {
            if (action.GetType().ToString().Equals(TypeString))
            {
                Unpatch(action, extra);
            }
        }
    }
}

/// <summary>
/// Contains a set of state action patches for an object
/// </summary>
/// <param name="objectName">The name of the object the patches are for</param>
/// <param name="patches">The list of patches for the object</param>
public class FSMStateActionPatchSet(string objectName, List<FSMStateActionPatch> patches)
    : FSMPatchSet_Base<FSMStateActionPatch, FsmStateAction>(objectName, patches);
#endregion

/// <summary>
/// Abstract class for containing an FSM patch
/// </summary>
/// <typeparam name="T">The type of FSM thing this is patching</typeparam>
/// <param name="name">The name of the thing being patched</param>
/// <param name="patch">The patch to apply</param>
/// <param name="unpatch">Optional unpatcher for when we want to remove it</param>
/// <param name="FSMName">Optional name for the FSM this patch should apply to, used for arrays of FSMs</param>
public abstract class FSMPatch_Base<T>(string name, Action<T, object[]?> patch, Action<T, object[]?>? unpatch = null, string FSMName = "")
    : ObjectPatch_Base<T, PlayMakerFSM>(name, patch, unpatch)
{
    /// <summary>
    /// The name of the FSM the patch is for, used when FSMStatePatchSet.ApplyPatches is called with an array of FSMs
    /// </summary>
    public string FSMName { get; } = FSMName;
}

/// <summary>
/// Abstract class for containing a set of FSM patches
/// </summary>
/// <typeparam name="FSMPatchBase">The type of FSM patch</typeparam>
/// <typeparam name="T">The type of FSM thing this is patching</typeparam>
/// <param name="name">The name of the thing being patched</param>
/// <param name="patches">The list of FSM Patches</param>
public abstract class FSMPatchSet_Base<FSMPatchBase, T>(string name, List<FSMPatchBase> patches)
    : ObjectPatchSet_Base<FSMPatchBase, PlayMakerFSM>(name, patches) where FSMPatchBase : FSMPatch_Base<T>
{
    public override void ApplyPatches(PlayMakerFSM fsm, object[]? param = null)
    {
        foreach (var patch in Patches)
        {
            patch.ApplyPatch(fsm, param);
        }
    }

    public override void ApplyPatches(PlayMakerFSM[] fsmArray, object[]? param = null)
    {
        foreach (var fsm in fsmArray)
        {
            foreach (var patch in Patches)
            {
                if (patch.FSMName == fsm.FsmName)
                    patch.ApplyPatch(fsm, param);
            }
        }
    }

    public override void RemovePatches(PlayMakerFSM fsm, object[]? param = null)
    {
        foreach (var patch in Patches)
        {
            patch.RemovePatch(fsm, param);
        }
    }

    public override void RemovePatches(PlayMakerFSM[] fsmArray, object[]? param = null)
    {
        foreach (var fsm in fsmArray)
        {
            foreach (var patch in Patches)
            {
                if (patch.FSMName == fsm.FsmName)
                    patch.RemovePatch(fsm, param);
            }
        }
    }

    public override void RegisterPatch(FSMPatchBase patch) => Patches.Add(patch);

    public override void UnregisterPatch(FSMPatchBase patch) => Patches.Remove(patch);
}
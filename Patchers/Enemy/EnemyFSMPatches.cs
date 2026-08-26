using System;
using System.Collections.Generic;

using HutongGames.PlayMaker;

using UnityEngine;

namespace Smol_Randomizer.Patchers.Enemy;

/// <summary>
/// Handles patching enemy FSMs
/// </summary>
internal class EnemyFSMPatches
{
    /// <summary>
    /// Class for containing a set of Enemy State Action patches
    /// </summary>
    public class EnemyStateAction : EnemyFSMPatchCollections_Base<EnemyStateActionPatch, EnemyStateActionPatchSet, FsmStateAction>;

    /// <summary>
    /// Class for containing a set of Enemy State patches
    /// </summary>
    public class EnemyState : EnemyFSMPatchCollections_Base<EnemyStatePatch, EnemyStatePatchSet, FsmState>;

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
    /// Registers a collection of patches that are a mix of State and StateActions
    /// </summary>
    /// <param name="enemyPatchCollection"></param>
    public static void RegisterPatchCollection(HashSet<IEnemyFSMPatch> enemyPatchCollection)
    {
        foreach (var patch in enemyPatchCollection)
        {
            if (patch is EnemyStateActionPatch or EnemyStateActionPatchSet)
                enemyStateAction.RegisterPatchInCollection(patch.EnemyName, patch);
            else if (patch is EnemyStatePatchSet)
                enemyState.RegisterPatchInCollection(patch.EnemyName, patch);
            else if (patch is EnemyStatePatch enemyStatePatch)
            {
                if (enemyStatePatch.EnemyNames.Length > 0)
                    foreach (var name in enemyStatePatch.EnemyNames)
                    {
                        enemyState.RegisterPatchInCollection(name, enemyStatePatch);
                    }
                else
                    enemyState.RegisterPatchInCollection(patch.EnemyName, enemyStatePatch);
            }
            else
                CuteRandoCore.Log.LogWarning($"Register patch called with invalid patch type, Name: {patch.Name}, Enemy Name: {patch.EnemyName}, Type: {patch.GetType()}");
        }
    }

    /// <summary>
    /// Unregisters a collection of patches that are a mix of State and StateActions
    /// </summary>
    /// <param name="enemyPatchCollection"></param>
    public static void UnregisterPatchCollection(HashSet<IEnemyFSMPatch> enemyPatchCollection)
    {
        foreach (var patch in enemyPatchCollection)
        {
            if (patch is StateActionPatch_Base or EnemyStateActionPatchSet)
                enemyStateAction.UnregisterPatchInCollection(patch.EnemyName, patch);
            else if (patch is EnemyStatePatch or EnemyStatePatchSet)
                enemyState.UnregisterPatchInCollection(patch.EnemyName, patch);
            else
                CuteRandoCore.Log.LogWarning($"Unregister patch called with invalid patch type, Name: {patch.Name}, Enemy Name: {patch.EnemyName}, Type: {patch.GetType()}");
        }
    }

    /// <summary>
    /// Apply the various enemy patches
    /// </summary>
    /// <param name="enemyHealthManager">The enemy to patch</param>
    internal static void ApplyPatches(HealthManager enemyHealthManager)
    {
        string enemyName = enemyHealthManager.name;

        int cullIndex = enemyName.IndexOf('(') - 1;
        if (cullIndex > 0) enemyName = enemyName[..cullIndex];

        if (!enemyStateAction.PatchedObjects.ContainsKey(enemyName) && !enemyState.PatchedObjects.ContainsKey(enemyName))
            return;

        GameObject enemyObject = enemyHealthManager.gameObject;

        enemyStateAction.ApplyPatches(enemyName, enemyObject);
        enemyState.ApplyPatches(enemyName, enemyObject);
    }

    /// <summary>
    /// Called to remove all enemy patches
    /// </summary>
    internal static void RemovePatches()
    {
        foreach (var enemy in patchedEnemies)
            RemovePatches(enemy);
    }

    /// <summary>
    /// Remove the various enemy patches
    /// </summary>
    /// <param name="enemyHealthManager">The enemy to unpatch</param>
    internal static void RemovePatches(HealthManager enemyHealthManager)
    {
        string enemyName = enemyHealthManager.name;

        if (!enemyStateAction.PatchedObjects.ContainsKey(enemyName) && !enemyState.PatchedObjects.ContainsKey(enemyName))
            return;

        GameObject enemyObject = enemyHealthManager.gameObject;

        enemyStateAction.RemovePatches(enemyName, enemyObject);
        enemyState.RemovePatches(enemyName, enemyObject);
    }
}
#region FSM State
/// <summary>
/// 
/// </summary>
/// <param name="enemyName"></param>
/// <param name="stateName"></param>
/// <param name="patch"></param>
/// <param name="unpatch"></param>
/// <param name="fsmName"></param>
public class EnemyStatePatch(string enemyName, string stateName, Action<FsmState, object[]?> patch, Action<FsmState, object[]?>? unpatch = null, string fsmName = "")
    : StatePatch_Base(stateName, patch, unpatch, fsmName), IEnemyFSMPatch
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="enemyName"></param>
    /// <param name="stateNames"></param>
    /// <param name="patch"></param>
    /// <param name="unpatch"></param>
    /// <param name="fsmName"></param>
    public EnemyStatePatch(string enemyName, string[] stateNames, Action<FsmState, object[]?> patch, Action<FsmState, object[]?>? unpatch = null, string fsmName = "")
        : this(enemyName, "", patch, unpatch, fsmName)
    {
        NameArray = stateNames;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="enemyNames"></param>
    /// <param name="stateName"></param>
    /// <param name="patch"></param>
    /// <param name="unpatch"></param>
    /// <param name="fsmName"></param>
    public EnemyStatePatch(string[] enemyNames, string stateName, Action<FsmState, object[]?> patch, Action<FsmState, object[]?>? unpatch = null, string fsmName = "")
        : this("", stateName, patch, unpatch, fsmName)
    {
        EnemyNames = enemyNames;
    }

    /// <summary>
    /// Array of enemy names to patch
    /// </summary>
    public string[] EnemyNames = [];

    public string EnemyName => enemyName;
}
#pragma warning disable CS9107 // Parameter is captured into the state of the enclosing type and its value is also passed to the base constructor. The value might be captured by the base class as well.
public class EnemyStatePatchSet(string objectName, List<EnemyStatePatch> patches)
    : StatePatchSet_Base<EnemyStatePatch>(objectName, patches), IEnemyFSMPatchSet
#pragma warning restore CS9107
{
    public string EnemyName => objectName;
}
#endregion

#region FSM State Action
public class EnemyStateActionPatch(string enemyName, string stateName, Type actionType, Action<FsmStateAction, object[]?> patch, Action<FsmStateAction, object[]?>? unpatch = null, string fsmName = "")
    : StateActionPatch_Base(stateName, actionType, patch, unpatch, fsmName), IEnemyFSMPatch
{
    public string EnemyName => enemyName;
}

public class EnemyStateActionPatchSet(string enemyName, string objectName, List<EnemyStateActionPatch> patches)
    : StateActionPatchSet_Base<EnemyStateActionPatch>(objectName, patches), IEnemyFSMPatchSet
{
    public string EnemyName => enemyName;
}
#endregion

public abstract class EnemyFSMPatchCollections_Base<PatchType, PatchSetType, PatchTarget>
    : ObjectPatchCollection_Base<PatchType, PatchSetType, PlayMakerFSM, PatchTarget>
    where PatchType : FSMPatch_Base<PatchTarget>, IEnemyFSMPatch
    where PatchSetType : FSMPatchSet_Base<PatchType, PatchTarget>, IEnemyFSMPatchSet

{
    public virtual void ApplyPatches(string enemyName, GameObject enemyObject)
    {
        if (Patches.TryGetValue(enemyName, out HashSet<PatchType> patchSet))
        {
            foreach (var setPatch in patchSet)
                setPatch.ApplyPatch(enemyObject.GetComponent<PlayMakerFSM>(), [enemyObject, enemyName]);
        }
    }

    public virtual void RemovePatches(string enemyName, GameObject enemyObject)
    {
        if (Patches.TryGetValue(enemyName, out HashSet<PatchType> patchSet))
        {
            foreach (var setPatch in patchSet)
                setPatch.RemovePatch(enemyObject.GetComponent<PlayMakerFSM>(), [enemyObject, enemyName]);
        }
    }

    public void RegisterEnemyPatchCollection(List<PatchType> patchCollection)
    {
        foreach (var patch in patchCollection)
        {
            RegisterPatchInCollection(patch.EnemyName, patch);
        }
    }

    public void RegisterPatchInCollection(string enemyName, IEnemyFSMPatch smolPatch)
    {
        if (smolPatch is PatchType patch)
        {
            if (Patches.ContainsKey(enemyName))
            {
                if (!Patches[enemyName].Add(patch))
                    return; // The patch is already in there
            }
            else
                Patches[enemyName] = new() { { patch } };

            if (PatchedObjects.ContainsKey(enemyName))
                PatchedObjects[enemyName]++;
            else
                PatchedObjects.Add(enemyName, 1);
        }
        else if (smolPatch is PatchSetType patchSet)
        {
            RegisterEnemyPatchCollection(patchSet.Patches);
        }
        else
            CuteRandoCore.Log.LogWarning($"Register patch called with invalid patch type, Name: {smolPatch.Name}, Type: {smolPatch.GetType()}");
    }

    public void UnregisterEnemyPatchCollection(List<PatchType> patchCollection)
    {
        foreach (var patch in patchCollection)
        {
            UnregisterPatchInCollection(patch.EnemyName, patch);
        }
    }

    public void UnregisterPatchInCollection(string enemyName, IEnemyFSMPatch smolPatch)
    {
        if (smolPatch is PatchType patch)
        {
            if (Patches.ContainsKey(enemyName))
            {
                if (!Patches[enemyName].Add(patch))
                    return; // The patch is already in there
            }
            else
                Patches[enemyName] = new() { { patch } };

            if (PatchedObjects.ContainsKey(enemyName))
                PatchedObjects[enemyName]++;
            else
                PatchedObjects.Add(enemyName, 1);
        }
        else if (smolPatch is PatchSetType patchSet)
        {
            UnregisterEnemyPatchCollection(patchSet.Patches);
        }
        else
            CuteRandoCore.Log.LogWarning($"Unregister patch called with invalid patch type, Name: {smolPatch.Name}, Type: {smolPatch.GetType()}");
    }
}

public interface IEnemyFSMPatch : ISmolPatch, IEnemyPatch;
public interface IEnemyFSMPatchSet : IEnemyFSMPatch;

public interface IEnemyPatch
{
    /// <summary>
    /// Name of the patch target
    /// </summary>
    public string EnemyName { get; }
}
using HutongGames.PlayMaker;

using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;

namespace Smol_Randomizer.Patchers.Enemy;

/// <summary>Handles patching enemy <see cref="PlayMakerFSM" /> s</summary>
internal class EnemyFSMPatches
{
    /// <summary>Class for containing a set of <see cref="EnemyStateAction" /> patches</summary>
    public class EnemyStateAction : EnemyFSMPatchCollections_Base<EnemyStateActionPatch, EnemyStateActionPatchSet, FsmStateAction>;

    /// <summary>Class for containing a set of <see cref="EnemyState" /> patches</summary>
    public class EnemyState : EnemyFSMPatchCollections_Base<EnemyStatePatch, EnemyStatePatchSet, FsmState>;

    /// <summary>A set of <see cref="EnemyStateAction" /> patches</summary>
    public static readonly EnemyStateAction enemyStateAction = new();

    /// <summary>As set of <see cref="EnemyState" /> patches</summary>
    public static readonly EnemyState enemyState = new();

    /// <summary>Set of patched enemies</summary>
    internal static HashSet<GameObject> patchedEnemies = [];

    /// <summary>Called when the scene loads to clear out unloaded enemies</summary>
    internal static void OnSceneLoaded()
=> patchedEnemies.RemoveWhere(enemy => enemy == null);

    /// <summary>Registers a collection of patches</summary>
    /// <param name="enemyPatchCollection">The collection of patches to add</param>
    public static void RegisterPatchCollection(HashSet<IEnemyFSMPatch> enemyPatchCollection)
    {
        foreach (var patch in enemyPatchCollection)
        {
            if (patch is EnemyStateActionPatch or EnemyStateActionPatchSet)
                enemyStateAction.RegisterPatchInCollection(patch.EnemyName, patch);
            else if (patch is EnemyStatePatchSet or EnemyStatePatch)
                enemyState.RegisterPatchInCollection(patch.EnemyName, patch);
            else
                CuteRandoCore.Log.LogWarning($"Register patch called with invalid patch type; Name: {patch.Name}, Enemy Name: {patch.EnemyName}, Type: {patch.GetType()}");
        }
    }

    /// <summary>Unregisters a collection of patches</summary>
    /// <param name="enemyPatchCollection">The collection of patches to remove</param>
    public static void UnregisterPatchCollection(HashSet<IEnemyFSMPatch> enemyPatchCollection)
    {
        foreach (var patch in enemyPatchCollection)
        {
            if (patch is EnemyStateActionPatch or EnemyStateActionPatchSet)
                enemyStateAction.UnregisterPatchInCollection(patch.EnemyName, patch);
            else if (patch is EnemyStatePatch or EnemyStatePatchSet)
                enemyState.UnregisterPatchInCollection(patch.EnemyName, patch);
            else
                CuteRandoCore.Log.LogWarning($"Unregister patch called with invalid patch type; Name: {patch.Name}, Enemy Name: {patch.EnemyName}, Type: {patch.GetType()}");
        }
    }

    /// <summary>Registers a patch and overwrites any existing ones with the new one</summary>
    /// <param name="patch">The patch to add</param>
    /// <returns>If the patch was added</returns>
    public static bool OverwritePatch(IEnemyFSMPatch patch)
    {
        if (patch is EnemyStateActionPatch actionPatch)
        {
            enemyStateAction.Patches.TryGetValue(actionPatch.EnemyName, out var patchSet);

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
        else if (patch is EnemyStatePatch statePatch)
        {
            enemyState.Patches.TryGetValue(statePatch.EnemyName, out var patchSet);

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
            CuteRandoCore.Log.LogWarning($"Overwrite patch called with invalid patch type; Name: {patch.Name}, Enemy Name: {patch.EnemyName}, Type: {patch.GetType()}");
        return false;
    }

    /// <summary>Apply the various enemy patches</summary>
    /// <param name="enemy">The enemy to patch</param>
    internal static void ApplyPatches(GameObject enemy)
    {
        {
            if (patchedEnemies.Contains(enemy))
            {
#if TESTING
                CuteRandoCore.Log.LogWarning($"Enemy attempted to patch again; Name: {enemy.name}");
#endif
                return; // Don't reapply patches
            }

            string enemyName = CuteRandoCore.CullName(enemy.name);

            if (!enemyStateAction.PatchedObjects.ContainsKey(enemyName) && !enemyState.PatchedObjects.ContainsKey(enemyName))
                return;

            enemyStateAction.ApplyPatches(enemyName, enemy);
            enemyState.ApplyPatches(enemyName, enemy);
        }
    }

    /// <summary>Called to remove all enemy patches</summary>
    internal static void RemovePatches()
    {
        for (int i = patchedEnemies.Count - 1; i >= 0; i--)
        {
            RemovePatches(patchedEnemies.ElementAt(i));
        }
    }

    /// <summary>Remove the various enemy patches</summary>
    /// <param name="enemy">The enemy to unpatch</param>
    internal static void RemovePatches(GameObject enemy)
    {
        if (enemy == null) return;

        string enemyName = enemy.name;

        if (!enemyStateAction.PatchedObjects.ContainsKey(enemyName) && !enemyState.PatchedObjects.ContainsKey(enemyName))
            return;

        enemyStateAction.RemovePatches(enemyName, enemy);
        enemyState.RemovePatches(enemyName, enemy);

        patchedEnemies.Remove(enemy);
    }
}

#region FSM State

/// <summary>Patches an enemy <see cref="FsmState" /> to add a new <see cref="FsmStateAction" /></summary>
/// <inheritdoc cref="StatePatch_Base" />
/// <param name="enemyName">Name of the enemy whos <see cref="FsmState" /> we want to patch</param>
public class EnemyStatePatch(string enemyName, string stateName, Action<FsmState, object[]> patch, Action<FsmState, object[]>? unpatch = null, string fsmName = "")
    : StatePatch_Base(stateName, patch, unpatch, fsmName), IEnemyFSMPatch
{
    /// <inheritdoc cref="EnemyStatePatch" />
    /// <param name="stateNames"><see cref="Array" /> of names for the <see cref="FsmState" />s to be patched</param>
    public EnemyStatePatch(string enemyName, string[] stateNames, Action<FsmState, object[]> patch, Action<FsmState, object[]>? unpatch = null, string fsmName = "")
        : this(enemyName, "", patch, unpatch, fsmName)
    {
        NameArray = stateNames;
    }

    /// <inheritdoc cref="EnemyStatePatch" />
    /// <param name="enemyNames"><see cref="Array" /> of names for the enemies whos <see cref="PlayMakerFSM" /> we want to patch</param>
    public EnemyStatePatch(string[] enemyNames, string stateName, Action<FsmState, object[]> patch, Action<FsmState, object[]>? unpatch = null, string fsmName = "")
        : this("", stateName, patch, unpatch, fsmName)
    {
        EnemyNames = enemyNames;
    }

    /// <summary><see cref="Array" /> of enemy names to patch</summary>
    private string[] enemyNames = [];

    /// <inheritdoc cref="enemyNames" />
    public string[] EnemyNames { get => enemyNames; protected internal set => enemyNames = value; }

    public string EnemyName { get => enemyName; protected internal set => enemyName = value; }

    public override object Clone()
    {
        if (NameArray.Length > 0)
            return new EnemyStatePatch(EnemyName, NameArray, (Action<FsmState, object[]>)Patch.Clone(), (Action<FsmState, object[]>?)Unpatch?.Clone(), FSMName);
        else if (EnemyNames.Length > 0)
            return new EnemyStatePatch(EnemyNames, Name, (Action<FsmState, object[]>)Patch.Clone(), (Action<FsmState, object[]>?)Unpatch?.Clone(), FSMName);
        return new EnemyStatePatch(EnemyName, Name, (Action<FsmState, object[]>)Patch.Clone(), (Action<FsmState, object[]>?)Unpatch?.Clone(), FSMName);
    }
}

/// <summary>Contains a set of <see cref="EnemyStatePatch" />es</summary>
public class EnemyStatePatchSet : StatePatchSet_Base<EnemyStatePatch>, IEnemyFSMPatchSet
{
    /// <summary>Name of the enemy whos <see cref="PlayMakerFSM" /> we want to patch</summary>
    private string enemyName;

    /// <inheritdoc cref=EnemyStatePatchSet""/>
    /// <param name="enemyName">Name of the enemy whos <see cref="PlayMakerFSM" /> we want to patch</param>
    public EnemyStatePatchSet(string enemyName, List<EnemyStatePatch> patches, string fsmName = "") : base(enemyName, patches, fsmName)
    {
        this.enemyName = enemyName;

        foreach (EnemyStatePatch patch in patches)
        {
            patch.EnemyName = enemyName;
            if (patch.FSMName == "") patch.FSMName = fsmName;
        }
    }

    public string EnemyName { get => enemyName; }

    public override object Clone()
    {
        List<EnemyStatePatch> newList = [];

        foreach (var patch in Patches)
            newList.Add(patch);

        return new EnemyStatePatchSet(Name, newList, FSMName);
    }
}

#endregion FSM State

#region FSM State Action

/// <summary>Patches an enemies <see cref="FsmStateAction" /></summary>
/// <inheritdoc cref="StateActionPatch_Base"/>
/// <param name="enemyName"> Name of the enemy whos <see cref="FsmStateAction" /> we want to patch</param>
public class EnemyStateActionPatch(string enemyName, string stateName, Type actionType, Action<FsmStateAction, object[]> patch, Action<FsmStateAction, object[]>? unpatch = null, string fsmName = "")
    : StateActionPatch_Base(stateName, actionType, patch, unpatch, fsmName), IEnemyFSMPatch
{
    /// <inheritdoc cref="EnemyStateActionPatch" />
    /// <param name="stateNames"><see cref="Array" /> of names for the <see cref="FsmState" /> s to be patch</param>
    public EnemyStateActionPatch(string enemyName, string[] stateNames, Type actionType, Action<FsmStateAction, object[]> patch, Action<FsmStateAction, object[]>? unpatch = null, string fsmName = "")
        : this(enemyName, "", actionType, patch, unpatch, fsmName)
    {
        NameArray = stateNames;
    }

    /// <inheritdoc cref="EnemyStateActionPatch" />
    /// <param name="enemyNames"><see cref="Array" /> of enemy names to patch</summary>
    public EnemyStateActionPatch(string[] enemyNames, string stateName, Type actionType, Action<FsmStateAction, object[]> patch, Action<FsmStateAction, object[]>? unpatch = null, string fsmName = "")
        : this("", stateName, actionType, patch, unpatch, fsmName)
    {
        EnemyNames = enemyNames;
    }

    /// <summary><see cref="Array" /> of enemy names to patch</summary>
    private string[] enemyNames = [];

    /// <inheritdoc cref="enemyNames" />
    public string[] EnemyNames { get => enemyNames; protected internal set => enemyNames = value; }

    public string EnemyName { get => enemyName; protected internal set => enemyName = value; }

    public override object Clone()
    {
        if (NameArray.Length > 0)
            return new EnemyStateActionPatch(EnemyName, NameArray, Type, (Action<FsmStateAction, object[]>)Patch.Clone(), (Action<FsmStateAction, object[]>?)Unpatch?.Clone(), FSMName);
        else if (EnemyNames.Length > 0)
            return new EnemyStateActionPatch(EnemyNames, Name, Type, (Action<FsmStateAction, object[]>)Patch.Clone(), (Action<FsmStateAction, object[]>?)Unpatch?.Clone(), FSMName);
        return new EnemyStateActionPatch(EnemyName, Name, Type, (Action<FsmStateAction, object[]>)Patch.Clone(), (Action<FsmStateAction, object[]>?)Unpatch?.Clone(), FSMName);
    }
}

/// <summary>Contains a set of <see cref="EnemyStateActionPatch" />es</summary>
public class EnemyStateActionPatchSet : StateActionPatchSet_Base<EnemyStateActionPatch>, IEnemyFSMPatchSet
{
    /// <summary>Name of the patch set target</summary>
    private string enemyName;

    /// <inheritdoc cref="EnemyStateActionPatchSet"/>
    /// <param name="enemyName">Name of the enemy whos <see cref="FsmStateAction" /> we want to patch</param>
    public EnemyStateActionPatchSet(string enemyName, List<EnemyStateActionPatch> patches, string fsmName = "") : base(enemyName, patches, fsmName)
    {
        this.enemyName = enemyName;

        foreach (EnemyStateActionPatch patch in patches)
        {
            patch.EnemyName = enemyName;
            if (patch.FSMName == "") patch.FSMName = fsmName;
        }
    }

    public string EnemyName { get => enemyName; }

    public override object Clone()
    {
        List<EnemyStateActionPatch> newList = [];

        foreach (var patch in Patches)
            newList.Add(patch);

        return new EnemyStateActionPatchSet(EnemyName, newList, FSMName);
    }
}

#endregion FSM State Action

/// <summary>Abstract class for Enemy FSM Patch Collections</summary>
/// <typeparam name="PatchType">   The type of FSM Patch</typeparam>
/// <typeparam name="PatchSetType">The type of FSM Patch Set</typeparam>
/// <typeparam name="PatchTarget"> The type of FSM Object we are targeting (<see cref="FsmStateAction"/>
/// or <see cref="FsmState">)</typeparam>
public abstract class EnemyFSMPatchCollections_Base<PatchType, PatchSetType, PatchTarget>
    : ObjectPatchCollection_Base<PatchType, PatchSetType, PlayMakerFSM, PatchTarget>
    where PatchType : FSMPatch_Base<PatchTarget>, IEnemyFSMPatch
    where PatchSetType : FSMPatchSet_Base<PatchType, PatchTarget>, IEnemyFSMPatchSet
{
    /// <summary>Applies the registered patches.</summary>
    /// <param name="enemyName">  Name of the enemy to patch</param>
    /// <param name="enemyObject">The <see cref="GameObject" /> of the enemy to patch</param>
    public virtual void ApplyPatches(string enemyName, GameObject enemyObject)
    {
        if (Patches.TryGetValue(enemyName, out HashSet<PatchType> patchSet))
        {
            foreach (var setPatch in patchSet)
            {
                setPatch.ApplyPatch(enemyObject.GetComponents<PlayMakerFSM>(), [enemyObject, enemyName]);

                EnemyFSMPatches.patchedEnemies.Add(enemyObject);
            }
        }
    }

    /// <summary>Removes the registered patches.</summary>
    /// <param name="enemyName">  Name of the enemy to unpatch</param>
    /// <param name="enemyObject">The <see cref="GameObject" /> of the enemy to unpatch</param>
    public virtual void RemovePatches(string enemyName, GameObject enemyObject)
    {
        if (Patches.TryGetValue(enemyName, out HashSet<PatchType> patchSet))
        {
            foreach (var setPatch in patchSet)
            {
                if (enemyObject == null)
                    continue;
                setPatch.RemovePatch(enemyObject.GetComponents<PlayMakerFSM>(), [enemyObject, enemyName]);
            }
        }
    }

    /// <summary>Registers a collection of FSM patches</summary>
    /// <param name="patchCollection">The collection of patches to register</param>
    /// <param name="enemyName">      The name of the enemy the patches are for</param>
    public void RegisterEnemyPatchCollection(List<PatchType> patchCollection, string enemyName)
    {
        foreach (var patch in patchCollection)
        {
            RegisterPatchInCollection(patch.EnemyName.Equals("") ? enemyName : patch.EnemyName, patch);
        }
    }

    /// <summary>Registers FSM patches</summary>
    /// <param name="enemyName">The name of the enemy the patches are for</param>
    /// <param name="smolPatch">The patch or patch set to register</param>
    public void RegisterPatchInCollection(string enemyName, IEnemyFSMPatch smolPatch)
    {
        if (smolPatch is PatchType patch)
        {
            if (enemyName == "")
            {
                if (smolPatch is EnemyStatePatch statePatch && statePatch.EnemyNames.Length > 0)
                {
                    foreach (string name in statePatch.EnemyNames)
                    {
                        RegisterPatchInCollection(name, smolPatch);
                    }
                    return;
                }
                else if (smolPatch is EnemyStateActionPatch stateActionPatch && stateActionPatch.EnemyNames.Length > 0)
                {
                    foreach (string name in stateActionPatch.EnemyNames)
                    {
                        RegisterPatchInCollection(name, smolPatch);
                    }
                    return;
                }
                else
                    CuteRandoCore.Log.LogWarning($"Register patch called with invalid patch, Name is blank, Enemy Name: {smolPatch.EnemyName}, Type: {smolPatch.GetType()}");
            }
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
            RegisterEnemyPatchCollection(patchSet.Patches, patchSet.EnemyName);
        }
        else
            CuteRandoCore.Log.LogWarning($"Register patch called with invalid patch type, Name: {smolPatch.Name}, Enemy Name: {smolPatch.EnemyName}, Type: {smolPatch.GetType()}");
    }

    /// <summary>Unregisters a collection of FSM patches</summary>
    /// <param name="patchCollection">The collection of patches to unregister</param>
    /// <param name="enemyName">      The name of the enemy the patches were for</param>
    public void UnregisterEnemyPatchCollection(List<PatchType> patchCollection, string enemyName)
    {
        foreach (var patch in patchCollection)
        {
            UnregisterPatchInCollection(patch.EnemyName, patch);
        }
    }

    /// <summary>Unregisters FSM patches</summary>
    /// <param name="enemyName">The name of the enemy the patches were for</param>
    /// <param name="smolPatch">The patch or patch set to unregister</param>
    public void UnregisterPatchInCollection(string enemyName, IEnemyFSMPatch smolPatch)
    {
        if (smolPatch is PatchType patch)
        {
            if (enemyName == "" && smolPatch is EnemyStatePatch statePatch && statePatch.EnemyNames.Length > 0)
            {
                foreach (string name in statePatch.EnemyNames)
                {
                    UnregisterPatchInCollection(name, smolPatch);
                }
                return;
            }
            if (Patches.ContainsKey(enemyName))
            {
                if (Patches[enemyName].Remove(patch))
                {
                    if (PatchedObjects[enemyName] == 1)
                        PatchedObjects.Remove(enemyName);
                    else
                        PatchedObjects[enemyName]--;
                }
            }
        }
        else if (smolPatch is PatchSetType patchSet)
        {
            UnregisterEnemyPatchCollection(patchSet.Patches, patchSet.EnemyName);
        }
        else
            CuteRandoCore.Log.LogWarning($"Unregister patch called with invalid patch type, Name: {smolPatch.Name}, Type: {smolPatch.GetType()}");
    }
}

/// <summary>Marks a patch as an Enemy FSM Patch</summary>
public interface IEnemyFSMPatch : ISmolPatch, IEnemyPatch;

/// <summary>Marks a patch as an Enemy FSM Patch Set</summary>
public interface IEnemyFSMPatchSet : IEnemyFSMPatch;

/// <summary>Marks a patch as an Enemy Patch</summary>
public interface IEnemyPatch
{
    /// <summary>Name of the patch target</summary>
    public string EnemyName { get; }
}
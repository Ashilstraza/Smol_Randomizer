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

    /// <summary>Class for containing a set of <see cref="PlayMakerFSM"/> patches</summary>
    public class EnemyFSMs : EnemyFSMPatchCollections_Base<EnemyFSMPatch, EnemyFSMPatchSet, PlayMakerFSM>;

    /// <summary>A set of <see cref="EnemyStateAction" /> patches</summary>
    public static readonly EnemyStateAction enemyStateAction = new();

    /// <summary>As set of <see cref="EnemyState" /> patches</summary>
    public static readonly EnemyState enemyState = new();

    public static readonly EnemyFSMs enemyFSMPatches = new();

    /// <summary>Set of patched enemies</summary>
    internal static HashSet<GameObject> patchedEnemies = [];

    /// <summary>Called when the scene loads to clear out unloaded enemies</summary>
    internal static void OnSceneLoaded()
=> patchedEnemies.RemoveWhere(enemy => enemy == null);

    /// <summary>Registers a collection of patches</summary>
    /// <param name="enemyPatchCollection">The collection of patches to add</param>
    public static void RegisterPatchCollection(HashSet<IEnemyFSMPatch> enemyPatchCollection)
    {
        int stateActionPatches = 0;
        int statePatches = 0;
        int fsmPatches = 0;

        foreach (var patch in enemyPatchCollection)
        {
            if (patch is EnemyStateActionPatch or EnemyStateActionPatchSet)
                stateActionPatches += enemyStateAction.RegisterPatchInCollection(patch.EnemyName, patch);
            else if (patch is EnemyStatePatchSet or EnemyStatePatch)
                statePatches += enemyState.RegisterPatchInCollection(patch.EnemyName, patch);
            else if (patch is EnemyFSMPatchSet or EnemyFSMPatch)
                fsmPatches += enemyFSMPatches.RegisterPatchInCollection(patch.EnemyName, patch);
            else
                CuteRandoCore.Log.LogWarning($"Register patch called with invalid patch type; Name: {patch.Name}, Enemy Name: {patch.EnemyName}, Type: {patch.GetType()}");
        }
        CuteRandoCore.Log.LogMessage($"Registered {stateActionPatches} State Action Patches, {statePatches} State Patches, and {fsmPatches} Fsm Patches");
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
            else if (patch is EnemyFSMPatchSet or EnemyFSMPatch)
                enemyFSMPatches.UnregisterPatchInCollection(patch.EnemyName, patch);
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
        else if (patch is EnemyFSMPatch)
            throw new NotImplementedException();
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

            enemyFSMPatches.ApplyPatches(enemyName, enemy);
            enemyState.ApplyPatches(enemyName, enemy);
            enemyStateAction.ApplyPatches(enemyName, enemy);
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
        enemyFSMPatches.RemovePatches(enemyName, enemy);

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

    /// <inheritdoc cref="EnemyStatePatch" />
    /// <param name="enemyNames"><see cref="Array" /> of names for the enemies whos <see cref="PlayMakerFSM" /> we want to patch</param>
    public EnemyStatePatch(string[] enemyNames, string[] stateNames, Action<FsmState, object[]> patch, Action<FsmState, object[]>? unpatch = null, string fsmName = "")
        : this("", "", patch, unpatch, fsmName)
    {
        NameArray = stateNames;
        EnemyNames = enemyNames;
    }

    /// <inheritdoc cref="EnemyNames" />
    private string[] enemyNames = [];

    public string[] EnemyNames { get => enemyNames; protected internal set => enemyNames = value; }

    public string EnemyName { get => enemyName; protected internal set => enemyName = value; }

    public override object Clone()
    {
        if (NameArray.Length > 0 && EnemyNames.Length > 0)
            return new EnemyStatePatch(EnemyNames, NameArray, (Action<FsmState, object[]>)Patch.Clone(), (Action<FsmState, object[]>?)Unpatch?.Clone(), FSMName);
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

    /// <inheritdoc cref="EnemyStatePatchSet"/>
    /// <inheritdoc cref="StatePatchSet_Base{EnemyStatePatch}"/>
    /// <param name="enemyName">Name of the enemy whos <see cref="PlayMakerFSM" /> we want to patch</param>
    public EnemyStatePatchSet(string enemyName, List<EnemyStatePatch> patches, string fsmName = "")
        : base(enemyName, patches, fsmName)
    {
        this.enemyName = enemyName;

        foreach (EnemyStatePatch patch in patches)
        {
            patch.EnemyName = enemyName;
            if (patch.FSMName == "") patch.FSMName = fsmName;
        }
    }

    /// <inheritdoc cref="EnemyStatePatchSet"/>
    /// <inheritdoc cref="StatePatchSet_Base{EnemyStatePatch}"/>
    /// <param name="enemyNames">Names of the enemies whos <see cref="PlayMakerFSM" /> we want to patch</param>
    public EnemyStatePatchSet(string[] enemyNames, List<EnemyStatePatch> patches, string fsmName = "")
        : base("", patches, fsmName)
    {
        this.enemyName = "";
        EnemyNames = enemyNames;

        foreach (EnemyStatePatch patch in patches)
        {
            patch.EnemyName = "";
            patch.EnemyNames = enemyNames;
            if (patch.FSMName == "") patch.FSMName = fsmName;
        }
    }

    public string EnemyName { get => enemyName; }

    /// <inheritdoc cref="EnemyNames" />
    private string[] enemyNames = [];

    public string[] EnemyNames { get => enemyNames; protected internal set => enemyNames = value; }

    public override object Clone()
    {
        List<EnemyStatePatch> newList = [];

        foreach (var patch in Patches)
            newList.Add(patch);

        if (EnemyNames.Length > 0)
            return new EnemyStatePatchSet(EnemyNames, newList, FSMName);
        else
            return new EnemyStatePatchSet(EnemyName, newList, FSMName);
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

    /// <inheritdoc cref="EnemyStateActionPatch" />
    /// <param name="enemyNames"><see cref="Array" /> of enemy names to patch</summary>
    /// <param name="stateNames"><see cref="Array" /> of names for the <see cref="FsmState" /> s to be patch</param>
    public EnemyStateActionPatch(string[] enemyNames, string[] stateNames, Type actionType, Action<FsmStateAction, object[]> patch, Action<FsmStateAction, object[]>? unpatch = null, string fsmName = "")
        : this("", "", actionType, patch, unpatch, fsmName)
    {
        EnemyNames = enemyNames;
        NameArray = stateNames;
    }

    /// <inheritdoc cref="EnemyNames" />
    private string[] enemyNames = [];

    public string[] EnemyNames { get => enemyNames; protected internal set => enemyNames = value; }

    public string EnemyName { get => enemyName; protected internal set => enemyName = value; }

    public override object Clone()
    {
        if (NameArray.Length > 0 && EnemyNames.Length > 0)
            return new EnemyStateActionPatch(EnemyNames, NameArray, Type, (Action<FsmStateAction, object[]>)Patch.Clone(), (Action<FsmStateAction, object[]>?)Unpatch?.Clone(), FSMName);
        else if (NameArray.Length > 0)
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
    /// <inheritdoc cref="StateActionPatchSet_Base{EnemyStateActionPatch}"/>
    /// <param name="enemyName">Name of the enemy whos <see cref="FsmStateAction" /> we want to patch</param>
    public EnemyStateActionPatchSet(string enemyName, List<EnemyStateActionPatch> patches, string fsmName = "")
        : base(enemyName, patches, fsmName)
    {
        this.enemyName = enemyName;

        foreach (EnemyStateActionPatch patch in patches)
        {
            patch.EnemyName = enemyName;
            if (patch.FSMName == "") patch.FSMName = fsmName;
        }
    }

    /// <inheritdoc cref="EnemyStateActionPatchSet"/>
    /// <inheritdoc cref="StateActionPatchSet_Base{EnemyStateActionPatch}"/>
    /// <param name="enemyNames">Names of the enemies whos <see cref="FsmStateAction" /> we want to patch</param>
    public EnemyStateActionPatchSet(string[] enemyNames, List<EnemyStateActionPatch> patches, string fsmName = "")
        : base("", patches, fsmName)
    {
        this.enemyName = "";
        EnemyNames = enemyNames;

        foreach (EnemyStateActionPatch patch in patches)
        {
            patch.EnemyName = "";
            patch.EnemyNames = enemyNames;
            if (patch.FSMName == "") patch.FSMName = fsmName;
        }
    }

    public string EnemyName { get => enemyName; }

    /// <inheritdoc cref="EnemyNames" />
    private string[] enemyNames = [];

    public string[] EnemyNames { get => enemyNames; protected internal set => enemyNames = value; }

    public override object Clone()
    {
        List<EnemyStateActionPatch> newList = [];

        foreach (var patch in Patches)
            newList.Add(patch);

        if (EnemyNames.Length > 0)
            return new EnemyStateActionPatchSet(EnemyNames, newList, FSMName);
        else
            return new EnemyStateActionPatchSet(EnemyName, newList, FSMName);
    }
}

#endregion FSM State Action

#region FSM Patch

/// <summary>Patches an enemies <see cref="PlayMakerFSM" /></summary>
/// <inheritdoc cref="FSMPatch_Base{PlayMakerFSM}" />
/// <param name="enemyName">Name of the enemy whos <see cref="PlayMakerFSM" /> we are patching</param>
public class EnemyFSMPatch(string enemyName, Action<PlayMakerFSM, object[]> patch, Action<PlayMakerFSM, object[]>? unpatch = null, string fsmName = "")
    : FSMPatch_Base<PlayMakerFSM>(enemyName + "_FSM", patch, unpatch, fsmName), IEnemyFSMPatch
{
    /// <inheritdoc cref="EnemyFSMPatch" />
    /// <param name="enemyNames"><see cref="Array" /> of enemy names to patch</summary>
    public EnemyFSMPatch(string[] enemyNames, Action<PlayMakerFSM, object[]> patch, Action<PlayMakerFSM, object[]>? unpatch = null, string fsmName = "")
        : this("", patch, unpatch, fsmName)
    {
        EnemyNames = enemyNames;
    }

    public string EnemyName { get => enemyName; protected internal set => enemyName = value; }

    /// <inheritdoc cref="EnemyNames" />
    private string[] enemyNames = [];

    public string[] EnemyNames { get => enemyNames; protected internal set => enemyNames = value; }

    public override void ApplyPatch(PlayMakerFSM[] fsmArray, object[] param)
       => ApplyPatch(fsmArray.FirstOrDefault(obj => obj.FsmName.Equals(FSMName)) ?? fsmArray[0], param);

    public override void ApplyPatch(PlayMakerFSM fsm, object[] param)
        => Patch(fsm, param);

    public override void RemovePatch(PlayMakerFSM[] fsmArray, object[] param)
    {
        if (Unpatch == null) return;

        RemovePatch(fsmArray.FirstOrDefault(obj => obj.FsmName.Equals(FSMName)) ?? fsmArray[0], param);
    }

    public override void RemovePatch(PlayMakerFSM fsm, object[] param)
    {
        if (Unpatch == null) return;

        Unpatch(fsm, param);
    }

    public override object Clone()
    {
        if (enemyNames.Length > 0)
            return new EnemyFSMPatch(EnemyNames, (Action<PlayMakerFSM, object[]>)Patch.Clone(), (Action<PlayMakerFSM, object[]>?)Unpatch?.Clone(), FSMName);
        else
            return new EnemyFSMPatch(EnemyName, (Action<PlayMakerFSM, object[]>)Patch.Clone(), (Action<PlayMakerFSM, object[]>?)Unpatch?.Clone(), FSMName);
    }
}

/// <summary>Contains a set of <see cref="EnemyFSMPatch" />es</summary>
public class EnemyFSMPatchSet : FSMPatchSet_Base<EnemyFSMPatch, PlayMakerFSM>, IEnemyFSMPatchSet
{
    /// <summary>Name of the patch set target</summary>
    private string enemyName;

    /// <inheritdoc cref="EnemyFSMPatchSet"/>
    /// <inheritdoc cref="FSMPatchSet_Base{EnemyFSMPatch,PlayMakerFSM}"/>
    /// <param name="enemyName">Name of the enemy who we want to patch</param>
    public EnemyFSMPatchSet(string enemyName, List<EnemyFSMPatch> patches, string fsmName = "") : base(enemyName, patches, fsmName)
    {
        this.enemyName = enemyName;

        foreach (EnemyFSMPatch patch in patches)
        {
            patch.EnemyName = enemyName;
            if (patch.FSMName == "") patch.FSMName = fsmName;
        }
    }

    /// <inheritdoc cref="EnemyFSMPatchSet"/>
    /// <inheritdoc cref="FSMPatchSet_Base{EnemyFSMPatch,PlayMakerFSM}"/>
    /// <param name="enemyNames">Names of the enemies who we want to patch</param>
    public EnemyFSMPatchSet(string[] enemyNames, List<EnemyFSMPatch> patches, string fsmName = "") : base("", patches, fsmName)
    {
        this.enemyName = "";
        EnemyNames = enemyNames;

        foreach (EnemyFSMPatch patch in patches)
        {
            patch.EnemyName = "";
            patch.EnemyNames = enemyNames;
            if (patch.FSMName == "") patch.FSMName = fsmName;
        }
    }

    /// <inheritdoc cref="EnemyNames" />
    private string[] enemyNames = [];

    public string[] EnemyNames { get => enemyNames; protected internal set => enemyNames = value; }

    public string EnemyName { get => enemyName; }

    public override object Clone()
    {
        List<EnemyFSMPatch> newList = [];

        foreach (var patch in Patches)
            newList.Add(patch);

        if (EnemyNames.Length > 0)
            return new EnemyFSMPatchSet(EnemyNames, newList, FSMName);
        else
            return new EnemyFSMPatchSet(EnemyName, newList, FSMName);
    }
}
#endregion FSM Patch

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
    /// <returns>The number of patches registered</returns>
    public int RegisterEnemyPatchCollection(List<PatchType> patchCollection, string enemyName)
    {
        int patchTotal = 0;

        foreach (var patch in patchCollection)
            patchTotal += RegisterPatchInCollection(patch.EnemyName.Equals("") ?
                                                    patch.EnemyNames.Length > 0 ?
                                                        ""
                                                        : enemyName
                                                    : patch.EnemyName, patch);

        return patchTotal;
    }

    /// <summary>Registers FSM patches</summary>
    /// <param name="enemyName">The name of the enemy the patches are for</param>
    /// <param name="smolPatch">The patch or patch set to register</param>
    /// <returns>The number of patches registered</returns>
    public int RegisterPatchInCollection(string enemyName, IEnemyFSMPatch smolPatch)
    {
        int patchTotal = 0;

        if (smolPatch is PatchType patch)
        {
            if (enemyName == "")
            {
                if (smolPatch is EnemyStatePatch statePatch && statePatch.EnemyNames.Length > 0)
                {
                    foreach (string name in statePatch.EnemyNames)
                        patchTotal += RegisterPatchInCollection(name, smolPatch);

                    return patchTotal;
                }
                else if (smolPatch is EnemyStateActionPatch stateActionPatch && stateActionPatch.EnemyNames.Length > 0)
                {
                    foreach (string name in stateActionPatch.EnemyNames)
                        patchTotal += RegisterPatchInCollection(name, smolPatch);

                    return patchTotal;
                }
                else if (smolPatch is EnemyFSMPatch fsmPatch && fsmPatch.EnemyNames.Length > 0)
                {
                    foreach (string name in fsmPatch.EnemyNames)
                        patchTotal += RegisterPatchInCollection(name, smolPatch);

                    return patchTotal;
                }
                else
                    CuteRandoCore.Log.LogWarning($"Register patch called with invalid patch, Name is blank, Enemy Name: {smolPatch.EnemyName}, Type: {smolPatch.GetType()}");
            }
            if (Patches.ContainsKey(enemyName))
            {
                if (!Patches[enemyName].Add(patch))
                    return patchTotal; // The patch is already in there
            }
            else
                Patches[enemyName] = new() { { patch } };

            if (PatchedObjects.ContainsKey(enemyName))
                PatchedObjects[enemyName]++;
            else
                PatchedObjects.Add(enemyName, 1);
            patchTotal++;
        }
        else if (smolPatch is PatchSetType patchSet)
        {
            if (enemyName == "")
            {
                if (patchSet is EnemyStatePatchSet statePatchSet && statePatchSet.EnemyNames.Length > 0)
                {
                    foreach (string name in statePatchSet.EnemyNames)
                        patchTotal += RegisterEnemyPatchCollection(patchSet.Patches, name);

                    return patchTotal;
                }
                else if (patchSet is EnemyStateActionPatchSet stateActionPatchSet && stateActionPatchSet.EnemyNames.Length > 0)
                {
                    foreach (string name in stateActionPatchSet.EnemyNames)
                        patchTotal += RegisterEnemyPatchCollection(patchSet.Patches, name);

                    return patchTotal;
                }
                else if (patchSet is EnemyFSMPatchSet fsmPatchSet && fsmPatchSet.EnemyNames.Length > 0)
                {
                    foreach (string name in fsmPatchSet.EnemyNames)
                        patchTotal += RegisterEnemyPatchCollection(patchSet.Patches, name);

                    return patchTotal;
                }
                else
                    CuteRandoCore.Log.LogWarning($"Register patch called with invalid patch set, Name is blank, Enemy Name: {smolPatch.EnemyName}, Type: {smolPatch.GetType()}");
            }
            else
                patchTotal += RegisterEnemyPatchCollection(patchSet.Patches, enemyName);
        }
        else
            CuteRandoCore.Log.LogWarning($"Register patch called with invalid patch type, Name: {smolPatch.Name}, Enemy Name: {smolPatch.EnemyName}, Type: {smolPatch.GetType()}");
        return patchTotal;
    }

    /// <summary>Unregisters a collection of FSM patches</summary>
    /// <param name="patchCollection">The collection of patches to unregister</param>
    /// <param name="enemyName">      The name of the enemy the patches were for</param>
    public void UnregisterEnemyPatchCollection(List<PatchType> patchCollection, string enemyName)
    {
        foreach (var patch in patchCollection)
        {
            UnregisterPatchInCollection(patch.EnemyName.Equals("") ?
                                                    patch.NameArray.Length > 0 ?
                                                        ""
                                                        : enemyName
                                                    : patch.EnemyName, patch);
        }
    }

    /// <summary>Unregisters FSM patches</summary>
    /// <param name="enemyName">The name of the enemy the patches were for</param>
    /// <param name="smolPatch">The patch or patch set to unregister</param>
    public void UnregisterPatchInCollection(string enemyName, IEnemyFSMPatch smolPatch)
    {
        if (smolPatch is PatchType patch)
        {
            if (enemyName == "")
            {
                if (smolPatch is EnemyStatePatch statePatch && statePatch.EnemyNames.Length > 0)
                {
                    foreach (string name in statePatch.EnemyNames)
                        UnregisterPatchInCollection(name, smolPatch);
                    return;
                }
                else if (smolPatch is EnemyStateActionPatch stateActionPatch && stateActionPatch.EnemyNames.Length > 0)
                {
                    foreach (string name in stateActionPatch.EnemyNames)
                        UnregisterPatchInCollection(name, smolPatch);
                    return;
                }
                else if (smolPatch is EnemyFSMPatch fsmPatch && fsmPatch.EnemyNames.Length > 0)
                {
                    foreach (string name in fsmPatch.EnemyNames)
                        UnregisterPatchInCollection(name, smolPatch);
                    return;
                }
                else
                    CuteRandoCore.Log.LogWarning($"Register patch called with invalid patch, Name is blank, Enemy Name: {smolPatch.EnemyName}, Type: {smolPatch.GetType()}");
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
            if (enemyName == "")
            {
                if (patchSet is EnemyStatePatchSet statePatchSet && statePatchSet.EnemyNames.Length > 0)
                {
                    foreach (string name in statePatchSet.EnemyNames)
                        UnregisterEnemyPatchCollection(patchSet.Patches, name);

                    return;
                }
                else if (patchSet is EnemyStateActionPatchSet stateActionPatchSet && stateActionPatchSet.EnemyNames.Length > 0)
                {
                    foreach (string name in stateActionPatchSet.EnemyNames)
                        UnregisterEnemyPatchCollection(patchSet.Patches, name);

                    return;
                }
                else if (patchSet is EnemyFSMPatchSet fsmPatchSet && fsmPatchSet.EnemyNames.Length > 0)
                {
                    foreach (string name in fsmPatchSet.EnemyNames)
                        UnregisterEnemyPatchCollection(patchSet.Patches, name);

                    return;
                }
                else
                    CuteRandoCore.Log.LogWarning($"Unregister patch called with invalid patch set, Name is blank, Enemy Name: {smolPatch.EnemyName}, Type: {smolPatch.GetType()}");
            }
            else
                UnregisterEnemyPatchCollection(patchSet.Patches, enemyName);
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

    /// <summary><see cref="Array" /> of enemy names to patch</summary>
    public string[] EnemyNames { get; }
}
using System;
using System.Collections.Generic;
using System.Linq;

using HutongGames.PlayMaker;

using UnityEngine;

namespace Smol_Randomizer.Patchers;
#region FSM State
/// <summary>
/// Contains a patch for an FSM state
/// </summary>
/// <param name="stateName">Name of the state the patch is for</param>
/// <param name="patch">The patch for the state</param>
/// <param name="unpatch">Optional </param>
/// <param name="fsmName">Optional name of the FSM the patch is for, used when FSMStatePatchSet.ApplyPatches is called with an array of FSMs</param>
public class StatePatch(string stateName, Action<FsmState, object[]?> patch, Action<FsmState, object[]?>? unpatch = null, string fsmName = "")
    : FSMPatch_Base<FsmState>(stateName, patch, unpatch, fsmName)
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="stateNames"></param>
    /// <param name="patch"></param>
    /// <param name="unpatch"></param>
    /// <param name="fsmName"></param>
    public StatePatch(string[] stateNames, Action<FsmState, object[]?> patch, Action<FsmState, object[]?>? unpatch = null, string fsmName = "")
        : this("", patch, unpatch, fsmName)
    {
        NameArray = stateNames;
    }

    public override void ApplyPatch(PlayMakerFSM[] fsmArray, object[]? param)
    {
        ApplyPatch(fsmArray.FirstOrDefault(obj => obj.name.Equals(fsmName)), param);
    }

    /// <summary>
    /// Patches an FSM state, will ignore Name if NameArray is populated
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

    public override void RemovePatch(PlayMakerFSM[] fsmArray, object[]? param)
    {
        if (Unpatch == null) return;

        RemovePatch(fsmArray.FirstOrDefault(obj => obj.name.Equals(fsmName)), param);
    }

    /// <summary>
    /// Unpatches an FSM state, will ignore Name if NameArray is populated
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

    public override object Clone()
    {
        if (Name != "")
            return new StatePatch(Name, (Action<FsmState, object[]?>)Patch.Clone(), (Action<FsmState, object[]?>?)Unpatch?.Clone(), FSMName);
        return new StatePatch(NameArray, (Action<FsmState, object[]?>)Patch.Clone(), (Action<FsmState, object[]?>?)Unpatch?.Clone(), FSMName);

    }
}

/// <summary>
/// Contains a set of state patches for an object
/// </summary>
/// <param name="objectName">The name of the object the patches are for</param>
/// <param name="patches">The list of patches for the object</param>
public class StatePatchSet(string objectName, List<StatePatch> patches)
    : FSMPatchSet_Base<StatePatch, FsmState>(objectName, patches)
{
    public override object Clone()
    {
        List<StatePatch> newPatchList = [];
        foreach (var patch in Patches)
        {
            newPatchList.Add((StatePatch)patch.Clone());
        }
        return new StatePatchSet(Name, newPatchList);
    }
}
#endregion

#region FSM State Action
/// <summary>
/// Contains a patch for an action in an FSM state
/// </summary>
/// <param name="stateName">Name of the state the patch is for</param>
/// <param name="actionType">The type of the action the patch is for</param>
/// <param name="patch">The patch for the action</param>
/// <param name="fsmName">Optional: The name of the FSM the patch is for, used when FSMStatePatchSet.ApplyPatches is called with an array of FSMs</param>
public class StateActionPatch(string stateName, Type actionType, Action<FsmStateAction, object[]?> patch, Action<FsmStateAction, object[]?>? unpatch = null, string fsmName = "")
    : FSMPatch_Base<FsmStateAction>(stateName, patch, unpatch, fsmName)
{
    /// <summary>
    /// >The type of the action the patch is for
    /// </summary>
    public Type Type { get; } = actionType;

    /// <summary>
    /// The type of the action the patch is for as a string
    /// </summary>
    public string TypeString => Type.ToString();

    public override void ApplyPatch(PlayMakerFSM[] fsmArray, object[]? param)
    {
        ApplyPatch(fsmArray.FirstOrDefault(obj => obj.name.Equals(fsmName)), param);
    }

    /// <summary>
    /// Patches an FSM
    /// </summary>
    /// <param name="fsm">The FSM to patch</param>
    /// <param name="param">An array of arguments the patcher may want</param>
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

    public override void RemovePatch(PlayMakerFSM[] fsmArray, object[]? param)
    {
        if (Unpatch == null) return;

        RemovePatch(fsmArray.FirstOrDefault(obj => obj.name.Equals(fsmName)), param);
    }

    /// <summary>
    /// Removes patches from an FSM
    /// </summary>
    /// <param name="fsm">The FSM to remove patches from</param>
    /// <param name="param">An array of arguments the unpatcher may want</param>
    public override void RemovePatch(PlayMakerFSM fsm, object[]? param)
    {
        if (Unpatch == null) return;

        foreach (FsmStateAction action in fsm.FsmStates.FirstOrDefault(state => state.Name.Equals(Name)).Actions)
        {
            if (action.GetType().ToString().Equals(TypeString))
            {
                Unpatch(action, param);
            }
        }
    }

    public override object Clone()
    {
        return new StateActionPatch(Name, Type, (Action<FsmStateAction, object[]?>)Patch.Clone(), (Action<FsmStateAction, object[]?>?)Unpatch?.Clone());
    }
}

/// <summary>
/// Contains a set of state action patches for an object
/// </summary>
/// <param name="objectName">The name of the object the patches are for</param>
/// <param name="patches">The list of patches for the object</param>
public class StateActionPatchSet(string objectName, List<StateActionPatch> patches)
    : FSMPatchSet_Base<StateActionPatch, FsmStateAction>(objectName, patches)
{
    public override object Clone()
    {
        List<StateActionPatch> newPatchList = [];
        foreach (var patch in Patches)
        {
            newPatchList.Add((StateActionPatch)patch.Clone());
        }
        return new StateActionPatchSet(Name, newPatchList);
    }
}
#endregion

/// <summary>
/// Abstract class for containing an FSM patch
/// </summary>
/// <typeparam name="PatchTarget">The type of FSM thing this is patching</typeparam>
/// <param name="name">The name of the thing being patched</param>
/// <param name="patch">The patch to apply</param>
/// <param name="unpatch">Optional unpatcher for when we want to remove it</param>
/// <param name="FSMName">Optional name for the FSM this patch should apply to, used for arrays of FSMs</param>
public abstract class FSMPatch_Base<PatchTarget>(string name, Action<PatchTarget, object[]?> patch, Action<PatchTarget, object[]?>? unpatch = null, string fsmName = "")
    : ObjectPatch_Base<PatchTarget, PlayMakerFSM>(name, patch, unpatch), IEnemyFSMPatch
{
    public string FSMName { get; } = fsmName;


    /// <summary>
    /// Patches an FSM
    /// </summary>
    /// <param name="fsmArray">Array that contains the FSM to patch</param>
    /// <param name="param">An array of arguments the patcher may want</param>
    public abstract void ApplyPatch(PlayMakerFSM[] fsmArray, object[]? param = null);

    /// <summary>
    /// Removes patches from an FSM
    /// </summary>
    /// <param name="fsmArray">Array that contains the FSM to remove patches from</param>
    /// <param name="param">An array of arguments the unpatcher may want</param>
    public abstract void RemovePatch(PlayMakerFSM[] fsmArray, object[]? param = null);
}

/// <summary>
/// Abstract class for containing a set of FSM patches
/// </summary>
/// <typeparam name="PatchType">The type of FSM patch</typeparam>
/// <typeparam name="PatchTarget">The type of FSM thing this is patching</typeparam>
/// <param name="name">The name of the thing being patched</param>
/// <param name="patches">The list of FSM Patches</param>
public abstract class FSMPatchSet_Base<PatchType, PatchTarget>(string name, List<PatchType> patches, string fsmName = "")
    : ObjectPatchSet_Base<PatchType, PlayMakerFSM, PatchTarget>(name, patches), IEnemyFSMPatchSet
    where PatchType : FSMPatch_Base<PatchTarget>
{
    public string FSMName { get; } = fsmName;
}

public abstract class FSMPatchCollection_Base<PatchType, PatchSetType, PatchTarget>
    : ObjectPatchCollection_Base<PatchType, PatchSetType, PlayMakerFSM, PatchTarget>
    where PatchType : FSMPatch_Base<PatchTarget>
    where PatchSetType : FSMPatchSet_Base<PatchType, PatchTarget>
{
    public void ApplyPatches(string enemyName, GameObject enemyObject)
    {
        if (Patches.TryGetValue(enemyName, out HashSet<PatchType> patchSet))
        {
            foreach (var setPatch in patchSet)
                setPatch.ApplyPatch(enemyObject.GetComponent<PlayMakerFSM>(), [enemyObject]);
        }
    }

    public void RemovePatches(string enemyName, GameObject enemyObject)
    {
        if (Patches.TryGetValue(enemyName, out HashSet<PatchType> patchSet))
        {
            foreach (var setPatch in patchSet)
                setPatch.RemovePatch(enemyObject.GetComponent<PlayMakerFSM>(), [enemyObject]);
        }
    }
}

public interface IEnemyFSMPatch : IFSMPatch, ISmolPatch;
public interface IEnemyFSMPatchSet : IEnemyFSMPatch, ISmolPatchSet;
public interface IFSMPatch
{
    /// <summary>
    /// The name of the FSM the patch is for, used when FSMStatePatchSet.ApplyPatches is called with an array of FSMs
    /// </summary>
    public string FSMName { get; }
}

public interface IFSMActionPatch
{
    /// <summary>
    /// >The type of the action the patch is for
    /// </summary>
    public Type Type { get; }

    /// <summary>
    /// The type of the action the patch is for as a string
    /// </summary>
    public string TypeString => Type.ToString();
}
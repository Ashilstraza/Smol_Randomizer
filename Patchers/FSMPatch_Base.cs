using HutongGames.PlayMaker;

using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;

namespace Smol_Randomizer.Patchers;

#region FSM State

/// <inheritdoc cref="StatePatch_Base"/>
public class StatePatch(string stateName, Action<FsmState, object[]> patch, Action<FsmState, object[]>? unpatch = null, string fsmName = "")
    : StatePatch_Base(stateName, patch, unpatch, fsmName)
{
    /// <inheritdoc cref="StatePatch"/>
    /// <param name="stateNames"><see cref="Array" /> of names for the <see cref="FsmState" />s to be patched</param>
    public StatePatch(string[] stateNames, Action<FsmState, object[]> patch, Action<FsmState, object[]>? unpatch = null, string fsmName = "")
        : this("", patch, unpatch, fsmName)
    {
        NameArray = stateNames;
    }

    public override object Clone()
    {
        if (NameArray.Length > 0)
            return new StatePatch(NameArray, (Action<FsmState, object[]>)Patch.Clone(), (Action<FsmState, object[]>?)Unpatch?.Clone());
        return new StatePatch(Name, (Action<FsmState, object[]>)Patch.Clone(), (Action<FsmState, object[]>?)Unpatch?.Clone());
    }
}

/// <inheritdoc cref="StatePatchSet_Base{StatePatch}"/>
public class StatePatchSet(string objectName, List<StatePatch> patches, string fsmName = "")
    : StatePatchSet_Base<StatePatch>(objectName, patches, fsmName)
{
    /// <inheritdoc cref="StatePatchSet"/>
    /// <param name="stateNames"><see cref="Array" /> of names for the <see cref="FsmState" />s to be patched</param>
    public StatePatchSet(string[] objectNames, List<StatePatch> patches, string fsmName = "")
        : this("", patches, fsmName)
    {
        NameArray = objectNames;
    }

    public override object Clone()
    {
        List<StatePatch> newList = [];

        foreach (var patch in Patches)
            newList.Add(patch);

        if (NameArray.Length > 0)
            return new StatePatchSet(NameArray, newList, FSMName);
        return new StatePatchSet(Name, newList, FSMName);
    }
}

/// <summary>Contains a patch for a <see cref="FsmState" /></summary>
/// <param name="stateName">Name of the <see cref="FsmState" /> to patch</param>
/// <param name="patch">    The patch for adding the <see cref="FsmStateAction" /></param>
/// <param name="unpatch">  Optional: The patch for removing the <see cref="FsmStateAction" /></param>
/// <param name="fsmName">  Optional: Name of the <see cref="PlayMakerFSM" /> to patch, the first found
/// <see cref="PlayMakerFSM" /> is used if not provided</param>
public abstract class StatePatch_Base(string stateName, Action<FsmState, object[]> patch, Action<FsmState, object[]>? unpatch = null, string fsmName = "")
    : FSMPatch_Base<FsmState>(stateName, patch, unpatch, fsmName)
{
    /// <summary>Patches a <see cref="PlayMakerFSM" /></summary>
    /// <param name="fsmArray"><see cref="Array" /> that contains the <see cref="PlayMakerFSM" /> to patch</param>
    /// <param name="param">   An <see cref="Array" /> of arguments the patcher may want</param>
    public override void ApplyPatch(PlayMakerFSM[] fsmArray, object[] param)
    {
        var fsm = fsmArray.FirstOrDefault(obj => obj.FsmName.Equals(FSMName));
#if DEBUG
        if (fsm == default) CuteRandoCore.Log.LogWarning($"Patch for {(string)param[1]} is default, using first fsm. FSMName: {FSMName}");
        if (fsm != null && Name.Equals("Init") && !fsm.ActiveStateName.Equals("Init")) CuteRandoCore.Log.LogWarning($"Attempting to patch Init on {(string)param[1]} when the active state is {fsm.ActiveStateName}");
#endif

        ApplyPatch(fsm ?? fsmArray[0], param);
    }

    /// <inheritdoc cref="ApplyPatch"/>
    /// <param name="fsm">The <see cref="PlayMakerFSM" /> to patch</param>
    public override void ApplyPatch(PlayMakerFSM fsm, object[] param)
    {
        if (fsm == default)
        {
#if DEBUG
            CuteRandoCore.Log.LogWarning($"Patch for state {Name} has invalid FSMName ({FSMName}) specified, patch won't be applied.");
#endif
            return;
        }

        if (NameArray.Length > 0)
        {
            foreach (string name in NameArray)
            {
                ApplyPatch(fsm.FsmStates.FirstOrDefault(state => state.Name.Equals(name)), param);
            }
        }
        else
            ApplyPatch(fsm.FsmStates.FirstOrDefault(state => state.Name.Equals(Name)), param);
    }

    /// <summary>Patches an <see cref="FsmState" /></summary>
    /// <param name="state">The <see cref="FsmState" /> to patch</param>
    /// <param name="param">An <see cref="Array" /> of arguments the patcher may want</param>
    public void ApplyPatch(FsmState state, object[] param)
    {
        if (state == null)
        {
#if DEBUG
            CuteRandoCore.Log.LogWarning($"Patch for state {Name} has invalid state specified, patch won't be applied.");
#endif
            return;
        }

        Patch(state, param);
    }

    /// <summary>Removes patches from a <see cref="PlayMakerFSM" /></summary>
    /// <param name="fsmArray">Array that contains the <see cref="PlayMakerFSM" /> to remove patches from</param>
    /// <param name="param">   An <see cref="Array" /> of arguments the unpatcher may want</param>
    public override void RemovePatch(PlayMakerFSM[] fsmArray, object[] param)
    {
        if (Unpatch == null) return;

        RemovePatch(fsmArray.FirstOrDefault(obj => obj.FsmName.Equals(FSMName)) ?? fsmArray[0], param);
    }

    /// <inheritdoc cref="RemovePatch"/>
    /// <param name="fsm">The <see cref="PlayMakerFSM" /> to remove patches from</param>
    public override void RemovePatch(PlayMakerFSM fsm, object[] param)
    {
        if (Unpatch == null) return;

        if (fsm == default)
        {
#if DEBUG
            CuteRandoCore.Log.LogWarning($"Patch for state {Name} has invalid FSMName ({FSMName}) specified, patch won't be removed.");
#endif
            return;
        }

        if (NameArray.Length > 0)
        {
            foreach (string name in NameArray)
            {
                RemovePatch(fsm.FsmStates.FirstOrDefault(state => state.Name.Equals(name)), param);
            }
        }
        else
            RemovePatch(fsm.FsmStates.FirstOrDefault(state => state.Name.Equals(Name)), param);
    }

    /// <summary>Unpatches an <see cref="FsmState" /></summary>
    /// <param name="state">The <see cref="FsmState" /> to unpatch</param>
    /// <param name="param">An <see cref="Array" /> of arguments the unpatcher may want</param>
    public void RemovePatch(FsmState state, object[] param)
    {
        if (Unpatch == null) return;

        if (state == null)
        {
#if DEBUG
            CuteRandoCore.Log.LogWarning($"Patch for state {Name} has invalid state specified, patch won't be removed.");
#endif
            return;
        }

        Unpatch(state, param);
    }
}

/// <summary>Contains a set of State FSM patches for an object</summary>
/// /// <inheritdoc cref="FSMPatchSet_Base{PatchType, FsmState}"/>
public abstract class StatePatchSet_Base<PatchType>(string objectName, List<PatchType> patches, string fsmName = "")
    : FSMPatchSet_Base<PatchType, FsmState>(objectName, patches, fsmName)
    where PatchType : StatePatch_Base;

#endregion FSM State

#region FSM State Action

/// /// <inheritdoc cref="StateActionPatch_Base"/>
public class StateActionPatch(string stateName, Type actionType, Action<FsmStateAction, object[]> patch, Action<FsmStateAction, object[]>? unpatch = null, string fsmName = "")
    : StateActionPatch_Base(stateName, actionType, patch, unpatch, fsmName)
{
    /// <inheritdoc cref="StateActionPatch" />
    /// <param name="stateNames"><see cref="Array" /> of names for the <see cref="FsmState" />s to be patched</param>
    public StateActionPatch(string[] stateNames, Type actionType, Action<FsmStateAction, object[]> patch, Action<FsmStateAction, object[]>? unpatch = null, string fsmName = "")
        : this("", actionType, patch, unpatch, fsmName)
    {
        NameArray = stateNames;
    }

    public override object Clone()
    {
        if (NameArray.Length > 0)
            return new StateActionPatch(NameArray, Type, (Action<FsmStateAction, object[]>)Patch.Clone(), (Action<FsmStateAction, object[]>?)Unpatch?.Clone());
        return new StateActionPatch(Name, Type, (Action<FsmStateAction, object[]>)Patch.Clone(), (Action<FsmStateAction, object[]>?)Unpatch?.Clone());
    }
}

/// <summary>Contains a set of <see cref="StateActionPatch"/>es</summary>
/// <inheritdoc cref="StateActionPatchSet_Base{StateActionPatch}" />
public class StateActionPatchSet(string objectName, List<StateActionPatch> patches, string fsmName = "")
    : StateActionPatchSet_Base<StateActionPatch>(objectName, patches, fsmName)
{
    /// <inheritdoc cref="StateActionPatchSet"/>
    /// <param name="objectNames"><see cref="Array" /> of names for <see cref="GameObject" />s to patch</param>
    public StateActionPatchSet(string[] objectNames, List<StateActionPatch> patches, string fsmName = "")
        : this("", patches, fsmName)
    {
        NameArray = objectNames;
    }

    public override object Clone()
    {
        List<StateActionPatch> newList = [];

        foreach (var patch in Patches)
            newList.Add(patch);

        if (NameArray.Length > 0)
            return new StateActionPatchSet(NameArray, newList, FSMName);
        return new StateActionPatchSet(Name, newList, FSMName);
    }
}

/// <summary>Contains a patch for an <see cref="FsmStateAction" /> in an <see cref="FsmState" /></summary>
/// <inheritdoc cref="FSMPatch_Base{FsmStateAction}"/>
/// <param name="stateName"> Name of the <see cref="FsmState" /> to patch</param>
/// <param name="actionType">The <see cref="Type" /> of <see cref="FsmStateAction" /> being patched</param>
public abstract class StateActionPatch_Base(string stateName, Type actionType, Action<FsmStateAction, object[]> patch, Action<FsmStateAction, object[]>? unpatch = null, string fsmName = "")
    : FSMPatch_Base<FsmStateAction>(stateName, patch, unpatch, fsmName), IFSMActionPatch
{
    public Type Type => actionType;

    public string TypeString => Type.ToString();

    /// <summary>Patches a <see cref="PlayMakerFSM" /></summary>
    /// <param name="fsmArray"><see cref="Array" /> that contains the <see cref="PlayMakerFSM" /> to patch</param>
    /// <param name="param">   An <see cref="Array" /> of arguments the patcher may want</param>
    public override void ApplyPatch(PlayMakerFSM[] fsmArray, object[] param)
    {
        ApplyPatch(fsmArray.FirstOrDefault(obj => obj.FsmName.Equals(FSMName)) ?? fsmArray[0], param);
    }

    /// <inheritdoc cref="ApplyPatch"/>
    /// <param name="fsm">The <see cref="PlayMakerFSM" /> to patch</param>
    public override void ApplyPatch(PlayMakerFSM fsm, object[] param)
    {
        bool typeFound = false;

        if (NameArray.Length > 0)
        {
            foreach (string name in NameArray)
                DoPatch(name);
        }
        else
        {
            DoPatch(Name);
        }

        void DoPatch(string name)
        {
            var state = fsm.FsmStates.FirstOrDefault(state => state.Name.Equals(name));
            if (state != null)
            {
                foreach (FsmStateAction action in state.Actions)
                {
                    if (action.GetType().ToString().Equals(TypeString))
                    {
                        Patch(action, param);
                        typeFound = true;
                    }
                }
            }

            if (!typeFound)
                CuteRandoCore.Log.LogWarning($"Patch failed for State {Name}, Type: {TypeString} FsmName: {FSMName}");
        }
    }

    /// <summary>Removes patches from a <see cref="PlayMakerFSM" /></summary>
    /// <param name="fsmArray">Array that contains the <see cref="PlayMakerFSM" /> to remove patches from</param>
    /// <param name="param">   An <see cref="Array" /> of arguments the unpatcher may want</param>
    public override void RemovePatch(PlayMakerFSM[] fsmArray, object[] param)
    {
        if (Unpatch == null) return;

        RemovePatch(fsmArray.FirstOrDefault(obj => obj.FsmName.Equals(FSMName)) ?? fsmArray[0], param);
    }

    /// <inheritdoc cref="RemovePatch"/>
    /// <param name="fsm">The <see cref="PlayMakerFSM" /> to remove patches from</param>
    public override void RemovePatch(PlayMakerFSM fsm, object[] param)
    {
        if (Unpatch == null) return;

        if (NameArray.Length > 0)
        {
            foreach (string name in NameArray)
                DoRemove(name);
        }
        else
            DoRemove(Name);

        void DoRemove(string name)
        {
            var state = fsm.FsmStates.FirstOrDefault(state => state.Name.Equals(name));
            if (state != null)
            {
                foreach (FsmStateAction action in state.Actions)
                {
                    if (action.GetType().ToString().Equals(TypeString))
                    {
                        Unpatch(action, param);
                    }
                }
            }
        }
    }
}

/// <summary>Contains a set of state action patches for an object</summary>
/// <inheritdoc cref="FSMPatchSet_Base{PatchType, FsmStateAction}"/>
public abstract class StateActionPatchSet_Base<PatchType>(string enemyName, List<PatchType> patches, string fsmName = "")
    : FSMPatchSet_Base<PatchType, FsmStateAction>(enemyName, patches, fsmName)
    where PatchType : StateActionPatch_Base;

#endregion FSM State Action

#region FSM Patch

/// <summary>Abstract class for containing an FSM patch</summary>
/// <inheritdoc cref="ObjectPatch_Base{PatchTarget, PlayMakerFSM}"/>
/// <typeparam name="PatchTarget">The type of FSM part this is patching</typeparam>
/// <param name="fsmPart">        The name of the FSM part being patched</param>
/// <param name="fsmName">        Optional: Name of the <see cref="PlayMakerFSM" /> to patch, the first found
/// <see cref="PlayMakerFSM" /> is used if not provided</param>
public abstract class FSMPatch_Base<PatchTarget>(string fsmPart, Action<PatchTarget, object[]> patch, Action<PatchTarget, object[]>? unpatch = null, string fsmName = "")
    : ObjectPatch_Base<PatchTarget, PlayMakerFSM>(fsmPart, patch, unpatch), IFSMPatch
{
    public string FSMName { get => fsmName; set => fsmName = value; }
}

/// <summary>Abstract class for containing a set of FSM Patches</summary>
/// <inheritdoc cref="ObjectPatchSet_Base{PatchType, PlayMakerFSM, PatchTarget}"/>
/// <typeparam name="PatchType">  The type of FSM Patch</typeparam>
/// <typeparam name="PatchTarget">The type of FSM Object this is patching</typeparam>
/// <param name="enemyName">     The name of the enemy being patched</param>
/// <param name="fsmName">   Optional: Name of the <see cref="PlayMakerFSM" /> to patch, the first found
/// <see cref="PlayMakerFSM" /> is used if not provided</param>
public abstract class FSMPatchSet_Base<PatchType, PatchTarget>(string enemyName, List<PatchType> patches, string fsmName = "")
    : ObjectPatchSet_Base<PatchType, PlayMakerFSM, PatchTarget>(enemyName, patches), IFSMPatch
    where PatchType : FSMPatch_Base<PatchTarget>
{
    public string FSMName { get => fsmName; set => fsmName = value; }
}

#endregion FSM Patch

/// <summary>Marks a patch as an FSM Patch</summary>
public interface IFSMPatch
{
    /// <summary>The name of the <see cref="PlayMakerFSM" /> the patch is for</summary>
    public string FSMName { get; set; }
}

/// <summary>Marks a patch as an FSM Action Patch</summary>
public interface IFSMActionPatch
{
    /// <summary>The <see cref="System.Type" /> of the action the patch is for</summary>
    public Type Type { get; }

    /// <summary>The <see cref="System.Type" /> of the action the patch is for as a <see langword="string" /></summary>
    public string TypeString => Type.ToString();
}
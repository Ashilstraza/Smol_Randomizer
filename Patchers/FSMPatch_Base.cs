using BepInEx;

using HarmonyLib;

using HutongGames.PlayMaker;

using Smol_Randomizer.Patchers.Enemy;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

using UnityEngine;

namespace Smol_Randomizer.Patchers;

#region FSM State

/// <inheritdoc cref="StatePatch_Base"/>
/// <param name="objectNames"><see cref="Array" /> of <see cref="GameObject" /> names to patch</param>
/// <param name="stateNames"><see cref="Array" /> of <see cref="FsmState" /> names to be patched</param>
public class StatePatch(string objectName, string stateName, Action<FsmState, object[]> patch, Action<FsmState, object[]>? unpatch = null, string fsmName = "")
    : StatePatch_Base(objectName, stateName, patch, unpatch, fsmName)
{
    /// <inheritdoc cref="StatePatch"/>
    public StatePatch(string objectName, string[] stateNames, Action<FsmState, object[]> patch, Action<FsmState, object[]>? unpatch = null, string fsmName = "")
        : this(objectName, "", patch, unpatch, fsmName)
    {
        StateArray = stateNames;
    }

    /// <inheritdoc cref="StatePatch" />
    public StatePatch(string[] objectNames, string stateName, Action<FsmState, object[]> patch, Action<FsmState, object[]>? unpatch = null, string fsmName = "")
        : this("", stateName, patch, unpatch, fsmName)
    {
        NameArray = objectNames;
    }

    /// <inheritdoc cref="StatePatch" />
    public StatePatch(string[] objectNames, string[] stateNames, Action<FsmState, object[]> patch, Action<FsmState, object[]>? unpatch = null, string fsmName = "")
        : this("", "", patch, unpatch, fsmName)
    {
        StateArray = stateNames;
        NameArray = objectNames;
    }

    public override object Clone()
    {
        if (NameArray.Length > 0 && StateArray.Length > 0)
            return new StatePatch(NameArray, StateArray, (Action<FsmState, object[]>)Patch.Clone(), (Action<FsmState, object[]>?)Unpatch?.Clone(), FSMName);
        else if (NameArray.Length > 0)
            return new StatePatch(NameArray, State, (Action<FsmState, object[]>)Patch.Clone(), (Action<FsmState, object[]>?)Unpatch?.Clone(), FSMName);
        else if (StateArray.Length > 0)
            return new StatePatch(Name, StateArray, (Action<FsmState, object[]>)Patch.Clone(), (Action<FsmState, object[]>?)Unpatch?.Clone(), FSMName);
        else
            return new StatePatch(Name, State, (Action<FsmState, object[]>)Patch.Clone(), (Action<FsmState, object[]>?)Unpatch?.Clone(), FSMName);
    }
}

/// <inheritdoc cref="StatePatchSet_Base{StatePatch}"/>
public class StatePatchSet : StatePatchSet_Base<StatePatch>
{
    /// <inheritdoc cref="StatePatchSet"/>
    public StatePatchSet(string objectName, List<StatePatch> patches, string fsmName = "") 
        : base(objectName, patches, fsmName)
        => PopulateChildFields();

    /// <inheritdoc cref="StatePatchSet"/>
    /// <param name="objectNames"><see cref="Array" /> of <see cref="GameObject" /> names to patch</param>
    public StatePatchSet(string[] objectNames, List<StatePatch> patches, string fsmName = "")
        : this("", patches, fsmName)
    {
        NameArray = objectNames;

        PopulateChildFields();
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
/// <inheritdoc cref="FSMPatch_Base{FsmStateAction}"/>
/// <param name="stateName">Name of the <see cref="FsmState" /> to patch</param>
public abstract class StatePatch_Base(string objectName, string stateName, Action<FsmState, object[]> patch, Action<FsmState, object[]>? unpatch = null, string fsmName = "")
    : FSMPatch_Base<FsmState>(objectName, stateName, patch, unpatch, fsmName)
{
    public override void ApplyPatch(FsmState state, object[] param)
    {
        if (state == null)
        {
            CuteRandoCore.DebugLog($"Patch on {(Name is "" ? ((string)param[1]) : Name)} for {State} was called with a null state, patch won't be applied.", DebugLogOptions.Error, true);
            return;
        }

        Patch(state, param);
    }

    public override void RemovePatch(FsmState state, object[] param)
    {
        if (Unpatch == null) return;

        if (state == null)
        {
            CuteRandoCore.DebugLog($"Unpatch on {(Name is "" ? ((string)param[1]) : Name)} for {State} was called with a null state, patch won't be removed.", DebugLogOptions.Error, true);
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

/// <inheritdoc cref="StateActionPatch_Base"/>
/// <param name="objectNames"><see cref="Array" /> of <see cref="GameObject" /> names to patch</param>
/// <param name="stateNames"><see cref="Array" /> of <see cref="FsmState" /> names to be patched</param>
public class StateActionPatch(string objectName, string stateName, Type actionType, Action<FsmStateAction, object[]> patch, Action<FsmStateAction, object[]>? unpatch = null, string fsmName = "")
    : StateActionPatch_Base(objectName, stateName, actionType, patch, unpatch, fsmName)
{
    /// <inheritdoc cref="StateActionPatch" />
    public StateActionPatch(string objectName, string[] stateNames, Type actionType, Action<FsmStateAction, object[]> patch, Action<FsmStateAction, object[]>? unpatch = null, string fsmName = "")
        : this(objectName, "", actionType, patch, unpatch, fsmName)
    {
        StateArray = stateNames;
    }

    /// <inheritdoc cref="StateActionPatch" />
    public StateActionPatch(string[] objectNames, string stateName, Type actionType, Action<FsmStateAction, object[]> patch, Action<FsmStateAction, object[]>? unpatch = null, string fsmName = "")
        : this("", stateName, actionType, patch, unpatch, fsmName)
    {
        NameArray = objectNames;
    }

    /// <inheritdoc cref="StateActionPatch" />
    public StateActionPatch(string[] objectNames, string[] stateNames, Type actionType, Action<FsmStateAction, object[]> patch, Action<FsmStateAction, object[]>? unpatch = null, string fsmName = "")
        : this("", "", actionType, patch, unpatch, fsmName)
    {
        StateArray = stateNames;
        NameArray = objectNames;
    }

    public override object Clone()
    {
        if (NameArray.Length > 0 && StateArray.Length > 0)
            return new StateActionPatch(NameArray, StateArray, Type, (Action<FsmStateAction, object[]>)Patch.Clone(), (Action<FsmStateAction, object[]>?)Unpatch?.Clone(), FSMName);
        else if (NameArray.Length > 0)
            return new StateActionPatch(NameArray, State, Type, (Action<FsmStateAction, object[]>)Patch.Clone(), (Action<FsmStateAction, object[]>?)Unpatch?.Clone(), FSMName);
        else if(StateArray.Length > 0)
            return new StateActionPatch(Name, StateArray, Type, (Action<FsmStateAction, object[]>)Patch.Clone(), (Action<FsmStateAction, object[]>?)Unpatch?.Clone(), FSMName);
        else
            return new StateActionPatch(Name, State, Type, (Action<FsmStateAction, object[]>)Patch.Clone(), (Action<FsmStateAction, object[]>?)Unpatch?.Clone(), FSMName);
    }
}

/// <inheritdoc cref="StateActionPatchSet_Base{StateActionPatch}" />
public class StateActionPatchSet : StateActionPatchSet_Base<StateActionPatch>
{
    /// <inheritdoc cref="StateActionPatchSet"/>
    public StateActionPatchSet(string objectName, List<StateActionPatch> patches, string fsmName = "") : base(objectName, patches, fsmName)
        => PopulateChildFields();

    /// <inheritdoc cref="StateActionPatchSet"/>
    /// <param name="objectNames"><see cref="Array" /> of <see cref="GameObject" /> names to patch</param>
    public StateActionPatchSet(string[] objectNames, List<StateActionPatch> patches, string fsmName = "")
        : this("", patches, fsmName)
    {
        NameArray = objectNames;

        PopulateChildFields();
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
public abstract class StateActionPatch_Base(string objectName, string stateName, Type actionType, Action<FsmStateAction, object[]> patch, Action<FsmStateAction, object[]>? unpatch = null, string fsmName = "")
    : FSMPatch_Base<FsmStateAction>(objectName, stateName, patch, unpatch, fsmName), IFSMActionPatch
{
    public Type Type => actionType;

    public string TypeString => Type.ToString();

    public override void ApplyPatch(FsmState state, object[] param)
    {
        if (state == null)
        {
            CuteRandoCore.DebugLog($"Patch on {(Name is "" ? ((string)param[1]) : Name)} for {State} was called with a null state, patch won't be applied.", DebugLogOptions.Error, true);
            return;
        }

        bool typeFound = false;

        foreach (FsmStateAction action in state.Actions)
        {
            if (action.GetType().ToString().Equals(TypeString))
            {
                Patch(action, param);
                typeFound = true;
            }
        }

        if (!typeFound)
            CuteRandoCore.DebugLog($"Patch on {(Name is "" ? ((string)param[1]) : Name)} for {State} failed; Type: {TypeString}, FsmName: {FSMName}", DebugLogOptions.Warning);
    }

    public override void RemovePatch(FsmState state, object[] param)
    {
        if (Unpatch == null) return;

        if (state == null)
        {
            CuteRandoCore.DebugLog($"Unpatch on {(Name is "" ? ((string)param[1]) : Name)} for {State} was called with a null state, patch won't be removed.", DebugLogOptions.Error, true);
            return;
        }

        bool typeFound = false;

        foreach (FsmStateAction action in state.Actions)
        {
            if (action.GetType().ToString().Equals(TypeString))
            {
                Unpatch(action, param);
                typeFound = true;
            }
        }

        if (!typeFound)
            CuteRandoCore.DebugLog($"Unpatch on {(Name is "" ? ((string)param[1]) : Name)} for {State} failed; Type: {TypeString}, FsmName: {FSMName}", DebugLogOptions.Warning);
    }
}

/// <summary>Contains a set of state action patches for an object</summary>
/// <inheritdoc cref="FSMPatchSet_Base{PatchType, FsmStateAction}"/>
public abstract class StateActionPatchSet_Base<PatchType>(string objectName, List<PatchType> patches, string fsmName = "")
    : FSMPatchSet_Base<PatchType, FsmStateAction>(objectName, patches, fsmName)
    where PatchType : StateActionPatch_Base;

#endregion FSM State Action

#region FSM Patch

/// <summary>Patches a <see cref="GameObject" />'s <see cref="PlayMakerFSM" /></summary>
/// <inheritdoc cref="FSMPatch_Base{PlayMakerFSM}"/>
public class FSMPatch(string objectName, Action<PlayMakerFSM, object[]> patch, Action<PlayMakerFSM, object[]>? unpatch = null, string fsmName = "")
    : FSMPatch_Base<PlayMakerFSM>(objectName, "FSM", patch, unpatch, fsmName)
{
    /// <inheritdoc cref="FSMPatch"/>
    /// <param name="objectNames"><see cref="Array" /> of <see cref="GameObject" /> names to patch</param>
    public FSMPatch(string[] objectNames, Action<PlayMakerFSM, object[]> patch, Action<PlayMakerFSM, object[]>? unpatch = null, string fsmName = "")
        : this("", patch, unpatch, fsmName)
    {
        NameArray = objectNames;
    }

    private string[] states = [];

    public override string[] StateArray { get => states; set => _ = value; }

    public override void ApplyPatch(PlayMakerFSM fsm, object[] param)
    {
        if (fsm == null)
        {
            CuteRandoCore.DebugLog($"Patch on {(Name is "" ? ((string)param[1]) : Name)} for FSM was called with a null FSM, patch won't be applied.", DebugLogOptions.Error, true);
            return;
        }

        Patch(fsm, param);
    }

    public override void RemovePatch(PlayMakerFSM fsm, object[] param)
    {
        if (Unpatch == null) return;

        if (fsm == null)
        {
            CuteRandoCore.DebugLog($"Unpatch on {(Name is "" ? ((string)param[1]) : Name)} for FSM was called with a null FSM, patch won't be removed.", DebugLogOptions.Error, true);
            return;
        }

        Unpatch(fsm, param);
    }

    public override object Clone()
    {
        if (NameArray.Length > 0)
            return new FSMPatch(NameArray, (Action<PlayMakerFSM, object[]>)Patch.Clone(), (Action<PlayMakerFSM, object[]>?)Unpatch?.Clone(), FSMName);
        else
            return new FSMPatch(NameArray, (Action<PlayMakerFSM, object[]>)Patch.Clone(), (Action<PlayMakerFSM, object[]>?)Unpatch?.Clone(), FSMName);
    }

#pragma warning disable CS0809 // Obsolete member overrides non-obsolete member
    [Obsolete("FSMPatch does not operate on FsmStates", true)]
    public override void ApplyPatch(FsmState state, object[] param)
    {
        throw new NotImplementedException("FSMPatch does not operate on FsmStates");
    }

    [Obsolete("FSMPatch does not operate on FsmStates", true)]
    public override void RemovePatch(FsmState state, object[] param)
    {
        throw new NotImplementedException("FSMPatch does not operate on FsmStates");
    }
#pragma warning restore CS0809 // Obsolete member overrides non-obsolete member
}

/// <summary>Contains a set of <see cref="FSMPatch"/>es</summary>
public class FSMPatchSet : FSMPatchSet_Base<FSMPatch, PlayMakerFSM>
{
    /// <inheritdoc cref="FSMPatchSet"/>
    /// <inheritdoc cref="FSMPatch_Base{FSMPatch}"/>
    /// <inheritdoc cref="ObjectPatchSet_Base{PatchType, PlayMakerFSM, PatchTarget}"/>
    public FSMPatchSet(string objectName, List<FSMPatch> patches, string fsmName = "") : base(objectName, patches, fsmName)
        => PopulateChildFields();

    /// <inheritdoc cref="FSMPatchSet"/>
    /// <inheritdoc cref="FSMPatch_Base{FSMPatch}"/>
    /// <inheritdoc cref="ObjectPatchSet_Base{PatchType, PlayMakerFSM, PatchTarget}"/>
    /// <param name="objectNames"><see cref="Array" /> of <see cref="GameObject" /> names to patch</param>
    public FSMPatchSet(string[] objectNames, List<FSMPatch> patches, string fsmName = "")
        : this("", patches, fsmName)
    {
        NameArray = objectNames;

        PopulateChildFields();
    }

    public override object Clone()
    {
        List<FSMPatch> newList = [];

        foreach (var patch in Patches)
            newList.Add(patch);

        if (NameArray.Length > 0)
            return new FSMPatchSet(NameArray, newList, FSMName);
        return new FSMPatchSet(Name, newList, FSMName);
    }
}

/// <summary>Abstract class for containing an FSM patch</summary>
/// <inheritdoc cref="ObjectPatch_Base{PatchTarget, PlayMakerFSM}"/>
/// <typeparam name="PatchTarget">The type of FSM part this is patching</typeparam>
/// <param name="state">        The name of the FSM part being patched</param>
/// <param name="fsmName">        Optional: Name of the <see cref="PlayMakerFSM" /> to patch, the first found
/// <see cref="PlayMakerFSM" /> is used if not provided</param>
public abstract class FSMPatch_Base<PatchTarget>(string objectName, string state, Action<PatchTarget, object[]> patch, Action<PatchTarget, object[]>? unpatch = null, string fsmName = "")
    : ObjectPatch_Base<PatchTarget, PlayMakerFSM>(objectName, patch, unpatch), IFSMPatch, IFSMState
{
    public string FSMName { get => fsmName; set => fsmName = value; }
    public string State { get => state; set => state = value; }
    public virtual string[] StateArray { get; set; } = [];

    /// <summary>Patches a <see cref="PlayMakerFSM" /></summary>
    /// <param name="fsmArray"><see cref="Array" /> that contains the <see cref="PlayMakerFSM" /> to patch</param>
    /// <param name="param">   An <see cref="Array" /> of arguments the patcher may want</param>
    public override void ApplyPatch(PlayMakerFSM[] fsmArray, object[] param)
    {
        var fsm = fsmArray.FirstOrDefault(obj => obj.FsmName.Equals(FSMName));

        if (FSMName.IsNullOrWhiteSpace())
            CuteRandoCore.DebugLog($"No FSM Name provided for patch on {(Name is "" ? ((string)param[1]) : Name)} for {(StateArray.Length > 0 ? StateArray : State)}! Defaulting to first FSM found.", DebugLogOptions.Warning, true);

        if (fsm != null && State.Equals("Init") && !InitialStateOk(fsm.ActiveStateName))
            CuteRandoCore.DebugLog($"Attempting to patch Init on {(Name is "" ? ((string)param[1]) : Name)} when the active state is {fsm.ActiveStateName}", DebugLogOptions.Warning);

        ApplyPatch(fsm ?? fsmArray[0], param);
    }

    /// <inheritdoc cref="ApplyPatch"/>
    /// <param name="fsm">The <see cref="PlayMakerFSM" /> to patch</param>
    public override void ApplyPatch(PlayMakerFSM fsm, object[] param)
    {
        if (fsm == null)
        {
            CuteRandoCore.DebugLog($"Patch on {(Name is "" ? ((string)param[1]) : Name)} for {State} was called with a null FSM, patch won't be applied.", DebugLogOptions.Error, true);
            return;
        }

        if (StateArray.Length > 0)
        {
            foreach (string name in StateArray)
            {
                ApplyPatch(fsm.FsmStates.FirstOrDefault(state => state.Name.Equals(name)), param);
            }
        }
        else
            ApplyPatch(fsm.FsmStates.FirstOrDefault(state => state.Name.Equals(State)), param);
    }

    /// <summary>Patches an <see cref="FsmState" /></summary>
    /// <param name="state">The <see cref="FsmState" /> to patch</param>
    /// <param name="param">An <see cref="Array" /> of arguments the patcher may want</param>
    public abstract void ApplyPatch(FsmState state, object[] param);

    /// <summary>Removes patches from a <see cref="PlayMakerFSM" /></summary>
    /// <param name="fsmArray"><see cref="Array" /> that contains the <see cref="PlayMakerFSM" /> to remove patches from</param>
    /// <param name="param">   An <see cref="Array" /> of arguments the unpatcher may want</param>
    public override void RemovePatch(PlayMakerFSM[] fsmArray, object[] param)
    {
        if (Unpatch == null) return;

        var fsm = fsmArray.FirstOrDefault(obj => obj.FsmName.Equals(FSMName));

        if (FSMName.IsNullOrWhiteSpace())
            CuteRandoCore.DebugLog($"No FSM Name provided for unpatch on {(Name is "" ? ((string)param[1]) : Name)} for {(StateArray.Length > 0 ? StateArray : State)}! Defaulting to first FSM found.", DebugLogOptions.Warning, true);

        RemovePatch(fsm ?? fsmArray[0], param);
    }

    /// <inheritdoc cref="RemovePatch"/>
    /// <param name="fsm">The <see cref="PlayMakerFSM" /> to remove patches from</param>
    public override void RemovePatch(PlayMakerFSM fsm, object[] param)
    {
        if (Unpatch == null) return;

        if (fsm == null)
        {
            CuteRandoCore.DebugLog($"Unpatch on {(Name is "" ? ((string)param[1]) : Name)} for {State} was called with a null FSM, patch won't be applied.", DebugLogOptions.Error, true);
            return;
        }

        if (StateArray.Length > 0)
        {
            foreach (string name in NameArray)
            {
                RemovePatch(fsm.FsmStates.FirstOrDefault(state => state.Name.Equals(name)), param);
            }
        }
        else
            RemovePatch(fsm.FsmStates.FirstOrDefault(state => state.Name.Equals(State)), param);
    }

    /// <summary>Removes patches from an <see cref="FsmState" /></summary>
    /// <param name="state">The <see cref="FsmState" /> to remove patches from</param>
    /// <param name="param">An <see cref="Array" /> of arguments the unpatcher may want</param>
    public abstract void RemovePatch(FsmState state, object[] param);

    /// <summary>Checks to see if the initial <see cref="PlayMakerFSM"/> <see cref="FsmState" /> is the same or before the <see cref="FsmState" /> that is to be patched</summary>
    /// <remarks>This just checks a switch statement for known states that are at the very start of most <see cref="PlayMakerFSM"/>s</remarks>
    /// <param name="activeStateName">The active <see cref="FsmState" /></param>
    /// <returns><see langword="true"/> If the initial <see cref="FsmState" /> is before or the same state, <see langword="false"/> if it is not and the patch will possibly not be encountered</returns>
    protected bool InitialStateOk(string activeStateName)
    {
        switch (activeStateName)
        {
            case "Init":
            case "Pause":
            case "RetreatAway Range?":
            case "Configure Tele":
                return true;
            default:
                return false;
        }
    }
}

/// <summary>Abstract class for containing a set of FSM Patches</summary>
/// <inheritdoc cref="ObjectPatchSet_Base{PatchType, PlayMakerFSM, PatchTarget}"/>
/// <typeparam name="PatchType">  The type of FSM Patch</typeparam>
/// <typeparam name="PatchTarget">The type of FSM Object this is patching</typeparam>
/// <param name="objectName">     The name of the enemy being patched</param>
/// <param name="fsmName">   Optional: Name of the <see cref="PlayMakerFSM" /> to patch, the first found
/// <see cref="PlayMakerFSM" /> is used if not provided</param>
public abstract class FSMPatchSet_Base<PatchType, PatchTarget>(string objectName, List<PatchType> patches, string fsmName = "")
    : ObjectPatchSet_Base<PatchType, PlayMakerFSM, PatchTarget>(objectName, patches), IFSMPatchSet
    where PatchType : FSMPatch_Base<PatchTarget>
{
    public string FSMName { get => fsmName; set => fsmName = value; }

    internal override void PopulateChildFields()
    {
        foreach (var patch in Patches)
        {
            if (patch.FSMName is "")
                patch.FSMName = FSMName;
            if (patch.Name is "")
                patch.Name = Name;
            if (patch.NameArray is null or [])
                patch.NameArray = NameArray;
        }
    }
}

/// <summary>Abstract class for Enemy FSM Patch Collections</summary>
/// <typeparam name="PatchType">   The type of FSM Patch</typeparam>
/// <typeparam name="PatchSetType">The type of FSM Patch Set</typeparam>
/// <typeparam name="PatchTarget"> The type of FSM Object we are targeting (<see cref="FsmStateAction"/>
/// or <see cref="FsmState">)</typeparam>
public abstract class FSMPatchCollections_Base<PatchType, PatchSetType, PatchTarget>
    : ObjectPatchCollection_Base<PatchType, PatchSetType, PlayMakerFSM, PatchTarget>
    where PatchType : FSMPatch_Base<PatchTarget>, IFSMPatch
    where PatchSetType : FSMPatchSet_Base<PatchType, PatchTarget>, IFSMPatchSet
{
    /// <summary>Applies the registered patches.</summary>
    /// <param name="objectName">  Name of the enemy to patch</param>
    /// <param name="enemyObject">The <see cref="GameObject" /> of the enemy to patch</param>
    public bool ApplyPatches(string objectName, GameObject enemyObject)
    {
        if (Patches.TryGetValue(objectName, out HashSet<PatchType> patchSet))
        {
            foreach (var setPatch in patchSet)
            {
                setPatch.ApplyPatch(enemyObject.GetComponents<PlayMakerFSM>(), [enemyObject, objectName]);
            }

            return true;
        }

        return false;
    }

    /// <summary>Removes the registered patches.</summary>
    /// <param name="objectName">  Name of the enemy to unpatch</param>
    /// <param name="enemyObject">The <see cref="GameObject" /> of the enemy to unpatch</param>
    public bool RemovePatches(string objectName, GameObject enemyObject)
    {
        if (Patches.TryGetValue(objectName, out HashSet<PatchType> patchSet))
        {
            foreach (var setPatch in patchSet)
            {
                if (enemyObject == null)
                    continue;
                setPatch.RemovePatch(enemyObject.GetComponents<PlayMakerFSM>(), [enemyObject, objectName]);

                return true;
            }
        }

        return false;
    }
}

#endregion FSM Patch

/// <summary>Marks a patch as an FSM Patch</summary>
public interface IFSMPatch : ISmolPatch
{
    /// <summary>The name of the <see cref="PlayMakerFSM" /> the patch is for</summary>
    public string FSMName { get; set; }
}

public interface IFSMPatchSet : IFSMPatch, ISmolPatchSet;

/// <summary>Marks a patch as an FSM Action Patch</summary>
public interface IFSMActionPatch : IFSMPatch, IFSMState
{
    /// <summary>The <see cref="System.Type" /> of the action the patch is for</summary>
    public Type Type { get; }

    /// <summary>The <see cref="System.Type" /> of the action the patch is for as a <see langword="string" /></summary>
    public string TypeString => Type.ToString();
}

public interface IFSMState
{
    /// <summary>The name of the FSM part being patched</summary>
    public string State { get; set; }

    public string[] StateArray { get; set; }
}
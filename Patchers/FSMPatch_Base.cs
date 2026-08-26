using System;
using System.Collections.Generic;
using System.Linq;

using HutongGames.PlayMaker;

namespace Smol_Randomizer.Patchers;
#region FSM State
public class StatePatch(string stateName, Action<FsmState, object[]?> patch, Action<FsmState, object[]?>? unpatch = null, string fsmName = "")
    : StatePatch_Base(stateName, patch, unpatch, fsmName)
{
    public StatePatch(string[] stateNames, Action<FsmState, object[]?> patch, Action<FsmState, object[]?>? unpatch = null, string fsmName = "")
        : this("", patch, unpatch, fsmName)
    {
        NameArray = stateNames;
    }
}

public class StatePatchSet(string objectName, List<StatePatch> patches, string fsmName = "")
    : StatePatchSet_Base<StatePatch>(objectName, patches, fsmName)
{
    public StatePatchSet(string[] objectNames, List<StatePatch> patches, string fsmName = "")
        : this("", patches, fsmName)
    {
        NameArray = objectNames;
    }
}

/// <summary>
/// Contains a patch for an FSM state
/// </summary>
/// <param name="stateName">Name of the state the patch is for</param>
/// <param name="patch">The patch for the state</param>
/// <param name="unpatch">Optional </param>
/// <param name="fsmName">Optional name of the FSM the patch is for, used when FSMStatePatchSet.ApplyPatches is called with an array of FSMs</param>
public abstract class StatePatch_Base(string stateName, Action<FsmState, object[]?> patch, Action<FsmState, object[]?>? unpatch = null, string fsmName = "")
    : FSMPatch_Base<FsmState>(stateName, patch, unpatch)
{
    /// <summary>
    /// Patches an FSM, will ignore Name if NameArray is populated
    /// </summary>
    /// <param name="fsmArray">Array that contains the FSM to patch</param>
    /// <param name="param">An array of arguments the patcher may want</param>
    public override void ApplyPatch(PlayMakerFSM[] fsmArray, object[]? param = null)
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

    /// <summary>
    /// 
    /// </summary>
    /// <param name="fsmArray"></param>
    /// <param name="param"></param>
    public override void RemovePatch(PlayMakerFSM[] fsmArray, object[]? param = null)
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
}

/// <summary>
/// Contains a set of state patches for an object
/// </summary>
/// <param name="objectName">The name of the object the patches are for</param>
/// <param name="patches">The list of patches for the object</param>
/// <param name="fsmName">Optional: The name of the FSM the patch is for, used when FSMStatePatchSet.ApplyPatches is called with an array of FSMs</param>
public abstract class StatePatchSet_Base<PatchType>(string objectName, List<PatchType> patches, string fsmName = "")
    : FSMPatchSet_Base<PatchType, FsmState>(objectName, patches, fsmName)
    where PatchType : StatePatch_Base;
#endregion

#region FSM State Action
public class StateActionPatch(string stateName, Type actionType, Action<FsmStateAction, object[]?> patch, Action<FsmStateAction, object[]?>? unpatch = null, string fsmName = "")
    : StateActionPatch_Base(stateName, actionType, patch, unpatch, fsmName)
{
    public StateActionPatch(string[] stateNames, Type actionType, Action<FsmStateAction, object[]?> patch, Action<FsmStateAction, object[]?>? unpatch = null, string fsmName = "")
        : this("", actionType, patch, unpatch, fsmName)
    {
        NameArray = stateNames;
    }
}

public class StateActionPatchSet(string objectName, List<StateActionPatch> patches, string fsmName = "")
    : StateActionPatchSet_Base<StateActionPatch>(objectName, patches, fsmName)
{
    public StateActionPatchSet(string[] objectNames, List<StateActionPatch> patches, string fsmName = "")
        : this("", patches, fsmName)
    {
        NameArray = objectNames;
    }
}

/// <summary>
/// Contains a patch for an action in an FSM state
/// </summary>
/// <param name="stateName">Name of the state the patch is for</param>
/// <param name="actionType">The type of the action the patch is for</param>
/// <param name="patch">The patch for the action</param>
/// <param name="fsmName">Optional: The name of the FSM the patch is for, used when FSMStatePatchSet.ApplyPatches is called with an array of FSMs</param>
public abstract class StateActionPatch_Base(string stateName, Type actionType, Action<FsmStateAction, object[]?> patch, Action<FsmStateAction, object[]?>? unpatch = null, string fsmName = "")
    : FSMPatch_Base<FsmStateAction>(stateName, patch, unpatch), IFSMActionPatch
{
    /// <summary>
    /// >The type of the action the patch is for
    /// </summary>
    public Type Type => actionType;

    /// <summary>
    /// The type of the action the patch is for as a string
    /// </summary>
    public string TypeString => Type.ToString();

    /// <summary>
    /// Patches an FSM
    /// </summary>
    /// <param name="fsmArray">Array that contains the FSM to patch</param>
    /// <param name="param">An array of arguments the patcher may want</param>
    public override void ApplyPatch(PlayMakerFSM[] fsmArray, object[]? param = null)
    {
        ApplyPatch(fsmArray.FirstOrDefault(obj => obj.name.Equals(fsmName)), param);
    }

    /// <summary>
    /// Patches an FSM
    /// </summary>
    /// <param name="fsm">The FSM to patch</param>
    /// <param name="param">An array of arguments the patcher may want</param>
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
    /// <param name="fsmArray">Array that contains the FSM to remove patches from</param>
    /// <param name="param">An array of arguments the unpatcher may want</param>
    public override void RemovePatch(PlayMakerFSM[] fsmArray, object[]? param = null)
    {
        if (Unpatch == null) return;

        RemovePatch(fsmArray.FirstOrDefault(obj => obj.name.Equals(fsmName)), param);
    }

    /// <summary>
    /// Removes patches from an FSM
    /// </summary>
    /// <param name="fsm">The FSM to remove patches from</param>
    /// <param name="param">An array of arguments the unpatcher may want</param>
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
}

/// <summary>
/// Contains a set of state action patches for an object
/// </summary>
/// <param name="objectName">The name of the object the patches are for</param>
/// <param name="patches">The list of patches for the object</param>
/// <param name="fsmName">Optional: The name of the FSM the patch is for, used when FSMStatePatchSet.ApplyPatches is called with an array of FSMs</param>
public abstract class StateActionPatchSet_Base<PatchType>(string objectName, List<PatchType> patches, string fsmName = "")
    : FSMPatchSet_Base<PatchType, FsmStateAction>(objectName, patches, fsmName)
    where PatchType : StateActionPatch_Base;
#endregion

/// <summary>
/// Abstract class for containing an FSM patch
/// </summary>
/// <typeparam name="PatchTarget">The type of FSM part this is patching</typeparam>
/// <param name="fsmPart">The name of the FSM part being patched</param>
/// <param name="patch">The patch to apply</param>
/// <param name="unpatch">Optional unpatcher for when we want to remove it</param>
/// <param name="fsmName">Optional name for the FSM this patch should apply to, used for arrays of FSMs</param>
public abstract class FSMPatch_Base<PatchTarget>(string fsmPart, Action<PatchTarget, object[]?> patch, Action<PatchTarget, object[]?>? unpatch = null, string fsmName = "")
    : ObjectPatch_Base<PatchTarget, PlayMakerFSM>(fsmPart, patch, unpatch)
{
    public string FSMName => fsmName;
}

/// <summary>
/// Abstract class for containing a set of FSM patches
/// </summary>
/// <typeparam name="PatchType">The type of FSM patch</typeparam>
/// <typeparam name="PatchTarget">The type of FSM part this is patching</typeparam>
/// <param name="objectName">The name of the enemy being patched</param>
/// <param name="patches">The list of FSM Patches</param>
public abstract class FSMPatchSet_Base<PatchType, PatchTarget>(string objectName, List<PatchType> patches, string fsmName = "")
    : ObjectPatchSet_Base<PatchType, PlayMakerFSM, PatchTarget>(objectName, patches)
    where PatchType : FSMPatch_Base<PatchTarget>
{
    public string FSMName => fsmName;
}

/// <summary>
/// 
/// </summary>
public interface IFSMPatch
{
    /// <summary>
    /// The name of the FSM the patch is for, used when FSMStatePatchSet.ApplyPatches is called with an array of FSMs
    /// </summary>
    public string FSMName { get; }
}

/// <summary>
/// 
/// </summary>
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
using System;
using System.Collections.Generic;
using System.Linq;

using HutongGames.PlayMaker;

namespace Smol_Randomizer.FSMThings;
#region FSMState
/// <summary>
/// Contains a patch for an FSM state
/// </summary>
/// <param name="stateName">Name of the state the patch is for</param>
/// <param name="patch">The patch for the state</param>
/// <param name="FSMName">Optional: The name of the FSM the patch is for, used when FSMStatePatchSet.ApplyPatches is called with an array of FSMs</param>
public class FSMStatePatch(string stateName, Action<FsmState, object[]?> patch, string FSMName = "")
{
    /// <summary>
    /// Contains a patch for several FSM states
    /// </summary>
    /// <param name="stateNames">Names of the states the patch is for</param>
    /// <param name="patch">The patch for the state</param>
    /// <param name="FSMName">Optional: The name of the FSM the patch is for, used when FSMStatePatchSet.ApplyPatches is called with an array of FSMs</param>
    public FSMStatePatch(string[] stateNames, Action<FsmState, object[]?> patch, string FSMName = "") : this("", patch, FSMName)
    {
        StateNameArray = stateNames;
    }

    /// <summary>
    /// Name of the state the patch is for
    /// </summary>
    public string StateName { get; } = stateName;

    /// <summary>
    /// Names of the states the patch is for
    /// </summary>
    public string[] StateNameArray { get; } = [];
    /// <summary>
    /// The patch for the state
    /// </summary>
    public Action<FsmState, object[]?> Patch { get; } = patch;
    /// <summary>
    /// The name of the FSM the patch is for, used when FSMStatePatchSet.ApplyPatches is called with an array of FSMs
    /// </summary>
    public string FSMName { get; } = FSMName;

    /// <summary>
    /// Patches an FSM state, will ignore StateName if StateNames is populated
    /// </summary>
    /// <param name="fsm">The PlayMakerFSM to patch</param>
    /// <param name="extra">An array of extra arguments that patches may want</param>
    public void ApplyPatch(PlayMakerFSM fsm, object[]? extra = null)
    {
        if (StateNameArray.Length > 0)
        {
            foreach (string name in StateNameArray)
            {
                Patch(fsm.FsmStates.FirstOrDefault(state => state.Name.Equals(name)), extra);
            }
        }
        else
            Patch(fsm.FsmStates.FirstOrDefault(state => state.Name.Equals(StateName)), extra);
    }
}

/// <summary>
/// Contains a set of state patches for an object
/// </summary>
/// <param name="objectName">The name of the object the patches are for</param>
/// <param name="patches">The list of patches for the object</param>
public class FSMStatePatchSet(string objectName, List<FSMStatePatch> patches)
{
    /// <summary>
    /// The name of the object the patches are for
    /// </summary>
    public string ObjectName { get; } = objectName;
    /// <summary>
    /// The list of patches for the object
    /// </summary>
    public List<FSMStatePatch> Patches { get; } = patches;

    /// <summary>
    /// Applies the patches to the given FSM when called
    /// </summary>
    /// <param name="fsm">The fsm to apply patches to</param>
    /// <param name="extra">An array of extra arguments that patches may want</param>
    public void ApplyPatches(PlayMakerFSM fsm, object[]? extra = null)
    {
        foreach (var patch in Patches)
        {
            patch.ApplyPatch(fsm, extra);
        }
    }

    /// <summary>
    /// Applies the patches to the given set of FSMs when called
    /// </summary>
    /// <param name="fsmArray">An array of FSMs to patch</param>
    /// <param name="extra">An array of extra arguments that patches may want</param>
    public void ApplyPatches(PlayMakerFSM[] fsmArray, object[]? extra = null)
    {
        foreach (var fsm in fsmArray)
        {
            foreach (var patch in Patches)
            {
                if (patch.FSMName == fsm.FsmName)
                    patch.ApplyPatch(fsm, extra);
            }
        }
    }

    /// <summary>
    /// Adds a new patch to the pre-existing list of patches.
    /// </summary>
    /// <param name="patch">The patch to be added</param>
    public void AddPatch(FSMStatePatch patch)
    {
        Patches.Add(patch);
    }
}
#endregion

#region FSMStateAction
/// <summary>
/// Contains a patch for an action in an FSM state
/// </summary>
/// <param name="stateName">Name of the state the patch is for</param>
/// <param name="actionType">The type of the action the patch is for</param>
/// <param name="patch">The patch for the action</param>
/// <param name="FSMName">Optional: The name of the FSM the patch is for, used when FSMStatePatchSet.ApplyPatches is called with an array of FSMs</param>
public class FSMStateActionPatch(string stateName, Type actionType, Action<FsmStateAction, object[]?> patch, string FSMName = "")
{
    /// <summary>
    /// Name of the state the patch is for
    /// </summary>
    public string StateName { get; } = stateName;
    /// <summary>
    /// >The type of the action the patch is for
    /// </summary>
    public Type Type { get; } = actionType;
    /// <summary>
    /// The patch for the action
    /// </summary>
    public Action<FsmStateAction, object[]?> Patch { get; } = patch;
    /// <summary>
    /// The name of the FSM the patch is for, used when FSMStatePatchSet.ApplyPatches is called with an array of FSMs
    /// </summary>
    public string FSMName { get; } = FSMName;
    /// <summary>
    /// The type of the action the patch is for as a string
    /// </summary>
    public string TypeString => Type.ToString();

    /// <summary>
    /// Patches an FSM state action
    /// </summary>
    /// <param name="fsm">The PlayMakerFSM to patch</param>
    /// <param name="extra">An array of extra arguments that patches may want</param>
    public void ApplyPatch(PlayMakerFSM fsm, object[]? extra)
    {
        foreach (FsmStateAction action in fsm.FsmStates.FirstOrDefault(state => state.Name.Equals(StateName)).Actions)
        {
            if (action.GetType().ToString().Equals(TypeString))
            {
                Patch(action, extra);
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
{
    /// <summary>
    /// The name of the object the patches are for
    /// </summary>
    public string ObjectName { get; } = objectName;
    /// <summary>
    /// The list of patches for the object
    /// </summary>
    public List<FSMStateActionPatch> Patches { get; } = patches;

    /// <summary>
    /// Applies the patches to the given FSM when called
    /// </summary>
    /// <param name="fsm">The fsm to apply patches to</param>
    /// <param name="extra">An array of extra arguments that patches may want</param>
    public void ApplyPatches(PlayMakerFSM fsm, object[]? extra = null)
    {
        foreach (var patch in Patches)
        {
            patch.ApplyPatch(fsm, extra);
        }
    }

    /// <summary>
    /// Applies the patches to the given set of FSMs when called
    /// </summary>
    /// <param name="fsmArray">An array of FSMs to patch</param>
    /// <param name="extra">An array of extra arguments that patches may want</param>
    public void ApplyPatches(PlayMakerFSM[] fsmArray, object[]? extra = null)
    {
        foreach (var fsm in fsmArray)
        {
            foreach (var patch in Patches)
            {
                if (patch.FSMName == fsm.FsmName)
                    patch.ApplyPatch(fsm, extra);
            }
        }
    }

    /// <summary>
    /// Adds a new patch to the pre-existing list of patches.
    /// </summary>
    /// <param name="patch">The patch to be added</param>
    public void AddPatch(FSMStateActionPatch patch)
    {
        Patches.Add(patch);
    }
}
#endregion
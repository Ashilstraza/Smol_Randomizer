using System;
using System.Collections.Generic;
using System.Linq;

using HutongGames.PlayMaker;

using UnityEngine;

using static Smol_Randomizer.Patchers.Enemy.EnemyFSMPatches;

namespace Smol_Randomizer.Patchers.Enemy;

/// <summary>Handles patching enemy FSMs</summary>
internal class EnemyFSMPatches
{
    /// <summary>Class for containing a set of Enemy State Action patches</summary>
    public class EnemyStateAction : EnemyFSMPatchCollections_Base<EnemyStateActionPatch, EnemyStateActionPatchSet, FsmStateAction>;

    /// <summary>Class for containing a set of Enemy State patches</summary>
    public class EnemyState : EnemyFSMPatchCollections_Base<EnemyStatePatch, EnemyStatePatchSet, FsmState>;

    /// <summary>A set of Enemy State Action patches</summary>
    public static readonly EnemyStateAction enemyStateAction = new();

    /// <summary>As set of Enemy State patches</summary>
    public static readonly EnemyState enemyState = new();

    /// <summary>Set of patched enemies</summary>
    internal static HashSet<GameObject> patchedEnemies = [];

    /// <summary>Called when the scene loads to clear out unloaded enemies</summary>
    internal static void OnSceneLoaded()
    {
        patchedEnemies.RemoveWhere(enemy => enemy == null);
    }

    /// <summary>List of objects to patch on the first frame to sidestep some weirdness</summary>
    internal static HashSet<LatePatchInfo> latePatches = [];

    internal class LatePatchInfo(string name, GameObject enemy, Vector2 originalScale)
    {
        public string Name { get; } = name;
        public GameObject Enemy { get; } = enemy;
        public Vector2 OriginalScale { get; } = originalScale;
    }

    /// <summary>If we are currently applying patches late</summary>
    internal static bool latePatching = false;

    internal static void OnFirstFrame()
    {
        latePatching = true;

        foreach (var enemy in latePatches)
        {
            if (enemy.Enemy == null)
                continue;
            string enemyName = enemy.Name;

            int cullIndex = enemyName.IndexOf('(');
            if (cullIndex > 0) enemyName = enemyName[..cullIndex].TrimEnd();

            enemyStateAction.ApplyPatches(enemy);
            enemyState.ApplyPatches(enemy);
        }

        latePatches.Clear();
        latePatching = false;
    }

    /// <summary>Registers a collection of patches that are a mix of State and StateActions</summary>
    /// <param name="enemyPatchCollection"></param>
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

    /// <summary>Unregisters a collection of patches that are a mix of State and StateActions</summary>
    /// <param name="enemyPatchCollection"></param>
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

    public static bool OverwritePatch(IEnemyFSMPatch patch)
    {
        if (patch is EnemyStateActionPatch actionPatch)
        {
            enemyStateAction.Patches.TryGetValue(actionPatch.EnemyName, out var patchSet);

            var oldPatch = patchSet.Where(p => p.FSMName.Equals(actionPatch.FSMName)
                                                && (p.Name.Equals(actionPatch.Name) || p.NameArray.Contains(actionPatch.Name))
                                                && p.TypeString.Equals(actionPatch.TypeString)).FirstOrDefault();
            if (oldPatch == null)
            {
                CuteRandoCore.Log.LogWarning($"Overwrite patch failed, no matching patch; Name: {patch.Name}, Enemy Name: {patch.EnemyName}, Type: {patch.GetType()}");
            }
            else
            {
                patchSet.Remove(oldPatch);
                patchSet.Add(actionPatch);
                return true;
            }
        }
        else if (patch is EnemyStatePatch statePatch)
        {
            enemyState.Patches.TryGetValue(statePatch.EnemyName, out var patchSet);

            var oldPatch = patchSet?.Where(p => p.FSMName.Equals(statePatch.FSMName)
                                                && (p.Name.Equals(statePatch.Name) || p.NameArray.Contains(statePatch.Name))).FirstOrDefault();
            if (oldPatch == null || patchSet == null)
            {
                CuteRandoCore.Log.LogWarning($"Overwrite patch failed, no matching patch; Name: {patch.Name}, Enemy Name: {patch.EnemyName}, Type: {patch.GetType()}");
            }
            else
            {
                patchSet.Remove(oldPatch);
                patchSet.Add(statePatch);
                return true;
            }
        }
        else
            CuteRandoCore.Log.LogWarning($"Overwrite patch called with invalid patch type; Name: {patch.Name}, Enemy Name: {patch.EnemyName}, Type: {patch.GetType()}");
        return false;
    }

    /// <summary>Apply the various enemy patches</summary>
    /// <param name="enemyHealthManager">The enemy to patch</param>
    internal static void ApplyPatches(HealthManager enemyHealthManager)
    {
        if (patchedEnemies.Contains(enemyHealthManager.gameObject)) return; // Don't reapply patches

        string enemyName = enemyHealthManager.name;

        int cullIndex = enemyName.IndexOf('(');
        if (cullIndex > 0) enemyName = enemyName[..cullIndex].TrimEnd();

        if (!enemyStateAction.PatchedObjects.ContainsKey(enemyName) && !enemyState.PatchedObjects.ContainsKey(enemyName))
            return;

        GameObject enemyObject = enemyHealthManager.gameObject;

        enemyStateAction.ApplyPatches(enemyName, enemyObject);
        enemyState.ApplyPatches(enemyName, enemyObject);
    }

    /// <summary>Called to remove all enemy patches</summary>
    internal static void RemovePatches()
    {
        foreach (var enemy in patchedEnemies)
            RemovePatches(enemy.GetComponent<HealthManager>());
    }

    /// <summary>Remove the various enemy patches</summary>
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

/// <summary></summary>
/// <param name="enemyName"></param>
/// <param name="stateName"></param>
/// <param name="patch">    </param>
/// <param name="unpatch">  </param>
/// <param name="fsmName">  </param>
public class EnemyStatePatch(string enemyName, string stateName, Action<FsmState, object[]?> patch, Action<FsmState, object[]?>? unpatch = null, string fsmName = "", bool latePatch = false, bool reInit = true)
    : StatePatch_Base(stateName, patch, unpatch, fsmName), IEnemyFSMPatch
{
    /// <summary></summary>
    /// <param name="enemyName"> </param>
    /// <param name="stateNames"></param>
    /// <param name="patch">     </param>
    /// <param name="unpatch">   </param>
    /// <param name="fsmName">   </param>
    public EnemyStatePatch(string enemyName, string[] stateNames, Action<FsmState, object[]?> patch, Action<FsmState, object[]?>? unpatch = null, string fsmName = "", bool latePatch = false, bool reInit = true)
        : this(enemyName, "", patch, unpatch, fsmName, latePatch, reInit)
    {
        NameArray = stateNames;
    }

    /// <summary></summary>
    /// <param name="enemyNames"></param>
    /// <param name="stateName"> </param>
    /// <param name="patch">     </param>
    /// <param name="unpatch">   </param>
    /// <param name="fsmName">   </param>
    public EnemyStatePatch(string[] enemyNames, string stateName, Action<FsmState, object[]?> patch, Action<FsmState, object[]?>? unpatch = null, string fsmName = "", bool latePatch = false, bool reInit = true)
        : this("", stateName, patch, unpatch, fsmName, latePatch, reInit)
    {
        EnemyNames = enemyNames;
    }

    private string[] enemyNames = [];

    /// <summary>Array of enemy names to patch</summary>
    public string[] EnemyNames { get => enemyNames; set => enemyNames = value; }

    public string EnemyName { get => enemyName; set => enemyName = value; }
    public bool LatePatch { get => latePatch; set => latePatch = value; }
    public bool ReInit { get => reInit; set => reInit = value; }

    public override object Clone()
    {
        if (NameArray.Length > 0)
            return new EnemyStatePatch(EnemyName, NameArray, (Action<FsmState, object[]?>)Patch.Clone(), (Action<FsmState, object[]?>?)Unpatch?.Clone(), FSMName, LatePatch);
        else if (EnemyNames.Length > 0)
            return new EnemyStatePatch(EnemyNames, Name, (Action<FsmState, object[]?>)Patch.Clone(), (Action<FsmState, object[]?>?)Unpatch?.Clone(), FSMName, LatePatch);
        return new EnemyStatePatch(EnemyName, Name, (Action<FsmState, object[]?>)Patch.Clone(), (Action<FsmState, object[]?>?)Unpatch?.Clone(), FSMName, LatePatch);
    }
}

public class EnemyStatePatchSet : StatePatchSet_Base<EnemyStatePatch>, IEnemyFSMPatchSet
{
    private string enemyName;
    private bool latePatch;
    private bool reInit;

    public EnemyStatePatchSet(string enemyName, List<EnemyStatePatch> patches, string fsmName = "", bool latePatch = false, bool reInit = true) : base(enemyName, patches, fsmName)
    {
        this.enemyName = enemyName;
        this.latePatch = latePatch;
        this.reInit = reInit;

        foreach (EnemyStatePatch patch in patches)
        {
            patch.EnemyName = enemyName;
            patch.LatePatch = latePatch;
            patch.ReInit = reInit;
            if (patch.FSMName == "") patch.FSMName = fsmName;
        }
    }

    public string EnemyName { get => enemyName; set => enemyName = value; }
    public bool LatePatch { get => latePatch; set => latePatch = value; }
    public bool ReInit { get => reInit; set => reInit = value; }

    public override object Clone()
    {
        List<EnemyStatePatch> newList = [];

        foreach (var patch in Patches)
            newList.Add(patch);

        return new EnemyStatePatchSet(Name, newList, FSMName, LatePatch);
    }
}

#endregion FSM State

#region FSM State Action

public class EnemyStateActionPatch(string enemyName, string stateName, Type actionType, Action<FsmStateAction, object[]?> patch, Action<FsmStateAction, object[]?>? unpatch = null, string fsmName = "", bool latePatch = false, bool reInit = true)
    : StateActionPatch_Base(stateName, actionType, patch, unpatch, fsmName), IEnemyFSMPatch
{
    public EnemyStateActionPatch(string enemyName, string[] stateNames, Type actionType, Action<FsmStateAction, object[]?> patch, Action<FsmStateAction, object[]?>? unpatch = null, string fsmName = "", bool latePatch = false, bool reInit = true)
        : this(enemyName, "", actionType, patch, unpatch, fsmName, latePatch, reInit)
    {
        NameArray = stateNames;
    }

    public EnemyStateActionPatch(string[] enemyNames, string stateName, Type actionType, Action<FsmStateAction, object[]?> patch, Action<FsmStateAction, object[]?>? unpatch = null, string fsmName = "", bool latePatch = false, bool reInit = true)
        : this("", stateName, actionType, patch, unpatch, fsmName, latePatch, reInit)
    {
        EnemyNames = enemyNames;
    }

    private string[] enemyNames = [];

    /// <summary>Array of enemy names to patch</summary>
    public string[] EnemyNames { get => enemyNames; set => enemyNames = value; }

    public string EnemyName { get => enemyName; set => enemyName = value; }
    public bool LatePatch { get => latePatch; set => latePatch = value; }
    public bool ReInit { get => reInit; set => reInit = value; }

    public override object Clone()
    {
        if (NameArray.Length > 0)
            return new EnemyStateActionPatch(EnemyName, NameArray, Type, (Action<FsmStateAction, object[]?>)Patch.Clone(), (Action<FsmStateAction, object[]?>?)Unpatch?.Clone(), FSMName, LatePatch);
        else if (EnemyNames.Length > 0)
            return new EnemyStateActionPatch(EnemyNames, Name, Type, (Action<FsmStateAction, object[]?>)Patch.Clone(), (Action<FsmStateAction, object[]?>?)Unpatch?.Clone(), FSMName, LatePatch);
        return new EnemyStateActionPatch(EnemyName, Name, Type, (Action<FsmStateAction, object[]?>)Patch.Clone(), (Action<FsmStateAction, object[]?>?)Unpatch?.Clone(), FSMName, LatePatch);
    }
}

public class EnemyStateActionPatchSet(string enemyName, string objectName, List<EnemyStateActionPatch> patches, string fsmName = "", bool latePatch = false, bool reInit = true)
    : StateActionPatchSet_Base<EnemyStateActionPatch>(objectName, patches, fsmName), IEnemyFSMPatchSet
{
    public string EnemyName { get => enemyName; set => enemyName = value; }
    public bool LatePatch { get => latePatch; set => latePatch = value; }
    public bool ReInit { get => reInit; set => reInit = value; }

    public override object Clone()
    {
        List<EnemyStateActionPatch> newList = [];

        foreach (var patch in Patches)
            newList.Add(patch);

        return new EnemyStateActionPatchSet(EnemyName, Name, newList, FSMName, LatePatch);
    }
}

#endregion FSM State Action

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
            {
                if (setPatch.LatePatch)
                {
                    EnemyFSMPatches.latePatches.Add(new(enemyName, enemyObject, enemyObject.transform.localScale));
                    continue;
                }
                else
                    setPatch.ApplyPatch(enemyObject.GetComponents<PlayMakerFSM>(), [enemyObject, enemyName]);

                EnemyFSMPatches.patchedEnemies.Add(enemyObject);
            }
        }
    }

    internal void ApplyPatches(LatePatchInfo patch)
    {
        if (Patches.TryGetValue(patch.Name, out HashSet<PatchType> patchSet))
        {
            foreach (var setPatch in patchSet)
            {
                if (!setPatch.LatePatch) continue;

                setPatch.ApplyPatch(patch.Enemy.GetComponents<PlayMakerFSM>(), [patch.Enemy, patch.Name, patch.OriginalScale]);

                if (setPatch.ReInit)
                {
                    var fsm = patch.Enemy.GetComponents<PlayMakerFSM>().FirstOrDefault(obj => obj.FsmName.Equals(setPatch.FSMName)) ?? patch.Enemy.GetComponents<PlayMakerFSM>()[0];

                    //if(fsm.ActiveStateName == setPatch.Name)
                    ReInit(setPatch, fsm);
                }

                EnemyFSMPatches.patchedEnemies.Add(patch.Enemy);
            }
        }

        void ReInit(PatchType setPatch, PlayMakerFSM fsm)
        {
            var state = fsm.FsmStates.FirstOrDefault(state => state.Name.Equals(setPatch.Name));
            fsm.Fsm.SwitchState(state);
        }
    }

    public virtual void RemovePatches(string enemyName, GameObject enemyObject)
    {
        if (Patches.TryGetValue(enemyName, out HashSet<PatchType> patchSet))
        {
            foreach (var setPatch in patchSet)
                setPatch.RemovePatch(enemyObject.GetComponents<PlayMakerFSM>(), [enemyObject, enemyName]);
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
            RegisterEnemyPatchCollection(patchSet.Patches);
        }
        else
            CuteRandoCore.Log.LogWarning($"Register patch called with invalid patch type, Name: {smolPatch.Name}, Enemy Name: {smolPatch.EnemyName}, Type: {smolPatch.GetType()}");
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
    /// <summary>Name of the patch target</summary>
    public string EnemyName { get; set; }

    public bool LatePatch { get; set; }

    public bool ReInit { get; set; }
}
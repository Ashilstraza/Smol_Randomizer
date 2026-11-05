using BepInEx;

using HarmonyLib;

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
    public class EnemyStateAction : FSMPatchCollections_Base<StateActionPatch, StateActionPatchSet, FsmStateAction>;

    /// <summary>Class for containing a set of <see cref="EnemyState" /> patches</summary>
    public class EnemyState : FSMPatchCollections_Base<StatePatch, StatePatchSet, FsmState>;

    /// <summary>Class for containing a set of <see cref="PlayMakerFSM"/> patches</summary>
    public class EnemyFSMs : FSMPatchCollections_Base<FSMPatch, FSMPatchSet, PlayMakerFSM>;

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
    public static void RegisterPatchCollection(List<IFSMPatch> enemyPatchCollection)
    {
        int stateActionPatches = 0;
        int statePatches = 0;
        int fsmPatches = 0;

        foreach (var patch in enemyPatchCollection)
        {
            if (patch is StateActionPatch or StateActionPatchSet)
                stateActionPatches += enemyStateAction.RegisterPatchInCollection(patch.Name, patch);
            else if (patch is StatePatchSet or StatePatch)
                statePatches += enemyState.RegisterPatchInCollection(patch.Name, patch);
            else if (patch is FSMPatchSet or FSMPatch)
                fsmPatches += enemyFSMPatches.RegisterPatchInCollection(patch.Name, patch);
            else
                CuteRandoCore.DebugLog($"Register patch called with invalid patch type, Name: {patch.Name}, Type: {patch.GetType()}", DebugLogOptions.Error, true);
        }

        CuteRandoCore.Log.LogInfo($"Registered {stateActionPatches} Enemy State Action Patches, {statePatches} Enemy State Patches, and {fsmPatches} Enemy Fsm Patches");
    }

    /// <summary>Unregisters a collection of patches</summary>
    /// <param name="enemyPatchCollection">The collection of patches to remove</param>
    public static void UnregisterPatchCollection(List<IFSMPatch> enemyPatchCollection)
    {
        int stateActionPatches = 0;
        int statePatches = 0;
        int fsmPatches = 0;

        foreach (var patch in enemyPatchCollection)
        {
            if (patch is StateActionPatch or StateActionPatchSet)
                stateActionPatches += enemyStateAction.UnregisterPatchInCollection(patch.Name, patch);
            else if (patch is StatePatch or StatePatchSet)
                statePatches += enemyState.UnregisterPatchInCollection(patch.Name, patch);
            else if (patch is FSMPatchSet or FSMPatch)
                fsmPatches += enemyFSMPatches.UnregisterPatchInCollection(patch.Name, patch);
            else
                CuteRandoCore.DebugLog($"Unregister patch called with invalid patch type, Name: {patch.Name}, Type: {patch.GetType()}", DebugLogOptions.Error, true);
        }

        CuteRandoCore.Log.LogInfo($"Unregistered {stateActionPatches} Enemy State Action Patches, {statePatches} Enemy State Patches, and {fsmPatches} Enemy Fsm Patches");
    }

    /// <summary>Registers a patch and overwrites any existing ones with the new one</summary>
    /// <param name="patch">The patch to add</param>
    /// <returns>If the patch was added</returns>
    public static bool OverwritePatch(IFSMPatch patch)
    {
        if (patch is StateActionPatch actionPatch)
        {
            enemyStateAction.Patches.TryGetValue(actionPatch.Name, out var patchSet);

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
        else if (patch is StatePatch statePatch)
        {
            enemyState.Patches.TryGetValue(statePatch.Name, out var patchSet);

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
        else if (patch is FSMPatch)
            throw new NotImplementedException();
        else
            CuteRandoCore.DebugLog($"Overwrite patch called with invalid patch type; Name: {patch.Name}, Enemy Name: {patch.Name}, Type: {patch.GetType()}", DebugLogOptions.Error, true);
        return false;
    }

    /// <summary>Apply the various enemy patches</summary>
    /// <param name="enemy">The enemy to patch</param>
    internal static void ApplyPatches(GameObject enemy)
    {
        {
            if (patchedEnemies.Contains(enemy))
            {
                CuteRandoCore.TestingLog($"Enemy attempted to patch again; Name: {enemy.name}", DebugLogOptions.Message, true);
                return; // Don't reapply patches
            }

            string enemyName = CuteRandoCore.CullName(enemy.name);

            bool patched = false;

            patched = patched | enemyFSMPatches.ApplyPatches(enemyName, enemy);
            patched = patched | enemyState.ApplyPatches(enemyName, enemy);
            patched = patched | enemyStateAction.ApplyPatches(enemyName, enemy);

            if(patched)
                patchedEnemies.Add(enemy);
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

        bool unpatched = false;

        unpatched = unpatched | enemyStateAction.RemovePatches(enemyName, enemy);
        unpatched = unpatched | enemyState.RemovePatches(enemyName, enemy);
        unpatched = unpatched | enemyFSMPatches.RemovePatches(enemyName, enemy);

        if (unpatched)
            patchedEnemies.Remove(enemy);
    }
}
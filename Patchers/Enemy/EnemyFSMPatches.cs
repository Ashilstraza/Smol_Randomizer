using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using HarmonyLib;

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
    public class EnemyStateAction : FSMPatchCollection_Base<StateActionPatch, StateActionPatchSet, FsmStateAction>;
    /// <summary>
    /// Class for containing a set of Enemy State patches
    /// </summary>
    public class EnemyState : FSMPatchCollection_Base<StatePatch, StatePatchSet, FsmState>;

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
    public static void RegisterPatchCollection(Dictionary<string, ISmolPatch> enemyPatchCollection)
    {
        foreach(var patch in enemyPatchCollection)
        {
            if (patch.Value is StateActionPatch or StateActionPatchSet)
                enemyStateAction.RegisterPatchInCollection(patch.Key, patch.Value);
            else if (patch.Value is StatePatch or StatePatchSet)
                enemyState.RegisterPatchInCollection(patch.Key, patch.Value);
            else
                CuteRandoCore.Log.LogWarning($"Register patch called with invalid patch {patch.Value.Name}");
        }
    }

    /// <summary>
    /// Unregisters a collection of patches that are a mix of State and StateActions
    /// </summary>
    /// <param name="enemyPatchCollection"></param>
    public static void UnregisterPatchCollection(Dictionary<string, ISmolPatch> enemyPatchCollection)
    {
        foreach (var patch in enemyPatchCollection)
        {
            if (patch.Value is StateActionPatch or StateActionPatchSet)
                enemyStateAction.UnregisterPatchInCollection(patch.Key, patch.Value);
            else if (patch.Value is StatePatch or StatePatchSet)
                enemyState.UnregisterPatchInCollection(patch.Key, patch.Value);
            else
                CuteRandoCore.Log.LogWarning($"Register patch called with invalid patch {patch.Value.Name}");
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
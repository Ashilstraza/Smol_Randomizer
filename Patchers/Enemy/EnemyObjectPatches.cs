using System;
using System.Collections.Generic;

using UnityEngine;

namespace Smol_Randomizer.Patchers.Enemy;

/// <summary>Class for containing a set of enemy patches</summary>
public class EnemyObjectPatchCollection : ObjectPatchCollection_Base<EnemyObjectPatch, EnemyObjectPatchSet, GameObject, GameObject>
{
    private static readonly Lazy<EnemyObjectPatchCollection> instance = new(() => new EnemyObjectPatchCollection());

    /// <summary>A set of enemy object patches</summary>
    public static EnemyObjectPatchCollection Instance => instance.Value;

    /// <summary>Set of patched enemies</summary>
    private static HashSet<HealthManager> patchedEnemies = [];

    /// <summary>Called when the scene loads to clear out unloaded enemies</summary>
    internal static void OnSceneLoaded()
    {
        patchedEnemies.RemoveWhere(enemy => enemy == null);
    }

    /// <summary>Apply the various enemy patches</summary>
    /// <param name="enemyHealthManager">The enemy to patch</param>
    internal static void ApplyPatches(HealthManager enemyHealthManager)
    {
        string enemyName = enemyHealthManager.name;

        int cullIndex = enemyName.IndexOf('(');
        if (cullIndex > 0) enemyName = enemyName[..cullIndex].TrimEnd();

        if (!Instance.PatchedObjects.ContainsKey(enemyName))
            return;

        GameObject obj = enemyHealthManager.gameObject;

        Instance.ApplyPatches(enemyName, obj);

        patchedEnemies.Add(enemyHealthManager);
    }

    /// <summary>Called to remove all enemy patches</summary>
    internal static void RemovePatches()
    {
        foreach (var enemy in patchedEnemies)
            RemovePatches(enemy);
    }

    /// <summary>Remove the various enemy patches</summary>
    /// <param name="enemyHealthManager">The enemy to unpatch</param>
    internal static void RemovePatches(HealthManager enemyHealthManager)
    {
        string enemyName = enemyHealthManager.name;

        if (!Instance.PatchedObjects.ContainsKey(enemyName))
            return;

        GameObject obj = enemyHealthManager.gameObject;

        Instance.RemovePatches(enemyName, obj);

        patchedEnemies.Remove(enemyHealthManager);
    }

    /// <summary>Register a collection of patches</summary>
    /// <param name="patchCollection">A list containing the patches to be applied</param>
    public void RegisterPatchCollection(List<IEnemyObjectPatch> patchCollection)
    {
        foreach (var patch in patchCollection)
        {
            RegisterPatchInCollection(patch.Name, patch);
        }
    }

    /// <summary> Unregister a collection of patches </summary> <param name="patchCollection">A list containing the
    /// patches to no longer be applied<</param>
    public void UnregisterPatchCollection(List<IEnemyObjectPatch> patchCollection)
    {
        foreach (var patch in patchCollection)
        {
            RegisterPatchInCollection(patch.Name, patch);
        }
    }
}

/// <summary>Set of patches for enemy objects</summary>
/// <param name="enemyName">Name of the enemy to patch</param>
/// <param name="patches">  Set of patches to apply to the enemy</param>
public class EnemyObjectPatchSet(string enemyName, List<EnemyObjectPatch> patches)
    : ObjectPatchSet_Base<EnemyObjectPatch, GameObject, GameObject>(enemyName, patches), IEnemyObjectPatchSet
{
    /// <summary>Set of patches for enemy objects</summary>
    /// <param name="enemyNames">Array of names for enemies to patch</param>
    /// <param name="patches">   Set of patches to apply to the enemy</param>
    public EnemyObjectPatchSet(string[] enemyNames, List<EnemyObjectPatch> patches)
        : this("", patches)
    {
        NameArray = enemyNames;
    }

    public override object Clone()
    {
        List<EnemyObjectPatch> newList = [];

        foreach (var patch in Patches)
            newList.Add(patch);

        if (NameArray.Length > 0)
            return new EnemyObjectPatchSet(NameArray, newList);
        return new EnemyObjectPatchSet(Name, newList);
    }
}

/// <summary>Patch for an enemy object</summary>
/// <param name="enemyName">Name of the enemy to patch</param>
/// <param name="patch">    The patch for the enemy</param>
/// <param name="unpatch">  Optional unpatcher for undoing the changes</param>
public class EnemyObjectPatch(string enemyName, Action<GameObject, object[]?> patch, Action<GameObject, object[]?>? unpatch = null)
        : ObjectPatch_Base<GameObject, GameObject>(enemyName, patch, unpatch), IEnemyObjectPatch
{
    /// <summary>Patch for an enemy object</summary>
    /// <param name="enemyNames">Array of names for enemies to patch</param>
    /// <param name="patch">     The patch for the enemy</param>
    /// <param name="unpatch">   Optional unpatcher for undoing the changes</param>
    public EnemyObjectPatch(string[] enemyNames, Action<GameObject, object[]?> patch, Action<GameObject, object[]?>? unpatch = null)
        : this("", patch, unpatch)
    {
        NameArray = enemyNames;
    }

    public override void ApplyPatch(GameObject[] patchTargets, object[]? param = null)
    {
        foreach (var target in patchTargets)
        {
            ApplyPatch(target, param);
        }
    }

    public override void ApplyPatch(GameObject patchTarget, object[]? param = null)
    {
        Patch(patchTarget, param);
    }

    public override void RemovePatch(GameObject[] patchTargets, object[]? param = null)
    {
        foreach (var target in patchTargets)
        {
            RemovePatch(target, param);
        }
    }

    public override void RemovePatch(GameObject patchTarget, object[]? param = null)
    {
        if (Unpatch == null) return;
        Unpatch(patchTarget, param);
    }

    public override object Clone()
    {
        if (NameArray.Length > 0)
            return new EnemyObjectPatch(NameArray, (Action<GameObject, object[]?>)Patch.Clone(), (Action<GameObject, object[]?>?)Unpatch?.Clone());
        return new EnemyObjectPatch(Name, (Action<GameObject, object[]?>)Patch.Clone(), (Action<GameObject, object[]?>?)Unpatch?.Clone());
    }
}

/// <summary>Marks a patch as an Enemy Object Patch</summary>
public interface IEnemyObjectPatch : ISmolPatch;

/// <summary>
/// Marks a patch set as an Enemy Object Patch Set, includes the interface to additionally mark it as a patch
/// </summary>
public interface IEnemyObjectPatchSet : IEnemyObjectPatch, ISmolPatchSet;
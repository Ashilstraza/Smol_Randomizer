using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;

namespace Smol_Randomizer.Patchers.Enemy;

/// <summary>Class for containing a set of enemy patches</summary>
public class EnemyObjectPatchCollection : ObjectPatchCollection_Base<EnemyObjectPatch, EnemyObjectPatchSet, GameObject, GameObject>
{
    /// <summary>Make a singleton of this class</summary>
    private static readonly Lazy<EnemyObjectPatchCollection> instance = new(() => new EnemyObjectPatchCollection());

    /// <summary>A set of enemy object patches</summary>
    public static EnemyObjectPatchCollection Instance => instance.Value;

    /// <summary>Set of patched enemy <see cref="GameObject" />s</summary>
    private HashSet<GameObject> patchedEnemies = [];

    /// <summary>Called when the scene loads to clear out unloaded enemies</summary>
    internal static void OnSceneLoaded()
    {
        Instance.patchedEnemies.RemoveWhere(enemy => enemy == null);
    }

    /// <summary>Apply the various enemy patches</summary>
    /// <param name="enemyHealthManager">The <see cref="HealthManager" /> of the enemy to patch</param>
    internal static void ApplyPatches(HealthManager enemyHealthManager)
    {
        if (enemyHealthManager == null) return;

        string enemyName = CuteRandoCore.CullName(enemyHealthManager.name);

        if (!Instance.PatchedObjects.ContainsKey(enemyName))
            return;

        GameObject obj = enemyHealthManager.gameObject;

        Instance.ApplyPatches(enemyName, obj);

        Instance.patchedEnemies.Add(obj);
    }

    /// <summary>Called to remove all enemy patches</summary>
    internal static void RemovePatches()
    {
        for (int i = Instance.patchedEnemies.Count - 1; i >= 0; i--)
            RemovePatches(Instance.patchedEnemies.ElementAt(i));
    }

    /// <summary>Remove the various enemy patches</summary>
    /// <param name="enemy">The enemy to unpatch</param>
    internal static void RemovePatches(GameObject enemy)
    {
        if (enemy == null) return;

        if (!Instance.PatchedObjects.ContainsKey(enemy.name))
            return;

        Instance.RemovePatches(enemy.name, enemy);

        Instance.patchedEnemies.Remove(enemy);
    }

    /// <summary>Register a collection of patches</summary>
    /// <param name="patchCollection">A list containing the patches to register</param>
    public void RegisterPatchCollection(List<IEnemyObjectPatch> patchCollection)
    {
        foreach (var patch in patchCollection)
        {
            RegisterPatchInCollection(patch.Name, patch);
        }
    }

    /// <summary>Unregister a collection of patches</summary>
    /// <param name="patchCollection">A list containing the patches to unregister</param>
    public void UnregisterPatchCollection(List<IEnemyObjectPatch> patchCollection)
    {
        foreach (var patch in patchCollection)
        {
            RegisterPatchInCollection(patch.Name, patch);
        }
    }
}

/// <summary>Set of patches for enemy <see cref="GameObject" />s</summary>
/// <param name="enemyName">Name of the enemy to patch</param>
/// <param name="patches">  Set of patches to apply to the enemy</param>
public class EnemyObjectPatchSet(string enemyName, List<EnemyObjectPatch> patches)
    : ObjectPatchSet_Base<EnemyObjectPatch, GameObject, GameObject>(enemyName, patches), IEnemyObjectPatchSet
{
    /// <inheritdoc cref="EnemyObjectPatchSet" />
    /// <param name="enemyNames">Names of the enemies to patch</param>
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

/// <summary>Patch for an enemy <see cref="GameObject" /></summary>
/// <param name="enemyName">Name of the enemy to patch</param>
/// <param name="patch">    The patch for the enemy</param>
/// <param name="unpatch">  Optional unpatcher for undoing the changes</param>
public class EnemyObjectPatch(string enemyName, Action<GameObject, object[]> patch, Action<GameObject, object[]>? unpatch = null)
        : ObjectPatch_Base<GameObject, GameObject>(enemyName, patch, unpatch), IEnemyObjectPatch
{
    /// <inheritdoc cref="EnemyObjectPatch" />
    /// <param name="enemyNames"><see cref="Array" /> of names for enemies to patch</param>
    public EnemyObjectPatch(string[] enemyNames, Action<GameObject, object[]> patch, Action<GameObject, object[]>? unpatch = null)
        : this("", patch, unpatch)
    {
        NameArray = enemyNames;
    }

    public override void ApplyPatch(GameObject[] patchTargets, object[] param)
    {
        foreach (var target in patchTargets)
        {
            ApplyPatch(target, param);
        }
    }

    public override void ApplyPatch(GameObject patchTarget, object[] param)
    {
        Patch(patchTarget, param);
    }

    public override void RemovePatch(GameObject[] patchTargets, object[] param)
    {
        foreach (var target in patchTargets)
        {
            RemovePatch(target, param);
        }
    }

    public override void RemovePatch(GameObject patchTarget, object[] param)
    {
        if (Unpatch == null) return;
        Unpatch(patchTarget, param);
    }

    public override object Clone()
    {
        if (NameArray.Length > 0)
            return new EnemyObjectPatch(NameArray, (Action<GameObject, object[]>)Patch.Clone(), (Action<GameObject, object[]>?)Unpatch?.Clone());
        return new EnemyObjectPatch(Name, (Action<GameObject, object[]>)Patch.Clone(), (Action<GameObject, object[]>?)Unpatch?.Clone());
    }
}

/// <summary>Marks a patch as an Enemy Object Patch</summary>
public interface IEnemyObjectPatch : ISmolPatch;

/// <summary>Marks a patch set as an Enemy Object Patch Set</summary>
public interface IEnemyObjectPatchSet : IEnemyObjectPatch, ISmolPatchSet;
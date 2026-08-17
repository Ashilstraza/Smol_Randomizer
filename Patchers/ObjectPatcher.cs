using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using UnityEngine.SceneManagement;

namespace Smol_Randomizer.Patchers;

/// <summary>
/// Class for patching various objects
/// </summary>
public static class ObjectPatcher
{
    #region Scene
    /// <summary>
    /// Class for containing a set of object patches
    /// </summary>
    public class SceneObject : GenericScenePatch<ObjectPatch>
    {
        public override void ApplyPatches(Scene scene, HashSet<GameObject> objects)
        {
            if (Patches.TryGetValue(scene.name, out Dictionary<string, HashSet<ObjectPatch>> patches))
            {
                foreach (var patchGroup in patches)
                {
                    foreach (var patch in patchGroup.Value)
                        patch.ApplyPatch(objects.FirstOrDefault(obj => obj.name.Equals(patchGroup.Key)), null);
                }
            }
        }

        public override void RemovePatches(Scene scene, HashSet<GameObject> objects)
        {
            if (Patches.TryGetValue(scene.name, out Dictionary<string, HashSet<ObjectPatch>> patches))
            {
                foreach (var patchGroup in patches)
                {
                    foreach (var patch in patchGroup.Value)
                        patch.RemovePatch(objects.FirstOrDefault(obj => obj.name.Equals(patchGroup.Key)), null);
                }
            }
        }
    }

    /// <summary>
    /// A set of scene object patches
    /// </summary>
    public static readonly SceneObject sceneObject = new();

    /// <summary>
    /// Apply the various scene patches
    /// </summary>
    /// <param name="scene">The scene to patch</param>
    internal static void ApplyScenePatches(Scene scene)
    {
        HashSet<GameObject> objects = scene.GetRootGameObjects().ToHashSet();

        sceneObject.ApplyPatches(scene, objects);
    }

    /// <summary>
    /// Remove the various scene patches
    /// </summary>
    /// <param name="scene">The scene to unpatch</param>
    internal static void RemoveScenePatches(Scene scene)
    {
        HashSet<GameObject> objects = scene.GetRootGameObjects().ToHashSet();

        sceneObject.RemovePatches(scene, objects);
    }

    /// <summary>
    /// Abstract class for object patches
    /// </summary>
    /// <typeparam name="T">The class for the base patch</typeparam>
    public abstract class GenericScenePatch<T>
    {
        private readonly Dictionary<string, Dictionary<string, HashSet<T>>> patches = [];
        /// <summary>
        /// The collection of patches that this contains
        /// </summary>
        public Dictionary<string, Dictionary<string, HashSet<T>>> Patches => patches;

        /// <summary>
        /// Registers a collection of patch sets
        /// </summary>
        /// <param name="newPatchCollection">The collection of patch sets to add</param>
        public void RegisterPatchSets(Dictionary<string, Dictionary<string, T>> newPatchCollection)
        {

            foreach (var newPatches in newPatchCollection)
            {
                RegisterPatchSets(newPatches.Key, newPatches.Value);
            }

        }

        /// <summary>
        /// Registers a series of patch sets
        /// </summary>
        /// <param name="sceneName">The scene the patches are for</param>
        /// <param name="newPatches">A series of patch sets to be added</param>
        public void RegisterPatchSets(string sceneName, Dictionary<string, T> newPatches)
        {
            foreach (var newPatchSet in newPatches)
            {
                RegisterPatchSet(sceneName, newPatchSet.Key, newPatchSet.Value);
            }
        }

        /// <summary>
        /// Registers a set of patches
        /// </summary>
        /// <param name="sceneName">The scene the patches are for</param>
        /// <param name="objectName">The object the patches to be applied to</param>
        /// <param name="newPatchSet">The set of patches to be added</param>
        /// <returns>If the patch set was added</returns>
        public bool RegisterPatchSet(string sceneName, string objectName, T newPatchSet)
        {
            if (patches.ContainsKey(sceneName))
            {
                if (patches[sceneName].ContainsKey(objectName))
                {
                    if (patches[sceneName][objectName].Contains(newPatchSet))
                        return false;
                    else
                        patches[sceneName][objectName].Add(newPatchSet);
                }
            }
            else
                patches[sceneName] = new() { { objectName, [newPatchSet] } };

            return true;
        }

        /// <summary>
        /// Unregisters a collection of patch sets
        /// </summary>
        /// <param name="oldPatchCollection">The collection of patch sets to remove</param>
        public void UnregisterPatchSets(Dictionary<string, Dictionary<string, T>> oldPatchCollection)
        {
            foreach (var oldPatches in oldPatchCollection)
            {
                UnregisterPatchSets(oldPatches.Key, oldPatches.Value);
            }
        }

        /// <summary>
        /// Unregisters a series of patch sets
        /// </summary>
        /// <param name="sceneName">The scene the patches were for</param>
        /// <param name="oldPatches">A series of patch sets to be removed</param>
        public void UnregisterPatchSets(string sceneName, Dictionary<string, T> oldPatches)
        {
            foreach (var oldPatchSet in oldPatches)
            {
                UnregisterPatchSet(sceneName, oldPatchSet.Key, oldPatchSet.Value);
            }
        }

        /// <summary>
        /// Unregisters a set of patches
        /// </summary>
        /// <param name="sceneName">The scene the patches were for</param>
        /// <param name="objectName">The object the patches were applied to</param>
        /// <param name="oldPatchSet">The set of patches to be removed</param>
        /// <returns>If the patch set was removed</returns>
        public bool UnregisterPatchSet(string sceneName, string objectName, T oldPatchSet)
        {
            if (patches.ContainsKey(sceneName))
            {
                if (patches[sceneName].ContainsKey(objectName))
                {
                    patches[sceneName][objectName].Remove(oldPatchSet);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Applies the patches to the given Scene
        /// </summary>
        /// <param name="scene">The scene to be patched</param>
        /// <param name="objects">The objects within the scene</param>
        public abstract void ApplyPatches(Scene scene, HashSet<GameObject> objects);

        /// <summary>
        /// Removes the patches applied to the given Scene
        /// </summary>
        /// <param name="scene">The scene that the patches were applied to</param>
        /// <param name="objects">The objects within the scene</param>
        public abstract void RemovePatches(Scene scene, HashSet<GameObject> objects);
    }
    #endregion

    #region Enemy
    /// <summary>
    /// Class for containing a set of enemy patches
    /// </summary>
    public class EnemyObject : GenericEnemyPatch<ObjectPatch>
    {
        public override void ApplyPatches(string enemyName, GameObject enemyObject)
        {
            if (Patches.TryGetValue(enemyName, out HashSet<ObjectPatch> patchSet))
            {
                foreach (var setPatch in patchSet)
                    setPatch.ApplyPatch(enemyObject);
            }
        }

        public override void RemovePatches(string enemyName, GameObject enemyObject)
        {
            if (Patches.TryGetValue(enemyName, out HashSet<ObjectPatch> patchSet))
            {
                foreach (var setPatch in patchSet)
                    setPatch.RemovePatch(enemyObject);
            }
        }
    }

    /// <summary>
    /// A set of enemy object patches
    /// </summary>
    public static readonly EnemyObject enemyObject = new();

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
    /// Apply the various enemy patches
    /// </summary>
    /// <param name="enemyHealthManager">The enemy to patch</param>
    internal static void ApplyEnemyPatches(HealthManager enemyHealthManager)
    {
        string enemyName = enemyHealthManager.name;

        int cullIndex = enemyName.IndexOf('(') - 1;
        if (cullIndex > 0) enemyName = enemyName[..cullIndex];

        if (!enemyObject.PatchedEnemies.ContainsKey(enemyName))
            return;

        GameObject obj = enemyHealthManager.gameObject;

        enemyObject.ApplyPatches(enemyName, obj);

        patchedEnemies.Add(enemyHealthManager);
    }

    /// <summary>
    /// Called to remove all enemy patches
    /// </summary>
    internal static void RemoveEnemyPatches()
    {
        foreach (var enemy in patchedEnemies)
            RemoveEnemyPatches(enemy);
    }

    /// <summary>
    /// Remove the various enemy patches
    /// </summary>
    /// <param name="enemyHealthManager">The enemy to unpatch</param>
    internal static void RemoveEnemyPatches(HealthManager enemyHealthManager)
    {
        string enemyName = enemyHealthManager.name;

        if (!enemyObject.PatchedEnemies.ContainsKey(enemyName))
            return;

        GameObject obj = enemyHealthManager.gameObject;

        enemyObject.RemovePatches(enemyName, obj);

        patchedEnemies.Remove(enemyHealthManager);
    }

    public abstract class GenericEnemyPatch<T>
    {
        private readonly Dictionary<string, int> patchedEnemies = [];
        public Dictionary<string, int> PatchedEnemies => patchedEnemies;

        private readonly Dictionary<string, HashSet<T>> patches = [];
        /// <summary>
        /// The collection of patches that this contains
        /// </summary>
        public Dictionary<string, HashSet<T>> Patches => patches;

        /// <summary>
        /// Registers a series of patch sets
        /// </summary>
        /// <param name="sceneName">The scene the patches are for</param>
        /// <param name="newPatches">A series of patch sets to be added</param>
        public void RegisterPatchSets(Dictionary<string, T> newPatches)
        {
            foreach (var newPatchSet in newPatches)
            {
                RegisterPatchSet(newPatchSet.Key, newPatchSet.Value);
            }
        }

        /// <summary>
        /// Registers a set of patches
        /// </summary>
        /// <param name="sceneName">The scene the patches are for</param>
        /// <param name="objectName">The object the patches to be applied to</param>
        /// <param name="newPatchSet">The set of patches to be added</param>
        /// <returns>If the patch set was added</returns>
        public bool RegisterPatchSet(string enemyName, T newPatchSet)
        {
            if (patches.ContainsKey(enemyName))
            {
                if (patches[enemyName].Contains(newPatchSet))
                    return false;
                else
                    patches[enemyName].Add(newPatchSet);
            }
            else
                patches[enemyName] = [newPatchSet];

            if (patchedEnemies.ContainsKey(enemyName))
                patchedEnemies[enemyName]++;
            else
                patchedEnemies.Add(enemyName, 1);

            return true;
        }

        /// <summary>
        /// Unregisters a series of patch sets
        /// </summary>
        /// <param name="sceneName">The scene the patches were for</param>
        /// <param name="oldPatches">A series of patch sets to be removed</param>
        public void UnregisterPatchSets(Dictionary<string, T> oldPatches)
        {
            foreach (var oldPatchSet in oldPatches)
            {
                UnregisterPatchSet(oldPatchSet.Key, oldPatchSet.Value);
            }
        }

        /// <summary>
        /// Unregisters a set of patches
        /// </summary>
        /// <param name="sceneName">The scene the patches were for</param>
        /// <param name="objectName">The object the patches were applied to</param>
        /// <param name="oldPatchSet">The set of patches to be removed</param>
        /// <returns>If the patch set was removed</returns>
        public bool UnregisterPatchSet(string enemyName, T oldPatchSet)
        {
            if (patches.ContainsKey(enemyName))
            {
                patches[enemyName].Remove(oldPatchSet);

                if (patchedEnemies.ContainsKey(enemyName))
                {
                    if (patchedEnemies[enemyName] == 1)
                        patchedEnemies.Remove(enemyName);
                    else
                        patchedEnemies[enemyName]--;
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Applies the patches to the given enemy
        /// </summary>
        /// <param name="enemyName">The name of the enemy to patch</param>
        /// <param name="enemyObject">The enemies' GameObject to be passed along</param>
        /// <param name="enemyFSM">The PlayMakerFSM to patch</param>
        public abstract void ApplyPatches(string enemyName, GameObject enemyObject);

        /// <summary>
        /// Removes the patches to the given enemy
        /// </summary>
        /// <param name="enemyName">The name of the enemy to unpatch</param>
        /// <param name="enemyObject">The enemies' GameObject to be passed along</param>
        /// <param name="enemyFSM">The PlayMakerFSM to unpatch</param>
        public abstract void RemovePatches(string enemyName, GameObject enemyObject);
    }

    #endregion
}

/// <summary>
/// Contains a patch for a Unity GameObject
/// </summary>
public class ObjectPatch(string objectName, Action<GameObject, object[]?> patch, Action<GameObject, object[]?>? unpatch = null)
    : ObjectPatch_Base<GameObject, GameObject>(objectName, patch, unpatch)
{
    public override void ApplyPatch(GameObject obj, object[]? param = null) => Patch(obj, param);


    public override void RemovePatch(GameObject obj, object[]? param = null) => Unpatch?.Invoke(obj, param);
}

/// <summary>
/// Base class of patches that want to patch objects
/// </summary>
/// <typeparam name="T1">The type of patch</typeparam>
/// <typeparam name="T2">The type of object the patch is applying to</typeparam>
/// <param name="name">Name of the object the patch is for</param>
/// <param name="patch">The delegate to patch the object</param>
/// <param name="unpatch">Optional delegate to unpatch the object</param>
public abstract class ObjectPatch_Base<T1, T2>(string name, Action<T1, object[]?> patch, Action<T1, object[]?>? unpatch = null)
{
    /// <summary>
    /// Name of the object the patch is for
    /// </summary>
    public string Name { get; } = name;
    /// <summary>
    /// The delegate to patch the object
    /// </summary>
    public Action<T1, object[]?> Patch { get; } = patch;
    /// <summary>
    /// Optional delegate to unpatch the object
    /// </summary>
    public Action<T1, object[]?>? Unpatch { get; } = unpatch;

    /// <summary>
    /// Patches an object
    /// </summary>
    /// <param name="obj">The object to patch</param>
    /// <param name="param">An array of extra arguments the patcher may want</param>
    public abstract void ApplyPatch(T2 obj, object[]? param = null);

    /// <summary>
    /// Unpatches an object
    /// </summary>
    /// <param name="obj">The object to patch</param>
    /// <param name="param">An array of extra arguments the patcher may want</param>
    public abstract void RemovePatch(T2 obj, object[]? param = null);
}

/// <summary>
/// Base class of patch sets for patching objects
/// </summary>
/// <typeparam name="T1">The type of patch</typeparam>
/// <typeparam name="T2">The type of object the patch is applying to</typeparam>
/// <param name="name">Name of the object the patch is for</param>
/// <param name="patches">List of patches for the object</param>
public abstract class ObjectPatchSet_Base<T1, T2>(string name, List<T1> patches)
{
    /// <summary>
    /// The name of the object the patches are for
    /// </summary>
    public string Name { get; } = name;
    /// <summary>
    /// The list of patches for the object
    /// </summary>
    public List<T1> Patches { get; } = patches;

    /// <summary>
    /// Applies the patches to the given target when called
    /// </summary>
    /// <param name="patchTarget">The target to apply patches to</param>
    /// <param name="param">An array of extra arguments that patches may want</param>
    public abstract void ApplyPatches(T2 patchTarget, object[]? param = null);

    /// <summary>
    /// Applies the patches to the given targets when called
    /// </summary>
    /// <param name="patchTargets">The target to apply patches to</param>
    /// <param name="param">An array of extra arguments that patches may want</param>
    public abstract void ApplyPatches(T2[] patchTargets, object[]? param = null);

    /// <summary>
    /// Removes the patches from the given target when called
    /// </summary>
    /// <param name="patchTarget">The target to remove patches from</param>
    /// <param name="param">An array of extra arguments that unpatchers may want</param>
    public abstract void RemovePatches(T2 patchTarget, object[]? param = null);

    /// <summary>
    /// Removes the patches from the given targets when called
    /// </summary>
    /// <param name="patchTargets">The targets to remove patches from</param>
    /// <param name="param">An array of extra arguments that unpatchers may want</param>
    public abstract void RemovePatches(T2[] patchTargets, object[]? param = null);

    /// <summary>
    /// Adds a patch to the pre-existing list of patches.
    /// </summary>
    /// <param name="patch">The patch to be added</param>
    public abstract void RegisterPatch(T1 patch);

    /// <summary>
    /// Removes a patch from the pre-existing list of patches.
    /// </summary>
    /// <param name="patch">The patch to be removed</param>
    public abstract void UnregisterPatch(T1 patch);
}
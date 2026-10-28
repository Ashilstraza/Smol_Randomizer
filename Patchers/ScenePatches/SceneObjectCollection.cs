using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using UnityEngine.SceneManagement;

namespace Smol_Randomizer.Patchers.ScenePatches;

public class SceneObjectCollection : ObjectPatchCollection_Base<SceneObjectPatch, SceneObjectPatchSet, GameObject, GameObject>
{
    /// <summary>Make a singleton of this class</summary>
    private static readonly Lazy<SceneObjectCollection> instance = new(() => new SceneObjectCollection());

    /// <summary>A set of <see cref="Scene"/> object patches</summary>
    public static SceneObjectCollection Instance => instance.Value;

    private HashSet<GameObject> patchedSceneObjects = [];

    private readonly Dictionary<string, Dictionary<string, int>> patchedObjects = [];
    public new Dictionary<string, Dictionary<string, int>> PatchedObjects => patchedObjects;

    public new Dictionary<string, Dictionary<string, HashSet<SceneObjectPatch>>> Patches { get; } = [];

    /// <summary>Called when the scene loads to clear out unloaded enemies</summary>
    internal static void OnSceneLoaded()
    {
        Instance.patchedSceneObjects.RemoveWhere(enemy => enemy == null);
    }

    internal static void ApplyPatches(Scene scene)
    {
        if (!scene.IsValid()) // Something is fishy, abort.
            return;

        HashSet<GameObject> objects = scene.GetRootGameObjects().ToHashSet();

        Instance.ApplyPatches(scene, objects);
    }

    public void ApplyPatches(Scene scene, HashSet<GameObject> objects)
    {
        if (Patches.TryGetValue(scene.name, out Dictionary<string, HashSet<SceneObjectPatch>> patches))
        {
            foreach (var patchGroup in patches)
            {
                foreach (var setPatch in patchGroup.Value)
                    setPatch.ApplyPatch(objects.FirstOrDefault(obj => obj.name.Equals(patchGroup.Key)), []);
            }
        }
    }

    internal static void RemovePatches()
    {
        Instance.RemovePatches(CuteRandoCore.CurrentScene, Instance.patchedSceneObjects);
    }

    internal static void RemovePatches(Scene scene)
    {
        if (!scene.IsValid()) // Something is fishy, abort.
            return;

        HashSet<GameObject> objects = scene.GetRootGameObjects().ToHashSet();

        Instance.RemovePatches(scene, objects);
    }

    public void RemovePatches(Scene scene, HashSet<GameObject> objects)
    {
        if (Patches.TryGetValue(scene.name, out Dictionary<string, HashSet<SceneObjectPatch>> patches))
        {
            foreach (var patchGroup in patches)
            {
                foreach (var setPatch in patchGroup.Value)
                    setPatch.RemovePatch(objects.FirstOrDefault(obj => obj.name.Equals(patchGroup.Key)), []);
            }
        }
    }

    /// <summary>Register a collection of patches</summary>
    /// <param name="patchCollection">The dictionary containing the patches to be applied to <see cref="GameObject" />s</param>
    public virtual void RegisterPatchCollection(Dictionary<string, Dictionary<string, SceneObjectPatch>> patchCollection)
    {
        foreach (var patchGroup in patchCollection)
        {
            foreach (var patchSet in patchGroup.Value)
            {
                RegisterPatchInCollection(patchGroup.Key, patchSet.Key, patchSet.Value);
            }
        }
    }

    /// <summary>Register a collection of patches</summary>
    /// <param name="sceneName">      Name of the <see cref="Scene" /> that the <see cref="GameObject" /> is within</param>
    /// <param name="objectName">     Name of the <see cref="GameObject" /></param>
    /// <param name="patchCollection">List containing patches to apply</param>
    public virtual void RegisterPatchCollection(string sceneName, string objectName, List<SceneObjectPatch> patchCollection)
    {
        foreach (var patch in patchCollection)
        {
            RegisterPatchInCollection(sceneName, objectName, patch);
        }
    }

    /// <summary>Register a patch for an <see cref="GameObject" /> in a <see cref="Scene" /></summary>
    /// <param name="sceneName"> Name of the <see cref="Scene" /> that the <see cref="GameObject" /> is within</param>
    /// <param name="objectName">Name of the <see cref="GameObject" /></param>
    /// <param name="scenePatch"> Patch to apply</param>
    public virtual void RegisterPatchInCollection(string sceneName, string objectName, ISceneObjectPatch scenePatch)
    {
        if (scenePatch is SceneObjectPatch patch)
        {
            if (Patches.ContainsKey(sceneName))
            {
                if (Patches[sceneName].ContainsKey(objectName))
                {
                    if (!Patches[sceneName][objectName].Add(patch))
                        return; // The patch is already in there
                }
                else
                    Patches[sceneName][objectName] = new() { { patch } };
            }
            else
                Patches[sceneName] = new() { { objectName, [patch] } };

            if (PatchedObjects.ContainsKey(sceneName))
            {
                if (PatchedObjects[sceneName].ContainsKey(objectName))
                {
                    PatchedObjects[sceneName][objectName]++;
                }
                else
                    PatchedObjects[sceneName].Add(objectName, 1);
            }
            else
                PatchedObjects[sceneName] = new() { { objectName, 1 } };
        }
        else if (scenePatch is SceneObjectPatchSet patchSet)
        {
            RegisterPatchCollection(sceneName, objectName, patchSet.Patches);
        }
        else
            CuteRandoCore.Log.LogWarning($"Register patch called with invalid patch type, Name: {scenePatch.Name}, Type: {scenePatch.GetType()}");
    }

    /// <summary>Unregister a collection of patches</summary>
    /// <param name="patchCollection">The dictionary containing the patches to be removed from <see cref="GameObject" />s</param>
    public virtual void UnregisterPatchCollection(Dictionary<string, Dictionary<string, SceneObjectPatch>> patchCollection)
    {
        foreach (var patchGroup in patchCollection)
        {
            foreach (var patchSet in patchGroup.Value)
            {
                UnregisterPatchInCollection(patchGroup.Key, patchSet.Key, patchSet.Value);
            }
        }
    }

    /// <summary>Unregister a collection of patches</summary>
    /// <param name="sceneName">      Name of the <see cref="Scene" /> that the <see cref="GameObject" /> is within</param>
    /// <param name="objectName">     Name of the <see cref="GameObject" /></param>
    /// <param name="patchCollection">List containing patches to remove</param>
    public virtual void UnregisterPatchCollection(string sceneName, string objectName, List<SceneObjectPatch> patchCollection)
    {
        foreach (var patch in patchCollection)
        {
            UnregisterPatchInCollection(sceneName, objectName, patch);
        }
    }

    /// <summary>Unregister a patch for an object in a <see cref="Scene" /></summary>
    /// <param name="sceneName"> Name of the <see cref="Scene" /> that the <see cref="GameObject" /> is within</param>
    /// <param name="objectName">Name of the <see cref="GameObject" /></param>
    /// <param name="scenePatch"> Patch to remove</param>
    public virtual void UnregisterPatchInCollection(string sceneName, string objectName, ISceneObjectPatch scenePatch)
    {
        if (scenePatch is SceneObjectPatch patch)
        {
            if (Patches.ContainsKey(sceneName))
            {
                if (Patches[sceneName].ContainsKey(objectName))
                {
                    if (Patches[sceneName][objectName].Remove(patch))
                    {
                        if (PatchedObjects[sceneName][objectName] == 1)
                            PatchedObjects[sceneName].Remove(objectName);
                        else
                            PatchedObjects[sceneName][objectName]--;
                    }
                    if (PatchedObjects[sceneName].Count == 0)
                        PatchedObjects.Remove(sceneName);
                }
            }
        }
        else if (scenePatch is SceneObjectPatchSet patchSet)
        {
            RegisterPatchCollection(sceneName, objectName, patchSet.Patches);
        }
        else
            CuteRandoCore.Log.LogWarning($"Register patch called with invalid patch {scenePatch.Name}");
    }

    #region Overridden Register/Unregister Methods

#pragma warning disable CS0809 // Obsolete member overrides non-obsolete member

    [Obsolete("Class Requires Dictionary{string, Dictionary{string, PatchType}} for Scene Detection", true)]
    public override void RegisterPatchCollection(List<SceneObjectPatch> patchCollection) { }

    [Obsolete("Class Requires Dictionary{string, Dictionary{string, ISmolPatch}} for Scene Detection", true)]
    public override void RegisterPatchInCollection(string objectName, ISmolPatch smolPatch) { }

    [Obsolete("Class Requires Dictionary{string, Dictionary{string, PatchType}} for Scene Detection", true)]
    public override void UnregisterPatchCollection(List<SceneObjectPatch> patchCollection) { }

    [Obsolete("Class Requires Dictionary{string, Dictionary{string, ISmolPatch}} for Scene Detection", true)]
    public override void UnregisterPatchInCollection(string objectName, ISmolPatch smolPatch) { }

#pragma warning restore CS0809 // Obsolete member overrides non-obsolete member

    #endregion Overridden Register/Unregister Methods
}

public class SceneObjectPatchSet(string sceneName, string objectName, List<SceneObjectPatch> patches)
    : ObjectPatchSet_Base<SceneObjectPatch, GameObject, GameObject>(objectName, patches), ISceneObjectPatchSet
{
    public SceneObjectPatchSet(string sceneName, string[] objectNames, List<SceneObjectPatch> patches)
        : this(sceneName, "", patches)
    {
        NameArray = objectNames;
    }
    public string SceneName => sceneName;

    public string ObjectName { get; } = objectName;

    public override object Clone()
    {
        List<SceneObjectPatch> newList = [];

        foreach (var patch in Patches)
            newList.Add(patch);

        if (NameArray.Length > 0)
            return new SceneObjectPatchSet(SceneName, NameArray, newList);
        return new SceneObjectPatchSet(SceneName, Name, newList);
    }
}

public class SceneObjectPatch(string sceneName, string objectName, Action<GameObject, object[]> patch, Action<GameObject, object[]>? unpatch = null)
    : ObjectPatch_Base<GameObject, GameObject>(objectName, patch, unpatch), ISceneObjectPatch
{
    public SceneObjectPatch(string sceneName, string[] objectNames, Action<GameObject, object[]> patch, Action<GameObject, object[]>? unpatch = null)
        : this(sceneName, "", patch, unpatch)
    {
        NameArray = objectNames;
    }

    public string SceneName => sceneName;

    public string ObjectName { get; } = objectName;

    public override void ApplyPatch(GameObject patchTarget, object[] param)
    {
        Patch(patchTarget, param);
    }

    public override void ApplyPatch(GameObject[] patchTargets, object[] param)
    {
        foreach (var target in patchTargets)
        {
            ApplyPatch(target, param);
        }
    }

    public override void RemovePatch(GameObject patchTarget, object[] param)
    {
        if (Unpatch == null) return;
        Unpatch(patchTarget, param);
    }

    public override void RemovePatch(GameObject[] patchTargets, object[] param)
    {
        foreach (var target in patchTargets)
        {
            RemovePatch(target, param);
        }
    }

    public override object Clone()
    {
        if (NameArray.Length > 0)
            return new SceneObjectPatch(SceneName, NameArray, (Action<GameObject, object[]>)Patch.Clone(), (Action<GameObject, object[]>?)Unpatch?.Clone());
        return new SceneObjectPatch(SceneName, Name, (Action<GameObject, object[]>)Patch.Clone(), (Action<GameObject, object[]>?)Unpatch?.Clone());
    }
}

/// <summary>Marks a patch as a Scene Object Patch</summary>
public interface ISceneObjectPatch : ISmolPatch, IScenePatch;

/// <summary>Marks a patch set as a Scene Object Patch Set</summary>
public interface ISceneObjectPatchSet : ISceneObjectPatch, ISmolPatchSet;
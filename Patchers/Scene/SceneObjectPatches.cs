using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;

namespace Smol_Randomizer.Patchers.Scene;

/// <summary>
/// Handles patching Scene Objects
/// </summary>
public class SceneObjectPatchCollection : ObjectPatchCollection_Base<SceneObjectPatch, SceneObjectPatchSet, HashSet<GameObject>, GameObject>
{
    private static readonly Lazy<SceneObjectPatchCollection> instance = new(() => new SceneObjectPatchCollection());
    /// <summary>
    /// A set of enemy object patches
    /// </summary>
    public static SceneObjectPatchCollection Instance => instance.Value;

    /// <summary>
    /// The collection of patches that this contains, is formatted {Scene name, {GameObject name, Patch}}
    /// </summary>
    public new Dictionary<string, Dictionary<string, HashSet<SceneObjectPatch>>> Patches { get; } = [];

    public override void ApplyPatches(string scene, HashSet<GameObject> objects)
    {
        if (Patches.TryGetValue(scene, out Dictionary<string, HashSet<SceneObjectPatch>> patches))
        {
            foreach (var patchGroup in patches)
            {
                foreach (var patch in patchGroup.Value)
                {
                    patch.ApplyPatch(objects, [patchGroup.Key]);
                }
            }
        }
    }

    public override void RemovePatches(string scene, HashSet<GameObject> objects)
    {
        if (Patches.TryGetValue(scene, out Dictionary<string, HashSet<SceneObjectPatch>> patches))
        {
            foreach (var patchGroup in patches)
            {
                foreach (var patch in patchGroup.Value)
                    patch.RemovePatch(objects, [patchGroup.Key]);
            }
        }
    }

    /// <summary>
    /// Apply the various scene patches
    /// </summary>
    /// <param name="scene">The scene to patch</param>
    public static void ApplyPatches(UnityEngine.SceneManagement.Scene scene)
    {
        HashSet<GameObject> objects = scene.GetRootGameObjects().ToHashSet();

        Instance.ApplyPatches(scene.name, objects);
    }

    /// <summary>
    /// Remove the various scene patches
    /// </summary>
    /// <param name="scene">The scene to unpatch</param>
    public static void RemovePatches(UnityEngine.SceneManagement.Scene scene)
    {
        HashSet<GameObject> objects = scene.GetRootGameObjects().ToHashSet();

        Instance.RemovePatches(scene.name, objects);
    }
}

/// <summary>
/// Set of patches for scene objects
/// </summary>
/// <param name="objectName">The object name to patch</param>
/// <param name="patches">The set of patches to apply to the scene object</param>
public class SceneObjectPatchSet(string objectName, List<SceneObjectPatch> patches)
    : ObjectPatchSet_Base<SceneObjectPatch, HashSet<GameObject>, GameObject>(objectName, patches)
{
    /// <summary>
    /// Set of patches for scene objects
    /// </summary>
    /// <param name="objectNames">Array of object names to patch</param>
    /// <param name="patches">The set of patches to apply to the scene object</param>
    public SceneObjectPatchSet(string[] objectNames, List<SceneObjectPatch> patches)
        : this("", patches)
    {
        NameArray = objectNames;
    }
    public override object Clone()
    {
        List<SceneObjectPatch> newPatchList = [];
        foreach (var patch in Patches)
        {
            newPatchList.Add((SceneObjectPatch)patch.Clone());
        }
        if (Name != "")
            return new SceneObjectPatchSet(Name, newPatchList);
        return new SceneObjectPatchSet(NameArray, newPatchList);
    }
}

/// <summary>
/// Patch for a scene object
/// </summary>
/// <param name="objectName">The name of the object to patch</param>
/// <param name="patch">The patch for the object</param>
/// <param name="unpatch">Optional unpatcher for undoing the changes</param>
public class SceneObjectPatch(string objectName, Action<GameObject, object[]?> patch, Action<GameObject, object[]?>? unpatch = null)
        : ObjectPatch_Base<GameObject, HashSet<GameObject>>(objectName, patch, unpatch)
{
    /// <summary>
    /// Patch for a scene object
    /// </summary>
    /// <param name="objectNames">Array names for objects to patch</param>
    /// <param name="patch">The patch for the object</param>
    /// <param name="unpatch">Optional unpatcher for undoing the changes</param>
    public SceneObjectPatch(string[] objectNames, Action<GameObject, object[]?> patch, Action<GameObject, object[]?>? unpatch = null)
        : this("", patch, unpatch)
    {
        NameArray = objectNames;
    }
    public override void ApplyPatch(HashSet<GameObject> patchTarget, object[]? param = null)
    {
        if (param == null) return;
        Patch(patchTarget.FirstOrDefault(obj => obj.name.Equals(param[0])), param);
    }

    public override void RemovePatch(HashSet<GameObject> patchTarget, object[]? param = null)
    {
        if (Unpatch == null || param == null) return;
        Unpatch(patchTarget.FirstOrDefault(obj => obj.name.Equals(param[0])), param);
    }

    public override object Clone()
    {
        if (Name != "")
            return new SceneObjectPatch(Name, (Action<GameObject, object[]?>)Patch.Clone(), (Action<GameObject, object[]?>?)Unpatch?.Clone());
        return new SceneObjectPatch(NameArray, (Action<GameObject, object[]?>)Patch.Clone(), (Action<GameObject, object[]?>?)Unpatch?.Clone());
    }
}

/// <summary>
/// Marks a patch as a Scene Object Patch
/// </summary>
public interface ISceneObjectPatch : ISmolPatch;

/// <summary>
/// Marks a patch set as a Scene Object Patch Set, include the interface to additionally mark it as a patch
/// </summary>
public interface ISceneObjectPatchSet : ISceneObjectPatch, ISmolPatchSet;
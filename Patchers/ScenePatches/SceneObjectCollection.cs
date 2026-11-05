using HutongGames.PlayMaker;

using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using UnityEngine.SceneManagement;

namespace Smol_Randomizer.Patchers.ScenePatches;

public class SceneObjectCollection : ScenePatchCollections_Base<SceneObjectPatch, SceneObjectPatchSet, GameObject, GameObject>
{
    /// <summary>Make a singleton of this class</summary>
    private static readonly Lazy<SceneObjectCollection> instance = new(() => new SceneObjectCollection());

    /// <summary>A set of <see cref="Scene"/> object patches</summary>
    public static SceneObjectCollection Instance => instance.Value;

    private HashSet<GameObject> patchedSceneObjects = [];

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

    public override bool ApplyPatches(Scene scene, HashSet<GameObject> objects)
    {
        if (Patches.TryGetValue(scene.name, out Dictionary<string, HashSet<SceneObjectPatch>> patches))
        {
            foreach (var patchGroup in patches)
            {
                foreach (var setPatch in patchGroup.Value)
                {
                    var filteredObjects = objects.Where(obj => CuteRandoCore.CullName(obj.name).Equals(patchGroup.Key)).ToArray();

                    foreach (var obj in filteredObjects)
                    {
                        setPatch.ApplyPatch(obj, [obj, patchGroup.Key, scene, scene.name]);

                        patchedSceneObjects.Add(obj);
                    }
                } 
            }

            return true;
        }

        return false;
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

    public override bool RemovePatches(Scene scene, HashSet<GameObject> objects)
    {
        if (Patches.TryGetValue(scene.name, out Dictionary<string, HashSet<SceneObjectPatch>> patches))
        {
            foreach (var patchGroup in patches)
            {
                foreach (var setPatch in patchGroup.Value)
                {
                    var filteredObjects = objects.Where(obj => CuteRandoCore.CullName(obj.name).Equals(patchGroup.Key)).ToArray();

                    foreach (var obj in filteredObjects)
                    {
                        setPatch.RemovePatch(obj, [obj, patchGroup.Key, scene, scene.name]);

                        patchedSceneObjects.Remove(obj);
                    }
                }
            }

            return true;
        }

        return false;
    }

    /// <summary>Registers a collection of patches</summary>
    /// <param name="scenePatchCollection">The collection of patches to add</param>
    public static void RegisterPatchCollection(List<ISceneObjectPatch> scenePatchCollection)
    {
        int patchCount = 0;

        foreach (var patch in scenePatchCollection)
        {
            if (patch is SceneObjectPatch or SceneObjectPatchSet)
                patchCount += Instance.RegisterPatchInCollection(patch.Name, patch, patch.SceneName);
            else
                CuteRandoCore.DebugLog($"Register patch called with invalid patch type, Name: {patch.Name}, Type: {patch.GetType()} Scene: {patch.SceneName}", DebugLogOptions.Error, true);
        }

        CuteRandoCore.Log.LogInfo($"Registered {patchCount} Scene Object Patches");
    }

    /// <summary>Unregisters a collection of patches</summary>
    /// <param name="scenePatchCollection">The collection of patches to remove</param>
    public static void UnregisterPatchCollection(List<ISceneObjectPatch> scenePatchCollection)
    {
        int patchCount = 0;

        foreach (var patch in scenePatchCollection)
        {
            if (patch is SceneObjectPatch or SceneObjectPatchSet)
                patchCount += Instance.UnregisterPatchInCollection(patch.Name, patch, patch.SceneName);
            else
                CuteRandoCore.DebugLog($"Unregister patch called with invalid patch type, Name: {patch.Name}, Type: {patch.GetType()} Scene: {patch.SceneName}", DebugLogOptions.Error, true);
        }

        CuteRandoCore.Log.LogInfo($"Unregistered {patchCount} Scene Object Patches");
    }
}

/// <summary>Contains a set of patches for a game object for a specific scene</summary>
/// <param name="sceneName"> Name of the <see cref="Scene" /> that the <see cref="GameObject" /> is within</param>
/// <param name="objectName">Name of the <see cref="GameObject" /> being patched</param>
/// <param name="patches">   The set of patches for the <see cref="GameObject" /></param>
/// <param name="objectNames"><see cref="Array" /> of <see cref="GameObject" /> names to patch</param>
/// <param name="sceneNames"><see cref="Array" /> of <see cref="Scene" /> names to patch</param>
public class SceneObjectPatchSet : ObjectPatchSet_Base<SceneObjectPatch, GameObject, GameObject>, ISceneObjectPatchSet
{
    /// <inheritdoc cref="SceneObjectPatchSet"/>
    public SceneObjectPatchSet(string sceneName, string objectName, List<SceneObjectPatch> patches) 
        : base(objectName, patches)
    {
        SceneName = sceneName;

        PopulateChildFields();
    }

    /// <inheritdoc cref="SceneObjectPatchSet" />
    private SceneObjectPatchSet(string sceneName, string[] objectNames, List<SceneObjectPatch> patches)
        : this(sceneName, "", patches)
    {
        NameArray = objectNames;

        PopulateChildFields();
    }

    /// <inheritdoc cref="SceneObjectPatchSet" />
    private SceneObjectPatchSet(string[] sceneNames, string objectName, List<SceneObjectPatch> patches)
        : this("", objectName, patches)
    {
        SceneArray = sceneNames;

        PopulateChildFields();
    }

    /// <inheritdoc cref="SceneObjectPatchSet" />
    private SceneObjectPatchSet(string[] sceneNames, string[] objectNames, List<SceneObjectPatch> patches)
        : this("", "", patches)
    {
        NameArray = objectNames;
        SceneArray = sceneNames;

        PopulateChildFields();
    }
    public string SceneName { get; set; }

    public string[] SceneArray { get; set; } = [];

    internal override void PopulateChildFields()
    {
        foreach (var patch in Patches)
        {
            if (patch.Name is "")
                patch.Name = Name;
            if (patch.NameArray is null or [])
                patch.NameArray = NameArray;
            if(patch.SceneName is "")
                patch.SceneName = SceneName;
            if(patch.SceneArray is null or [])
                patch.SceneArray = SceneArray;
        }
    }

    public override object Clone()
    {
        List<SceneObjectPatch> newList = [];

        foreach (var patch in Patches)
            newList.Add(patch);

        if (NameArray.Length > 0 && SceneArray.Length > 0)
            return new SceneObjectPatchSet(SceneArray, NameArray, newList);
        else if (SceneArray.Length > 0)
            return new SceneObjectPatchSet(SceneArray, Name, newList);
        else if (NameArray.Length > 0)
            return new SceneObjectPatchSet(SceneName, NameArray, newList);
        else
            return new SceneObjectPatchSet(SceneName, Name, newList);
    }
}

/// <summary>Contains a patch for a game object for a specific scene</summary>
/// <param name="sceneName"> Name of the <see cref="Scene" /> that the <see cref="GameObject" /> is within</param>
/// <param name="objectName">Name of the <see cref="GameObject" /> being patched</param>
/// <param name="patch">    The patch for modifying the <see cref="GameObject" /></param>
/// <param name="unpatch">  Optional: The patch for removing the <see cref="GameObject" /> modification</param>
/// <param name="objectNames"><see cref="Array" /> of <see cref="GameObject" /> names to patch</param>
/// <param name="sceneNames"><see cref="Array" /> of <see cref="Scene" /> names to patch</param>
public class SceneObjectPatch(string sceneName, string objectName, Action<GameObject, object[]> patch, Action<GameObject, object[]>? unpatch = null)
    : ObjectPatch_Base<GameObject, GameObject>(objectName, patch, unpatch), ISceneObjectPatch
{
    /// <inheritdoc cref="SceneObjectPatch"/>
    public SceneObjectPatch(string sceneName, string[] objectNames, Action<GameObject, object[]> patch, Action<GameObject, object[]>? unpatch = null)
        : this(sceneName, "", patch)
    {
        NameArray = objectNames;
    }

    /// <inheritdoc cref="SceneObjectPatch" />
    public SceneObjectPatch(string[] sceneNames, string objectName, Action<GameObject, object[]> patch, Action<GameObject, object[]>? unpatch = null)
        : this("", objectName, patch)
    {
        SceneArray = sceneNames;
    }

    /// <inheritdoc cref="SceneObjectPatch" />
    public SceneObjectPatch(string[] sceneNames, string[] objectNames, Action<GameObject, object[]> patch, Action<GameObject, object[]>? unpatch = null)
        : this("", "", patch)
    {
        SceneArray = sceneNames;
        NameArray = objectNames;
    }

    public string SceneName { get; set; } = sceneName;

    public string[] SceneArray { get; set; } = [];

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
        if (NameArray.Length > 0 && SceneArray.Length > 0)
            return new SceneObjectPatch(SceneArray, NameArray, (Action<GameObject, object[]>)Patch.Clone(), (Action<GameObject, object[]>?)Unpatch?.Clone());
        else if (NameArray.Length > 0)
            return new SceneObjectPatch(SceneName, NameArray, (Action<GameObject, object[]>)Patch.Clone(), (Action<GameObject, object[]>?)Unpatch?.Clone());
        else if (SceneArray.Length > 0)
            return new SceneObjectPatch(SceneArray, Name, (Action<GameObject, object[]>)Patch.Clone(), (Action<GameObject, object[]>?)Unpatch?.Clone());
        else
            return new SceneObjectPatch(SceneName, Name, (Action<GameObject, object[]>)Patch.Clone(), (Action<GameObject, object[]>?)Unpatch?.Clone());
    }
}

/// <summary>Marks a patch as a Scene Object Patch</summary>
public interface ISceneObjectPatch : IScenePatch;

/// <summary>Marks a patch set as a Scene Object Patch Set</summary>
public interface ISceneObjectPatchSet : ISceneObjectPatch, IScenePatchSet;
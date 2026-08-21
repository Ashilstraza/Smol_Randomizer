using System;
using System.Collections.Generic;
using System.Linq;

namespace Smol_Randomizer.Patchers;

/// <summary>
/// Base class of patches that want to patch objects
/// </summary>
/// <typeparam name="PatchTarget">The patch target</typeparam>
/// <typeparam name="ObjectType">The type of object the patch is applying to</typeparam>
/// <param name="name">Name of the object the patch is for</param>
/// <param name="patch">The delegate to patch the object</param>
/// <param name="unpatch">Optional delegate to unpatch the object</param>
public abstract class ObjectPatch_Base<PatchTarget, ObjectType>(string name, Action<PatchTarget, object[]?> patch, Action<PatchTarget, object[]?>? unpatch = null)
    : ISmolPatch
{
    public string Name { get; set; } = name;
    public string[] NameArray { get; set; } = [];

    /// <summary>
    /// The delegate to patch the object
    /// </summary>
    public Action<PatchTarget, object[]?> Patch { get; } = patch;

    /// <summary>
    /// Optional delegate to unpatch the object
    /// </summary>
    public Action<PatchTarget, object[]?>? Unpatch { get; } = unpatch;

    /// <summary>
    /// Patches an object
    /// </summary>
    /// <param name="patchTarget">The object to patch</param>
    /// <param name="param">An array of extra arguments the patcher may want</param>
    public abstract void ApplyPatch(ObjectType patchTarget, object[]? param = null);

    /// <summary>
    /// Unpatches an object
    /// </summary>
    /// <param name="patchTarget">The object to patch</param>
    /// <param name="param">An array of extra arguments the patcher may want</param>
    public abstract void RemovePatch(ObjectType patchTarget, object[]? param = null);
    public abstract object Clone();
}

/// <summary>
/// Base class of patch sets for patching objects
/// </summary>
/// <typeparam name="PatchType">The patch type</typeparam>
/// <typeparam name="ObjectType">The object type the patch is applying to</typeparam>
/// <typeparam name="PatchTarget">The patch target</typeparam>
/// <param name="name">Name of the object the patch is for</param>
/// <param name="patches">List of patches for the object</param>
public abstract class ObjectPatchSet_Base<PatchType, ObjectType, PatchTarget>(string name, List<PatchType> patches)
    : ISmolPatchSet
    where PatchType : ObjectPatch_Base<PatchTarget, ObjectType>
{
    public string Name { get; set; } = name;
    public string[] NameArray { get; set; } = [];
    /// <summary>
    /// The list of patches for the object
    /// </summary>
    public List<PatchType> Patches => patches;

    /// <summary>
    /// Applies the patches to the given target when called
    /// </summary>
    /// <param name="patchTarget">The target to apply patches to</param>
    /// <param name="param">An array of extra arguments that patches may want</param>
    public virtual void ApplyPatches(ObjectType patchTarget, object[]? param = null)
    {
        foreach (var patch in Patches)
        {
            patch.ApplyPatch(patchTarget, param);
        }
    }

    /// <summary>
    /// Applies the patches to the given targets when called
    /// </summary>
    /// <param name="patchTargets">The target to apply patches to</param>
    /// <param name="param">An array of extra arguments that patches may want</param>
    public virtual void ApplyPatches(ObjectType[] patchTargets, object[]? param = null)
    {
        foreach (var patchTarget in patchTargets)
        {
            foreach (var patch in Patches)
            {
                patch.ApplyPatch(patchTarget, param);
            }
        }
    }

    /// <summary>
    /// Removes the patches from the given target when called
    /// </summary>
    /// <param name="patchTarget">The target to remove patches from</param>
    /// <param name="param">An array of extra arguments that unpatchers may want</param>
    public virtual void RemovePatches(ObjectType patchTarget, object[]? param = null)
    {
        foreach (var patch in Patches)
        {
            patch.RemovePatch(patchTarget, param);
        }
    }

    /// <summary>
    /// Removes the patches from the given targets when called
    /// </summary>
    /// <param name="patchTargets">The targets to remove patches from</param>
    /// <param name="param">An array of extra arguments that unpatchers may want</param>
    public virtual void RemovePatches(ObjectType[] patchTargets, object[]? param = null)
    {
        foreach (var patchTarget in patchTargets)
        {
            foreach (var patch in Patches)
            {
                patch.RemovePatch(patchTarget, param);
            }
        }
    }

    /// <summary>
    /// Adds a patch to the pre-existing list of patches.
    /// </summary>
    /// <param name="patch">The patch to be added</param>
    public virtual void RegisterPatch(PatchType patch) => Patches.Add(patch);

    /// <summary>
    /// Adds a set of patchs to the pre-existing list of patches.
    /// </summary>
    /// <param name="patch">The set of patches to be added</param>
    public virtual void RegisterPatches(PatchType[] patchSet) => Patches.AddRange(patchSet);

    /// <summary>
    /// Removes a patch from the pre-existing list of patches.
    /// </summary>
    /// <param name="patch">The patch to be removed</param>
    public virtual void UnregisterPatch(PatchType patch) => Patches.Remove(patch);

    /// <summary>
    /// Removes a set of patches from the pre-existing list of patches.
    /// </summary>
    /// <param name="patchSet">The set of patches to be removed</param>
    public virtual void UnregisterPatches(PatchType[] patchSet) => Patches.RemoveAll(patchSet.Contains);

    public abstract object Clone();
}

public abstract class ObjectPatchCollection_Base<PatchType, PatchSetType, ObjectType, PatchTarget>
    where PatchType : ObjectPatch_Base<PatchTarget, ObjectType>, ISmolPatch
    where PatchSetType : ObjectPatchSet_Base<PatchType, ObjectType, PatchTarget>, ISmolPatchSet
{
    private readonly Dictionary<string, int> patchedObjects = [];
    /// <summary>
    /// Dictionary containing the names of objects to patch and how many patches are to be applied to them
    /// </summary>
    public Dictionary<string, int> PatchedObjects => patchedObjects;
    /// <summary>
    /// Dictionary containing a list of objects and the patches to apply to them
    /// </summary>
    public Dictionary<string, HashSet<PatchType>> Patches { get; } = [];

    /// <summary>
    /// Applies the patches to the given object
    /// </summary>
    /// <param name="objectName">Name of the object to patch</param>
    /// <param name="obj">The object to patch</param>
    public virtual void ApplyPatches(string objectName, ObjectType obj)
    {
        if (Patches.TryGetValue(objectName, out HashSet<PatchType> patchSet))
        {
            foreach (var patch in patchSet)
            {
                patch.ApplyPatch(obj);
            }
        }
    }

    /// <summary>
    /// Removes the patches from the given object
    /// </summary>
    /// <param name="objectName">Name of the object to unpatch</param>
    /// <param name="obj">The object to unpatch</param>
    public virtual void RemovePatches(string objectName, ObjectType obj)
    {
        if (Patches.TryGetValue(objectName, out HashSet<PatchType> patchSet))
        {
            foreach (var patch in patchSet)
            {
                patch.RemovePatch(obj);
            }
        }
    }

    /// <summary>
    /// Register a collection of patches
    /// </summary>
    /// <param name="patchCollection">The dictionary containing the patches to be applied to objects</param>
    public virtual void RegisterPatchCollection(Dictionary<string, ISmolPatch> patchCollection)
    {
        foreach (var patch in patchCollection)
        {
            RegisterPatchInCollection(patch.Key, patch.Value);
        }
    }

    /// <summary>
    /// Register a collection of patches
    /// </summary>
    /// <param name="patchCollection">The dictionary containing the patches to be applied to objects</param>
    public virtual void RegisterPatchCollection(Dictionary<string, PatchType> patchCollection)
    {
        foreach (var patch in patchCollection)
        {
            RegisterPatchInCollection(patch.Key, patch.Value);
        }
    }

    /// <summary>
    /// Register a collection of patches
    /// </summary>
    /// <param name="patchCollection">The dictionary containing the patches to be applied to an array of objects</param>
    public virtual void RegisterPatchCollection(Dictionary<string[], ISmolPatch> patchCollection)
    {
        foreach (var patch in patchCollection)
        {
            foreach (var obj in patch.Key)
            {
                RegisterPatchInCollection(obj, patch.Value);
            }
        }
    }

    /// <summary>
    /// Register a collection of patches
    /// </summary>
    /// <param name="patchCollection">The dictionary containing the patches to be applied to an array of objects</param>
    public virtual void RegisterPatchCollection(Dictionary<string[], PatchType> patchCollection)
    {
        foreach (var patch in patchCollection)
        {
            foreach (var obj in patch.Key)
            {
                RegisterPatchInCollection(obj, patch.Value);
            }
        }
    }

    /// <summary>
    /// Register a collection of patches
    /// </summary>
    /// <param name="patchCollection">A list containing the patches to be applied to an array of objects</param>
    public virtual void RegisterPatchCollection(List<ISmolPatch> patchCollection)
    {
        foreach (var patch in patchCollection)
        {
            RegisterPatchInCollection(patch.Name, patch);
        }
    }

    /// <summary>
    /// Register a collection of patches
    /// </summary>
    /// <param name="patchCollection">A list containing the patches to be applied to an array of objects</param>
    public virtual void RegisterPatchCollection(List<PatchType> patchCollection)
    {
        foreach (var patch in patchCollection)
        {
            RegisterPatchInCollection(patch.Name, patch);
        }
    }

    /// <summary>
    /// Registers the patch for the object
    /// </summary>
    /// <param name="objectName">The object's name that is to be patched</param>
    /// <param name="patch">The patch to register</param>
    public virtual void RegisterPatchInCollection(string objectName, ISmolPatch smolPatch)
    {
        if (smolPatch is PatchType patch)
        {
            if (Patches.ContainsKey(objectName))
            {
                if (!Patches[objectName].Add(patch))
                    return; // The patch is already in there
            }
            else
                Patches[objectName] = new() { { patch } };

            if (patchedObjects.ContainsKey(objectName))
                patchedObjects[objectName]++;
            else
                patchedObjects.Add(objectName, 1);
        }
        else if (smolPatch is PatchSetType patchSet)
        {
            RegisterPatchCollection(patchSet.Patches);
        }
        else
            CuteRandoCore.Log.LogWarning($"Register patch called with invalid patch {smolPatch.Name}");
    }

    /// <summary>
    /// Unregister a collection of patches
    /// </summary>
    /// <param name="patchCollection">The dictionary containing the patches to no longer be applied</param>
    public virtual void UnregisterPatchCollection(Dictionary<string, ISmolPatch> patchCollection)
    {
        foreach (var patch in patchCollection)
        {
            UnregisterPatchInCollection(patch.Key, patch.Value);
        }
    }

    /// <summary>
    /// Unregister a collection of patches
    /// </summary>
    /// <param name="patchCollection">The dictionary containing the patches to no longer be applied</param>
    public virtual void UnregisterPatchCollection(Dictionary<string, PatchType> patchCollection)
    {
        foreach (var patch in patchCollection)
        {
            UnregisterPatchInCollection(patch.Key, patch.Value);
        }
    }

    /// <summary>
    /// Unregister a collection of patches
    /// </summary>
    /// <param name="patchCollection">The dictionary containing the patches to no longer be applied</param>
    public virtual void UnregisterPatchCollection(Dictionary<string[], ISmolPatch> patchCollection)
    {
        foreach (var patch in patchCollection)
        {
            foreach (var obj in patch.Key)
            {
                UnregisterPatchInCollection(obj, patch.Value);
            }
        }
    }

    /// <summary>
    /// Unregister a collection of patches
    /// </summary>
    /// <param name="patchCollection">The dictionary containing the patches to no longer be applied</param>
    public virtual void UnregisterPatchCollection(Dictionary<string[], PatchType> patchCollection)
    {
        foreach (var patch in patchCollection)
        {
            foreach (var obj in patch.Key)
            {
                UnregisterPatchInCollection(obj, patch.Value);
            }
        }
    }

    /// <summary>
    /// Unregister a collection of patches
    /// </summary>
    /// <param name="patchCollection">A list containing the patches to no longer be applied<</param>
    public virtual void UnregisterPatchCollection(List<ISmolPatch> patchCollection)
    {
        foreach (var patch in patchCollection)
        {
            UnregisterPatchInCollection(patch.Name, patch);
        }
    }

    /// <summary>
    /// Unregister a collection of patches
    /// </summary>
    /// <param name="patchCollection">A list containing the patches to no longer be applied<</param>
    public virtual void UnregisterPatchCollection(List<PatchType> patchCollection)
    {
        foreach (var patch in patchCollection)
        {
            UnregisterPatchInCollection(patch.Name, patch);
        }
    }

    /// <summary>
    /// Unregisters the patch for the object
    /// </summary>
    /// <param name="objectName">The object's name that is to no longer be patched</param>
    /// <param name="patch">The patch to unregister</param>
    public virtual void UnregisterPatchInCollection(string objectName, ISmolPatch smolPatch)
    {
        if (smolPatch is PatchType patch)
        {
            if (Patches.ContainsKey(objectName))
            {
                if (Patches[objectName].Remove(patch))
                {
                    if (patchedObjects[objectName] == 1)
                        patchedObjects.Remove(objectName);
                    else
                        patchedObjects[objectName]--;
                }
            }
        }
        else if (smolPatch is PatchSetType patchSet)
        {
            UnregisterPatchCollection(patchSet.Patches);
        }
        else
            CuteRandoCore.Log.LogWarning($"Unregister patch called with invalid patch {smolPatch.Name}");
    }
}

/// <summary>
/// Marks a patch as such along with some expected methods that don't require generic typing
/// </summary>
public interface ISmolPatch
{
    /// <summary>
    /// The name of the object the patch is for
    /// </summary>
    public string Name { get; protected set; }

    /// <summary>
    /// An array of names for objects the patch is for
    /// </summary>
    public string[] NameArray { get; protected set; }

    /// <summary>
    /// Creates a duplicate of the patch
    /// </summary>
    /// <returns>The duplicate</returns>
    public abstract object Clone();
}

/// <summary>
/// Marks a patch set as such, includes the interface to additionally mark it as a patch
/// </summary>
public interface ISmolPatchSet : ISmolPatch;
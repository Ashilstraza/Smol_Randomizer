using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;

namespace Smol_Randomizer.Patchers;

/// <summary>Base class of patches that want to patch <see cref="GameObject"/>s</summary>
/// <typeparam name="PatchTarget">The patch target</typeparam>
/// <typeparam name="ObjectType"> The type of <see cref="GameObject"/> the patch is applying to</typeparam>
/// <param name="objectName">     Name of the <see cref="GameObject"/> the patch is for</param>
/// <param name="patch">          The delegate to patch the <see cref="GameObject"/></param>
/// <param name="unpatch">        Optional delegate to unpatch the <see cref="GameObject"/></param>
public abstract class ObjectPatch_Base<PatchTarget, ObjectType>(string objectName, Action<PatchTarget, object[]> patch, Action<PatchTarget, object[]>? unpatch = null)
    : ISmolPatch
{
    public string Name { get; protected internal set; } = objectName;
    public string[] NameArray { get; protected internal set; } = [];

    /// <summary>The delegate to patch the object</summary>
    public Action<PatchTarget, object[]> Patch { get; } = patch;

    /// <summary>Optional delegate to unpatch the object</summary>
    public Action<PatchTarget, object[]>? Unpatch { get; } = unpatch;

    /// <summary>Patches an object</summary>
    /// <param name="patchTarget">The object to patch</param>
    /// <param name="param">      An <see cref="Array" /> of extra arguments the patcher may want</param>
    public abstract void ApplyPatch(ObjectType patchTarget, object[] param);

    /// <summary>Patches an object</summary>
    /// <param name="patchTargets">The objects to patch</param>
    /// <param name="param">       An <see cref="Array" /> of extra arguments the patcher may want</param>
    public abstract void ApplyPatch(ObjectType[] patchTargets, object[] param);

    public abstract object Clone();

    /// <summary>Unpatches an object</summary>
    /// <param name="patchTarget">The object to patch</param>
    /// <param name="param">      An <see cref="Array" /> of extra arguments the patcher may want</param>
    public abstract void RemovePatch(ObjectType patchTarget, object[] param);

    /// <summary>Unpatches an object</summary>
    /// <param name="patchTargets">The object to patch</param>
    /// <param name="param">       An <see cref="Array" /> of extra arguments the patcher may want</param>
    public abstract void RemovePatch(ObjectType[] patchTargets, object[] param);
}

/// <summary>Base class of patch sets for patching objects</summary>
/// <typeparam name="PatchType">The patch type</typeparam>
/// <typeparam name="ObjectType">The object type the patch is applying to</typeparam>
/// <typeparam name="PatchTarget">The patch target</typeparam>
/// <param name="name">   Name of the object the patch is for</param>
/// <param name="patches">List of patches for the object</param>
public abstract class ObjectPatchSet_Base<PatchType, ObjectType, PatchTarget>(string name, List<PatchType> patches)
    : ISmolPatchSet
    where PatchType : ObjectPatch_Base<PatchTarget, ObjectType>
{
    public string Name { get; protected internal set; } = name;
    public string[] NameArray { get; protected internal set; } = [];

    /// <summary>The list of patches for the object</summary>
    public List<PatchType> Patches => patches;

    /// <summary>Applies the patches to the given target when called</summary>
    /// <param name="patchTarget">The target to apply patches to</param>
    /// <param name="param">      An <see cref="Array" /> of extra arguments that patches may want</param>
    public virtual void ApplyPatches(ObjectType patchTarget, object[] param)
    {
        foreach (var patch in Patches)
        {
            patch.ApplyPatch(patchTarget, param);
        }
    }

    /// <summary>Applies the patches to the given targets when called</summary>
    /// <param name="patchTargets">The target to apply patches to</param>
    /// <param name="param">       An <see cref="Array" /> of extra arguments that patches may want</param>
    public virtual void ApplyPatches(ObjectType[] patchTargets, object[] param)
    {
        foreach (var patch in Patches)
        {
            patch.ApplyPatch(patchTargets, param);
        }
    }

    /// <summary>Removes the patches from the given target when called</summary>
    /// <param name="patchTarget">The target to remove patches from</param>
    /// <param name="param">      An <see cref="Array" /> of extra arguments that unpatchers may want</param>
    public virtual void RemovePatches(ObjectType patchTarget, object[] param)
    {
        foreach (var patch in Patches)
        {
            patch.RemovePatch(patchTarget, param);
        }
    }

    /// <summary>Removes the patches from the given targets when called</summary>
    /// <param name="patchTargets">The targets to remove patches from</param>
    /// <param name="param">       An <see cref="Array" /> of extra arguments that unpatchers may want</param>
    public virtual void RemovePatches(ObjectType[] patchTargets, object[] param)
    {
        foreach (var patch in Patches)
        {
            patch.RemovePatch(patchTargets, param);
        }
    }

    /// <summary>Adds a patch to the pre-existing list of patches.</summary>
    /// <param name="patch">The patch to be added</param>
    public virtual void RegisterPatch(PatchType patch) => Patches.Add(patch);

    /// <summary>Adds a set of patchs to the pre-existing list of patches.</summary>
    /// <param name="patch">The set of patches to be added</param>
    public virtual void RegisterPatches(PatchType[] patchSet) => Patches.AddRange(patchSet);

    /// <summary>Removes a patch from the pre-existing list of patches.</summary>
    /// <param name="patch">The patch to be removed</param>
    public virtual void UnregisterPatch(PatchType patch) => Patches.Remove(patch);

    /// <summary>Removes a set of patches from the pre-existing list of patches.</summary>
    /// <param name="patchSet">The set of patches to be removed</param>
    public virtual void UnregisterPatches(PatchType[] patchSet) => Patches.RemoveAll(patchSet.Contains);

    /// <summary>Populates the various child fields if they are empty</summary>
    internal abstract void PopulateChildFields();

    public abstract object Clone();
}

/// <summary>Abstract class for patch collections</summary>
/// <typeparam name="PatchType">   The type of patch</typeparam>
/// <typeparam name="PatchSetType">The type of patch set</typeparam>
/// <typeparam name="ObjectType">  The object type to patch</typeparam>
/// <typeparam name="PatchTarget"> The type of object we are targeting</typeparam>
public abstract class ObjectPatchCollection_Base<PatchType, PatchSetType, ObjectType, PatchTarget>
    where PatchType : ObjectPatch_Base<PatchTarget, ObjectType>, ISmolPatch
    where PatchSetType : ObjectPatchSet_Base<PatchType, ObjectType, PatchTarget>, ISmolPatchSet
{
    private readonly Dictionary<string, int> patchedObjects = [];

    /// <summary>Dictionary containing the names of objects to patch and how many patches are to be applied to them</summary>
    public Dictionary<string, int> PatchedObjects => patchedObjects;

    /// <summary>Dictionary containing a list of objects and the patches to apply to them</summary>
    public Dictionary<string, HashSet<PatchType>> Patches { get; } = [];

    /// <summary>Applies the patches to the given object</summary>
    /// <param name="objectName">Name of the object to patch</param>
    /// <param name="obj">       The object to patch</param>
    public virtual void ApplyPatches(string objectName, ObjectType obj)
    {
        if (Patches.TryGetValue(objectName, out HashSet<PatchType> patchSet))
        {
            foreach (var patch in patchSet)
            {
                patch.ApplyPatch(obj, [objectName]);
            }
        }
    }

    /// <summary>Removes the patches from the given object</summary>
    /// <param name="objectName">Name of the object to unpatch</param>
    /// <param name="obj">       The object to unpatch</param>
    public virtual void RemovePatches(string objectName, ObjectType obj)
    {
        if (Patches.TryGetValue(objectName, out HashSet<PatchType> patchSet))
        {
            foreach (var patch in patchSet)
            {
                patch.RemovePatch(obj, [objectName]);
            }
        }
    }

    /// <summary>Registers a collection of FSM patches</summary>
    /// <param name="patchCollection">The collection of patches to register</param>
    /// <param name="objectName">      The name of the object the patches are for</param>
    /// <returns>The number of patches registered</returns>
    public virtual int RegisterPatchCollection(List<PatchType> patchCollection, string objectName)
    {
        int patchTotal = 0;

        foreach (var patch in patchCollection)
        {
            string name = patch.Name is "" ?
                                patch.NameArray.Length > 0 ?
                                    ""
                                    : objectName
                                : patch.Name;

            patchTotal += RegisterPatchInCollection(name, patch);
        }

        return patchTotal;
    }

    /// <summary>Registers FSM patches</summary>
    /// <param name="objectName">The name of the object the patches are for</param>
    /// <param name="smolPatch">The patch or patch set to register</param>
    /// <returns>The number of patches registered</returns>
    public virtual int RegisterPatchInCollection(string objectName, ISmolPatch smolPatch)
    {
        int patchTotal = 0;

        if (smolPatch is PatchType patch)
        {
            if (objectName is "")
            {
                if (smolPatch.NameArray.Length > 0)
                {
                    foreach (string name in smolPatch.NameArray)
                        patchTotal += RegisterPatchInCollection(name, smolPatch);

                    return patchTotal;
                }
                else
                    CuteRandoCore.DebugLog($"Register patch called with blank name and empty name array; Type: {smolPatch.GetType()}", DebugLogOptions.Error, true);
            }
            if (Patches.ContainsKey(objectName))
            {
                if (!Patches[objectName].Add(patch))
                    return patchTotal; // The patch is already in there
            }
            else
                Patches[objectName] = new() { { patch } };

            if (PatchedObjects.ContainsKey(objectName))
                PatchedObjects[objectName]++;
            else
                PatchedObjects.Add(objectName, 1);
            patchTotal++;
        }
        else if (smolPatch is PatchSetType patchSet)
        {
            if (objectName == "")
            {
                if (smolPatch.NameArray.Length > 0)
                {
                    foreach (string name in smolPatch.NameArray)
                        patchTotal += RegisterPatchCollection(patchSet.Patches, name);

                    return patchTotal;
                }
                else
                    CuteRandoCore.DebugLog($"Register patch called with blank name and empty name array; Type: {smolPatch.GetType()}", DebugLogOptions.Error, true);
            }
            else
                patchTotal += RegisterPatchCollection(patchSet.Patches, objectName);
        }
        else
            CuteRandoCore.DebugLog($"Register patch called with invalid patch type, Name: {smolPatch.Name}, Type: {smolPatch.GetType()}", DebugLogOptions.Error, true);

        return patchTotal;
    }

    /// <summary> Unregister a collection of patches </summary>
    /// <param name="patchCollection">A list containing the patches to no longer be applied<</param>
    /// <returns>The number of patches unregistered</returns>
    public virtual int UnregisterPatchCollection(List<PatchType> patchCollection, string objectName)
    {
        int unpatchTotal = 0;

        foreach (var patch in patchCollection)
        {
            string name = patch.Name is "" ?
                                patch.NameArray.Length > 0 ?
                                    ""
                                    : objectName
                                : patch.Name;

            unpatchTotal += UnregisterPatchInCollection(name, patch);
        }

        return unpatchTotal;
    }

    /// <summary>Unregisters the patch for the object</summary>
    /// <param name="objectName">The object's name that is to no longer be patched</param>
    /// <param name="smolPatch"> The patch to unregister</param>
    /// <returns>The number of patches unregistered</returns>
    public virtual int UnregisterPatchInCollection(string objectName, ISmolPatch smolPatch)
    {
        int unpatchTotal = 0;

        if (smolPatch is PatchType patch)
        {
            if (objectName == "")
            {
                if (smolPatch.NameArray.Length > 0)
                {
                    foreach (string name in smolPatch.NameArray)
                        unpatchTotal += UnregisterPatchInCollection(name, smolPatch);
                    return unpatchTotal;
                }
                else
                    CuteRandoCore.DebugLog($"Unregister patch called with blank name and empty name array; Type: {smolPatch.GetType()}", DebugLogOptions.Error, true);
            }
            if (Patches.ContainsKey(objectName))
            {
                if (Patches[objectName].Remove(patch))
                {
                    if (PatchedObjects[objectName] == 1)
                        PatchedObjects.Remove(objectName);
                    else
                        PatchedObjects[objectName]--;
                }
                unpatchTotal++;
            }
        }
        else if (smolPatch is PatchSetType patchSet)
        {
            if (objectName == "")
            {
                if (patchSet.NameArray.Length > 0)
                {
                    foreach (string name in patchSet.NameArray)
                        unpatchTotal += UnregisterPatchCollection(patchSet.Patches, name);

                    return unpatchTotal;
                }
                else
                    CuteRandoCore.DebugLog($"Unregister patch called with blank name and empty name array; Type: {smolPatch.GetType()}", DebugLogOptions.Error, true);
            }
            else
                unpatchTotal += UnregisterPatchCollection(patchSet.Patches, objectName);
        }
        else
            CuteRandoCore.DebugLog($"Unregister patch called with invalid patch type, Name: {smolPatch.Name}, Type: {smolPatch.GetType()}", DebugLogOptions.Error, true);

        return unpatchTotal;
    }
}

/// <summary>Marks a patch as such along with some expected methods that don't require generic typing</summary>
public interface ISmolPatch : ICloneable
{
    /// <summary>The name of the object the patch is for</summary>
    public string Name { get; }

    /// <summary>An <see cref="Array" /> of names for objects the patch is for</summary>
    public string[] NameArray { get; }
}

/// <summary>Marks a patch set as such, includes the interface to additionally mark it as a patch</summary>
public interface ISmolPatchSet : ISmolPatch;
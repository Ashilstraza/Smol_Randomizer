using System;
using System.Reflection;

using HarmonyLib;

using HutongGames.PlayMaker;

using UnityEngine;

namespace Smol_Randomizer.Patchers;

public static class SmolExtensions
{
    extension(float f)
    {
        /// <summary>Gets the absolute value of the given <see langword="float" /></summary>
        /// <returns>Returns the absolute value of the <see langword="float" /></returns>
        public float Abs() => f > 0f ? f : f * -1f;
    }

    extension(Transform transform)
    {
        /// <summary>Gets the absolute x value of the transform scale</summary>
        /// <returns>Returns the absolute value of localScale.x</returns>
        public float GetAbsScaleX()
            => transform.localScale.x > 0f ? transform.localScale.x : transform.localScale.x * -1;

        /// <summary>Gets the absolute y value of the transform scale</summary>
        /// <returns>Returns the absolute value of localScale.y</returns>
        public float GetAbsScaleY()
            => transform.localScale.y > 0f ? transform.localScale.y : transform.localScale.y * -1;

        /// <summary>Gets the absolute z value of the transform scale</summary>
        /// <returns>Returns the absolute value of localScale.z</returns>
        public float GetAbsScaleZ()
            => transform.localScale.z > 0f ? transform.localScale.z : transform.localScale.z * -1;

        /// <summary>Gets the absolute value of the transform scale</summary>
        /// <returns>Returns the absolute value of localScale</returns>
        public Vector3 GetAbsScale()
            => transform.localScale.Abs();

        /// <summary>Gets the absolute x value of the transform scale from a global standpoint</summary>
        /// <returns>Returns the absolute value of lossyScale.x</returns>
        public float GetAbsLossyScaleX()
            => transform.lossyScale.x > 0f ? transform.lossyScale.x : transform.lossyScale.x * -1;

        /// <summary>Gets the absolute y value of the transform scale from a global standpoint</summary>
        /// <returns>Returns the absolute value of lossyScale.y</returns>
        public float GetAbsLossyScaleY()
            => transform.lossyScale.y > 0f ? transform.lossyScale.y : transform.lossyScale.y * -1;

        /// <summary>Gets the absolute z value of the transform scale from a global standpoint</summary>
        /// <returns>Returns the absolute value of lossyScale.z</returns>
        public float GetAbsLossyScaleZ()
            => transform.lossyScale.z > 0f ? transform.lossyScale.z : transform.lossyScale.z * -1;

        /// <summary>Gets the absolute value of the transform scale from a global standpoint</summary>
        /// <returns>Returns the absolute value of lossyScale</returns>
        public Vector3 GetAbsLossyScale()
            => transform.lossyScale.Abs();

        /// <summary>Gets an <see cref="Array" /> of children of the <see cref="Transform" /></summary>
        /// <returns>Returns the <see cref="Array" /> of children</returns>
        public Transform[] GetChildren()
        {
            Transform[] children = new Transform[transform.childCount];

            for (int i = 0; i < transform.childCount; i++)
                children[i] = transform.GetChild(i);

            return children;
        }
    }

    extension(FsmVariables fsmVariables)
    {
        /// <summary>Adds a <see cref="NamedVariable" /> to the fsm's internal lookup array</summary>
        /// <param name="v">The variable to add</param>
        public void AddVariableLookup(NamedVariable v)
        {
            MethodInfo addVariableLookup = AccessTools.Method(typeof(FsmVariables), "AddVariableLookup");

            addVariableLookup.Invoke(fsmVariables, [v]);
        }

        /// <summary>Adds an array of <see cref="NamedVariable" /> s to the fsm's internal lookup array</summary>
        /// <param name="v">The variable to add</param>
        public void AddVariableLookup(NamedVariable[] vArray)
        {
            MethodInfo addVariableLookup = AccessTools.Method(typeof(FsmVariables), "AddVariableLookup");

            foreach (NamedVariable v in vArray)
                addVariableLookup.Invoke(fsmVariables, [v]);
        }
    }
}
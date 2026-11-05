using HutongGames.PlayMaker;

using System;

#if TESTING

using DebugDrawing;

#endif

using UnityEngine;

namespace Smol_Randomizer.Patchers.FSM_Actions;

/// <summary><see cref="FsmStateAction" /> to shift a variable</summary>
public class ShiftVar : FsmStateAction
{
    /// <summary><see cref="GameObject" /> we are referencing</summary>
    public FsmOwnerDefault? gameObject = null;

    /// <summary>Name of the variable we want to shift</summary>
    public FsmString? variableName = null;

    /// <summary>Name of the variable that references the target angle</summary>
    public FsmString? angleName = null;

    /// <summary>Shift by the full height</summary>
    public bool fullHeight = false;

    /// <summary>Magic number to scale by instead, bypasses need for collider sizes</summary>
    public float magicNumber = 0f;

    /// <summary>Alternate size to use if there is no <see cref="Collider" /></summary>
    public Vector2 backupSize = Vector2.zero;

    /// <summary>Alternate offset to use if there is no <see cref="Collider" /></summary>
    public Vector2 backupOffset = Vector2.zero;

    /// <summary>If the Action should be able to be called more than once</summary>
    public bool callOnce = false;

    /// <summary>Which values should be shifted when it is a Vector2 or Vector3, arranged in an array as [x,y,z]</summary>
    public bool[] shiftValues = [true, true, true];

    /// <summary>The variable being changed</summary>
    private NamedVariable? targetVar = null;

    /// <summary>The variable for the angle</summary>
    private FsmFloat? targetAngle = null;

    /// <summary>The angle for the target adjustment</summary>
    private float angle = 0f;

    /// <summary>Collider of referenced GameObject</summary>
    private BoxCollider2D? col;

    private bool called = false;

    /// <summary>Reset this to the default values</summary>
    public override void Reset()
    {
        gameObject = null;
        variableName = null;
        angleName = null;
        fullHeight = false;
        magicNumber = 0;
        callOnce = false;
        backupOffset = Vector2.zero;
        backupSize = Vector2.zero;
        shiftValues = [true, true, true];

        targetVar = null;
        targetAngle = null;
        angle = 0f;
        col = null;
        called = false;
    }

    /// <summary>Call this when an area is entered</summary>
    public override void OnEnter()
    {
        Adjust();
        Finish();
    }

    /// <summary>Call this when an update happens</summary>
    public override void OnUpdate()
    {
        Adjust();
    }

    /// <summary>Adjusts the variable by half the height of the <see cref="GameObject" /></summary>
    private void Adjust()
    {
        if (callOnce && called)
        {
            CuteRandoCore.TestingLog($"ShiftVar called multiple times for {base.Fsm.GetOwnerDefaultTarget(gameObject).name}, " +
                $"State: {this.Fsm.ActiveStateName}, " +
                $"Current Frame Count: {Time.frameCount}, " +
                $"Hash Code:{this.GetHashCode()},  " +
                $"Scene: {base.Fsm.GetOwnerDefaultTarget(gameObject).scene.name}",
                DebugLogOptions.Info);
            return;
        }

        GameObject thing = base.Fsm.GetOwnerDefaultTarget(gameObject);

        if (variableName == null || thing == null)
        {
            CuteRandoCore.DebugLog($"Variable {variableName} and/or thing {thing} are null. Unable to run ShiftVar Action.",
                DebugLogOptions.Error,
                true);
            return;
        }

        if (col == null)
            col = thing.GetComponent<BoxCollider2D>();

        bool skipColCheck = false;

        if (magicNumber != 0)
            skipColCheck = true;

        if (!skipColCheck && col == null && (backupSize == Vector2.zero && backupOffset == Vector2.zero))
        {
            CuteRandoCore.DebugLog($"Tried to adjust variable on thing without collider. " +
                $"Please check {thing?.name} in {thing?.scene.name}.",
                DebugLogOptions.Error,
                true);
            return;
        }

        CuteRandoCore.TestingLog($"ShiftVar on {thing.name}, " +
        $"State: {this.Fsm.ActiveStateName}, " +
        $"Frame Count: {Time.frameCount}, " +
        $"Hash Code:{this.GetHashCode()}" +
        $"Scene: {base.Fsm.GetOwnerDefaultTarget(gameObject).scene.name}",
        DebugLogOptions.Info);

        Vector2 size = col?.size ?? backupSize;
        Vector2 offset = col?.offset ?? backupOffset;

        if (targetVar == null)
            targetVar = ActionHelpers.GetGameObjectFsm(thing, "").FsmVariables.GetVariable(variableName.Value);

        if (angleName != null && targetAngle == null)
        {
            targetAngle = ActionHelpers.GetGameObjectFsm(thing, "").FsmVariables.GetFsmFloat(angleName.Value);
            angle = targetAngle.Value;
        }
        else if (angleName == null)
            angle = 0f;
        else if (targetAngle != null)
            angle = targetAngle.Value * Mathf.Deg2Rad;

        var absScale = thing.transform.GetAbsScale();

        switch (targetVar.VariableType)
        {
            case (VariableType.Float):
                if (magicNumber != 0)
                    targetVar.RawValue = ShiftPosToBase.ShiftN((float)targetVar.RawValue, absScale.y, magicNumber, Mathf.Cos(angle));
                else
                    targetVar.RawValue = (float)targetVar.RawValue + (offset.y * absScale.y - size.y / (fullHeight ? 1 : 2));
                break;

            case (VariableType.Vector2):
                Vector2 oldV2Value = (Vector2)targetVar.RawValue;
#if TESTING
                Primitives.Circle(oldV2Value, 0.06f, color: Color.gray, duration: 10f);
#endif
                if (magicNumber != 0)
                {
                    var x = shiftValues[0] ? ShiftPosToBase.ShiftN(oldV2Value.x, absScale.x, magicNumber, Mathf.Sin(angle)) : oldV2Value.x;
                    var y = shiftValues[1] ? ShiftPosToBase.ShiftN(oldV2Value.y, absScale.y, magicNumber, Mathf.Cos(angle)) : oldV2Value.y;
                    targetVar.RawValue = new Vector2(x, y);
                }
                else
                    targetVar.RawValue = oldV2Value + ((size / (fullHeight ? 1 : 2)) * new Vector2(Mathf.Sin(angle), -Mathf.Cos(angle)));
#if TESTING
                Primitives.Circle((Vector2)targetVar.RawValue, 0.04f, color: Color.cyan, duration: 10f);
#endif
                break;

            case (VariableType.Vector3):
                Vector3 oldv3Value = (Vector3)targetVar.RawValue;
#if TESTING
                Primitives.Circle(oldv3Value, 0.06f, color: Color.gray, duration: 10f);
#endif
                if (magicNumber != 0)
                {
                    var x = shiftValues[0] ? ShiftPosToBase.ShiftN(oldv3Value.x, absScale.x, magicNumber, Mathf.Sin(angle)) : oldv3Value.x;
                    var y = shiftValues[1] ? ShiftPosToBase.ShiftN(oldv3Value.y, absScale.y, magicNumber, Mathf.Cos(angle)) : oldv3Value.y;
                    var z = shiftValues[2] ? ShiftPosToBase.ShiftN(oldv3Value.z, absScale.z, magicNumber, 1f) : oldv3Value.z;
                    targetVar.RawValue = new Vector3(x, y, z);
                }
                else
                    targetVar.RawValue = oldv3Value + (((Vector3)size / (fullHeight ? 1 : 2)).MultiplyElements(new Vector3(Mathf.Sin(angle), -Mathf.Cos(angle), 1f)));
#if TESTING
                Primitives.Circle((Vector3)targetVar.RawValue, 0.04f, color: Color.cyan, duration: 10f);
#endif
                break;

            default:
                throw new NotImplementedException();
        }
        if (callOnce) called = true;
    }
}
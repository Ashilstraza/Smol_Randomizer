using DebugDrawing;

using HutongGames.PlayMaker;

using System;

using UnityEngine;

namespace Smol_Randomizer.Patchers.FSM_Actions;

/// <summary><see cref="FsmStateAction" /> to shift a variable down by half an object's height</summary>
public class ShiftVarDownByHalfHeight : FsmStateAction
{
    /// <summary><see cref="GameObject" /> we are referencing</summary>
    public FsmOwnerDefault? gameObject;

    /// <summary>Name of the variable we want to shift</summary>
    public FsmString? variableName;

    /// <summary>Name of the variable that references the target angle</summary>
    public FsmString? angleName;

    /// <summary>The variable being changed</summary>
    private NamedVariable? targetVar;

    /// <summary>The variable for the angle</summary>
    private FsmFloat? targetAngle;

    /// <summary>The angle for the target adjustment</summary>
    private float angle;

    /// <summary>Collider of referenced GameObject</summary>
    private BoxCollider2D? col;

    /// <summary>Reset this to the default values</summary>
    public override void Reset()
    {
        gameObject = null;
        variableName = null;
        angleName = null;

        targetVar = null;
        targetAngle = null;
        angle = 0f;
        col = null;
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
        GameObject thing = base.Fsm.GetOwnerDefaultTarget(gameObject);

        if (thing != null && col == null)
            col = thing.GetComponent<BoxCollider2D>();

        if (thing == null || col == null || variableName == null) return;

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

        switch (targetVar.VariableType)
        {
            case (VariableType.Float):
                targetVar.RawValue = (float)targetVar.RawValue - col.size.y / 2;
                break;

            case (VariableType.Vector2):
                DebugDrawing.Primitives.Circle((Vector2)targetVar.RawValue, 0.06f, color: Color.gray, duration: 10f);
                targetVar.RawValue = (Vector2)targetVar.RawValue + (((Vector2)col.size / 2) * new Vector2((float)Math.Sin(angle), -(float)Math.Cos(angle)));
                DebugDrawing.Primitives.Circle((Vector2)targetVar.RawValue, 0.04f, color: Color.cyan, duration: 10f);
                break;

            case (VariableType.Vector3):
                DebugDrawing.Primitives.Circle((Vector3)targetVar.RawValue, 0.06f, color: Color.gray, duration: 10f);
                targetVar.RawValue = (Vector3)targetVar.RawValue + (((Vector3)col.size / 2).MultiplyElements(new Vector3((float)Math.Sin(angle), -(float)Math.Cos(angle), 1f)));
                DebugDrawing.Primitives.Circle((Vector3)targetVar.RawValue, 0.04f, color: Color.cyan, duration: 10f);
                break;

            default:
                throw new NotImplementedException();
        }
    }
}
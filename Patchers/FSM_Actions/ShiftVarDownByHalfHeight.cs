using HutongGames.PlayMaker;

using UnityEngine;

namespace Smol_Randomizer.Patchers.FSM_Actions;

/// <summary><see cref="FsmStateAction" /> to shift a variable down by half an object's height</summary>
public class ShiftVarDownByHalfHeight : FsmStateAction
{
    /// <summary><see cref="GameObject" /> we are referencing</summary>
    public FsmOwnerDefault? gameObject;

    /// <summary>Name of the variable we want to shift</summary>
    public FsmString? variableName;

    /// <summary>The float being changed</summary>
    private FsmFloat? floatTarget;

    /// <summary>Collider of referenced GameObject</summary>
    private BoxCollider2D? col;

    /// <summary>Reset this to the default values</summary>
    public override void Reset()
    {
        gameObject = null;
        variableName = null;

        floatTarget = null;
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

        if (floatTarget == null)
            floatTarget = ActionHelpers.GetGameObjectFsm(thing, "").FsmVariables.GetFsmFloat(variableName.Value);

        floatTarget.Value = floatTarget.Value - col.size.y / 2;
    }
}
using HutongGames.PlayMaker;

using UnityEngine;

namespace Smol_Randomizer.Patchers.FSM_Actions;

/// <summary>FSM Action to shift a variable down by half an object's height</summary>
public class ShiftVarDownByHalfHeight : FsmStateAction
{
    /// <summary>Object we are referencing</summary>
    public FsmOwnerDefault? gameObject;

    /// <summary>Name of the variable we want to shift</summary>
    public FsmString? variableName;

    /// <summary>If this should happen every frame</summary>
    public bool everyFrame = false;

    /// <summary>The float being changed</summary>
    private FsmFloat? floatTarget;

    /// <summary>Collider of referenced GameObject</summary>
    private BoxCollider2D? col;

    /// <summary>Reset Action to base values</summary>
    public override void Reset()
    {
        gameObject = null;
        variableName = null;
        everyFrame = false;

        floatTarget = null;
        col = null;
    }

    public override void OnEnter()
    {
        Adjust();

        if (!everyFrame)
            Finish();
    }

    public override void OnUpdate()
    {
        Adjust();
    }

    /// <summary>Adjusts the variable by half the height of the GameObject</summary>
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
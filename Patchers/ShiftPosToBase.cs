using System;
using System.Collections.Generic;
using System.Text;

using HutongGames.PlayMaker;

using UnityEngine;

namespace Smol_Randomizer.Patchers;

/// <summary>
/// Shifts a GameObject to be located at its unscaled base (where it would be if standing on the floor). This does take into account rotation.
/// </summary>
public class ShiftPosToBase : FsmStateAction
{
    /// <summary>
    /// Object we are adjusting
    /// </summary>
    public FsmOwnerDefault? gameObject;
    /// <summary>
    /// If this should happen every frame
    /// </summary>
    public bool everyFrame = false;

    /// <summary>
    /// Collider of referenced GameObject
    /// </summary>
    private BoxCollider2D? col;

    private Transform? transform;

    public override void Reset()
    {
        gameObject = null;
        everyFrame = false;
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

    /// <summary>
    /// Adjusts the object down to its base
    /// </summary>
    private void Adjust()
    {
        GameObject thing = base.Fsm.GetOwnerDefaultTarget(gameObject);

        if (thing != null && col == null)
            col = thing.GetComponent<BoxCollider2D>();
        if (thing != null && transform == null)
            transform = thing.transform;

        if (thing == null || col == null || transform == null) return;
                
        Vector3 pos = transform.position;
        float height = col.size.y;
        float scale = transform.GetScaleY();
        float rotation = transform.rotation.eulerAngles.z;

        pos.x = (float)(pos.x + (((height / scale) - height) / 2) * Math.Cos(rotation));
        pos.y = (float)(pos.y + (((height / scale) - height) / 2) * Math.Sin(rotation));
        transform.position = pos;
    }
}

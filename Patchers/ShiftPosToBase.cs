using System;

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
    /// If the height should be divided in half
    /// </summary>
    public bool halfHeight = true;

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
        double rotation = Math.PI * transform.rotation.eulerAngles.z / 180;

        pos.x = ShiftX(height, scale, pos.x, rotation, halfHeight);
        pos.y = ShiftY(height, scale, pos.y, rotation, halfHeight);

        transform.position = pos;
    }

    /// <summary>
    /// Returns a float for shifting a collider's Y position
    /// </summary>
    /// <param name="height">Height of the object</param>
    /// <param name="scale">Scale of the object</param>
    /// <param name="y">The starting y position</param>
    /// <param name="rotation">The rotation in Radians</param>
    /// <param name="half">Optional: cut height in half before subtracting</param>
    /// <returns>The shifted y position</returns>
    public static float ShiftY(float height, float scale, float y, double rotation, bool half = true)
    {
        return (float)(y - (((height - (height * scale)) / (half ? 2 : 1)) * Math.Cos(rotation)));
    }

    /// <summary>
    /// Returns a float for shifting a collider's Y position
    /// </summary>
    /// <param name="height">Height of the object</param>
    /// <param name="scale">Scale of the object</param>
    /// <param name="y">The starting x position</param>
    /// <param name="rotation">The rotation in Radians</param>
    /// <param name="half">Optional: cut height in half before subtracting</param>
    /// <returns>The shifted x position</returns>
    public static float ShiftX(float height, float scale, float x, double rotation, bool half = true)
    {
        return (float)(x - ((((height * scale) - height) / (half ? 2 : 1)) * Math.Sin(rotation)));
    }
}

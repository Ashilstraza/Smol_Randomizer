using System;

using HutongGames.PlayMaker;

using UnityEngine;

namespace Smol_Randomizer.Patchers.FSM_Actions;

/// <summary>
/// Shifts a GameObject to be located at its unscaled base (where it would be if standing on the floor). This does take
/// into account rotation.
/// </summary>
public class ShiftPosToBase : FsmStateAction
{
    /// <summary>Object we are adjusting</summary>
    public FsmOwnerDefault? gameObject;

    /// <summary>Changes the direction the adjustment will take place</summary>
    public float rotateAdjust = 0;

    /// <summary>Alternate size to use if there is no collider</summary>
    public Vector2 backupSize = Vector2.zero;

    /// <summary>Original size of the object</summary>
    public Vector2 originalScale = Vector2.zero;

    /// <summary>If the X value should be reversed</summary>
    public bool reverseX = false;

    /// <summary>If the Y value should be reversed</summary>
    public bool reverseY = false;

    /// <summary>If the enemy should be temporarily set to invincible to band aid some issues</summary>
    public bool tempInvincible = false;

    /// <summary>If the enemy was invincible to start with</summary>
    public bool wasInvincible = false;

    /// <summary>
    /// If the object should be shifted by a specific number instead of being calculated from the size and offset
    /// </summary>
    public float magicNumber = float.MinValue;

    /// <summary>Reverses the direction, useful for debugging.</summary>
    public bool reverse = false;

    /// <summary>Collider of referenced GameObject</summary>
    private BoxCollider2D? col;

    /// <summary>Transform of referenced GameObject</summary>
    private Transform? transform;

    private float lastFrameCount;

    public override void Reset()
    {
        gameObject = null;
        reverse = false;
        rotateAdjust = 0;
        backupSize = Vector2.zero;
        originalScale = Vector2.zero;
        reverseX = false;
        reverseY = false;
        tempInvincible = false;
        wasInvincible = false;
        magicNumber = float.MinValue;
        col = null;
        transform = null;
    }

    public override void OnEnter()
    {
        Adjust();
        Finish();
    }

    public override void OnUpdate()
    {
        Adjust();
    }

    /// <summary>Adjusts the object down to its base</summary>
    private void Adjust()
    {
        if (Time.frameCount == lastFrameCount) return; // prevent multiple calls in one frame
        lastFrameCount = Time.frameCount;

        GameObject thing = base.Fsm.GetOwnerDefaultTarget(gameObject);

        if (thing != null && col == null)
            col = thing.GetComponent<BoxCollider2D>();
        if (thing != null && transform == null)
            transform = thing.transform;

        if (thing == null || transform == null) return;
        if (col == null && backupSize == Vector2.zero)
        {
#if DEBUG
            CuteRandoCore.Log.LogWarning($"Tried to adjust enemy without collider. " +
                $"Please check {thing.name} in {thing.scene.name}.");
#endif
            return;
        }

        if (magicNumber == 0)
            magicNumber = float.MinValue;

        Shift(thing,
            originalScale,
            rotateAdjust,
            backupSize,
            magicNumber,
            reverse ? !reverseX : reverseX,
            reverse ? !reverseY : reverseY,
            col,
            transform);

        if (tempInvincible && !wasInvincible)
            thing.GetComponent<HealthManager>()?.IsInvincible = wasInvincible;
    }

    /// <summary>Shifts the given coordinate value</summary>
    /// <param name="n">       The coordinate value to shift</param>
    /// <param name="scale">   The scale of the object</param>
    /// <param name="halfSize">The size of the object</param>
    /// <param name="offset">  The offset of the object</param>
    /// <param name="rotation">The rotation of the object</param>
    /// <param name="reverse"> Optional: If the value should be reversed in direction</param>
    /// <returns>The adjusted coordinate value</returns>
    public static float ShiftN(float n,
                                float scale,
                                float halfSize,
                                float offset,
                                double rotation,
                                bool reverse = false)
    {
        float scaledBase = n + offset * scale - halfSize * scale;
        float enemyBase = n + offset - halfSize;
        return (float)(n - ((enemyBase - scaledBase)) * rotation * (reverse ? 1 : -1));
    }

    /// <summary>Shifts the given coordinate by a specific value that is then scaled, rotated, and possibly reversed</summary>
    /// <param name="n">          The coordinate value to shift</param>
    /// <param name="scale">      The scale of the object</param>
    /// <param name="magicNumber">The special number to shift the coordinate value by</param>
    /// <param name="rotation">   The rotation of the object</param>
    /// <param name="reverse">    Optional: If the value should be reversed in direction</param>
    /// <returns>The adjusted coordinate value</returns>
    public static float ShiftN(float n,
                                float scale,
                                float magicNumber,
                                double rotation,
                                bool reverse = false)
    {
        return (float)(n + (((1 - scale) * 100 * magicNumber) * rotation) * (reverse ? -1 : 1));
    }

    /// <summary>Shifts a GameObject toward its base</summary>
    /// <param name="thing">        The GameObject to shift</param>
    /// <param name="originalScale">The original scale of the GameObject before it was scaled</param>
    /// <param name="rotateAdjust"> Optional: Changes the direction the adjustment will take place</param>
    /// <param name="backupSize">   Optional: Alternate size to use if there is no collider</param>
    /// <param name="magicNumber">  Optional: Alternate value used to shift the object</param>
    /// <param name="reverseX">     Optional: Reverses the direction to shift the X value</param>
    /// <param name="reverseY">     Optional: Reverses the direction to shift the Y value</param>
    /// <param name="collider">     Optional: The Collider of the GameObject to shift</param>
    /// <param name="transform">    Optional: The Transform of the GameObject to shift</param>
    public static void Shift(
        GameObject thing,
        Vector2 originalScale,
        float rotateAdjust = 0,
        Vector2? backupSize = null,
        float magicNumber = float.MinValue,
        bool reverseX = false,
        bool reverseY = false,
        Collider2D? collider = null,
        Transform? transform = null)
    {
        collider ??= thing.GetComponent<Collider2D>();
        transform ??= thing.transform;

        Vector2 altSize = backupSize ?? Vector2.zero;

        if (thing == null || (collider == null && altSize == Vector2.zero) || transform == null) return;

        bool xReverse = reverseX;
        bool yReverse = reverseY;
        Vector3 pos = transform.position;
        Vector2 offset = collider?.offset ?? Vector2.zero;
        float xScale = transform.GetScaleX();
        float yScale = transform.GetScaleY();
        double rotationDeg = Math.Round(transform.rotation.eulerAngles.z);
        double rotation = Math.PI * (rotationDeg + rotateAdjust) / 180;
        double sinRotation = Math.Sin(rotation);
        double cosRotation = Math.Cos(rotation);

        float height = 0;
        float width = 0;

        if (collider is BoxCollider2D boxCollider)
        {
            height = (float)(Math.Abs(cosRotation) * boxCollider.size.y + Math.Abs(sinRotation) * boxCollider.size.x);
            width = (float)(Math.Abs(sinRotation) * boxCollider.size.y + Math.Abs(cosRotation) * boxCollider.size.x);
        }
        else if (collider == null)
        {
            height = (float)(Math.Abs(cosRotation) * altSize.y + Math.Abs(sinRotation) * altSize.x);
            width = (float)(Math.Abs(sinRotation) * altSize.y + Math.Abs(cosRotation) * altSize.x);
        }
        else
            throw new NotImplementedException();

        if (rotationDeg < 315 && rotationDeg > 225 && yScale > 0)
        {
            xReverse = !xReverse;
        }
        else if (rotationDeg < 135 && rotationDeg > 45 && yScale > 0)
        {
            xReverse = !xReverse;
        }
        else if (xScale < 0)
        {
            xReverse = !xReverse;
        }

        float actualHalfHeight = (float)(((height / 2) * (1 / Math.Abs(originalScale.y)) * Math.Abs(cosRotation)) + ((height / 2) * (1 / Math.Abs(originalScale.x)) * Math.Abs(sinRotation)));
        float actualHalfWidth = (float)(((width / 2) * (1 / Math.Abs(originalScale.y)) * Math.Abs(sinRotation)) + ((width / 2) * (1 / Math.Abs(originalScale.x)) * Math.Abs(cosRotation)));
        float actualPosX = ActualN(pos.x, offset.x, width, actualHalfWidth); // correction for rotation needed?
        float actualPosY = ActualN(pos.y, offset.y, height, actualHalfHeight);

        pos.x = magicNumber != float.MinValue ? ShiftN(actualPosX, Math.Abs(xScale), magicNumber, sinRotation, xReverse)
            : ShiftN(actualPosX, Math.Abs(xScale), actualHalfWidth, offset.x, sinRotation, xReverse);
        pos.y = magicNumber != float.MinValue ? ShiftN(actualPosY, Math.Abs(yScale), magicNumber, cosRotation, yReverse)
            : ShiftN(actualPosY, Math.Abs(yScale), actualHalfHeight, offset.y, cosRotation, yReverse);

        transform.position = pos;

        float ActualN(float pos, float offset, float size, float actualSize)
        {
            return pos - ((pos + offset - size / 2) - (pos + offset - actualSize));
        }
    }
}
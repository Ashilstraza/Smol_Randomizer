using System;

using HutongGames.PlayMaker;

using Smol_Randomizer.Randomizers;
using Smol_Randomizer.Settings;

#if TESTING

using DebugDrawing;

#endif

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

    /// <summary>If we should skip trying to shift using raycasting and just calculate</summary>
    public bool skipRaycast = false;

    /// <summary>Cancels the Y velocity</summary>
    public bool cancelYVelocity = true;

    /// <summary>
    /// If the object should be shifted by a specific number instead of being calculated from the size and offset,
    /// ignored unless skipRaycast is used or if no surface found
    /// </summary>
    public float magicNumber = float.MinValue;

    /// <summary>
    /// Reverses the direction, useful for debugging, ignored unless skipRaycast is used or if no surface found
    /// </summary>
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
        skipRaycast = false;
        cancelYVelocity = true;
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
        if (Time.frameCount - 5 <= lastFrameCount) // prevent multiple calls in quick sucession
        {
#if TESTING
            CuteRandoCore.Log.LogWarning($"ShiftPosToBase called in quick sucession for {base.Fsm.GetOwnerDefaultTarget(gameObject).name}, " +
                $"State: {this.Fsm.ActiveStateName}, " +
                $"Current Frame Count: {Time.frameCount}, " +
                $"Last Frame Count: {lastFrameCount}, " +
                $"Hash Code:{this.GetHashCode()}, " +
                $"Scene: {base.Fsm.GetOwnerDefaultTarget(gameObject).scene.name}");
#endif
            return;
        }
        lastFrameCount = Time.frameCount;

        GameObject enemy = base.Fsm.GetOwnerDefaultTarget(gameObject);

        if (enemy != null && col == null)
            col = enemy.GetComponent<BoxCollider2D>();
        if (enemy != null && transform == null)
            transform = enemy.transform;

        if (enemy == null || transform == null) return;
        if (col == null && backupSize == Vector2.zero)
        {
#if DEBUG
            CuteRandoCore.Log.LogError($"Tried to adjust enemy without collider. " +
                $"Please check {enemy.name} in {enemy.scene.name}.");
#endif
            return;
        }
#if TESTING
        CuteRandoCore.Log.LogMessage($"ShiftPosToBase on {enemy.name}, " +
            $"State: {this.Fsm.ActiveStateName}, " +
            $"Frame Count: {lastFrameCount}, " +
            $"Hash Code:{this.GetHashCode()}" +
            $"Scene: {base.Fsm.GetOwnerDefaultTarget(gameObject).scene.name}");
#endif

        if (magicNumber == 0)
            magicNumber = float.MinValue;

        Shift(enemy,
            originalScale,
            rotateAdjust,
            backupSize,
            magicNumber,
            reverse ? !reverseX : reverseX,
            reverse ? !reverseY : reverseY,
            skipRaycast,
            cancelYVelocity,
            col,
            transform);

        if (tempInvincible && !wasInvincible)
            enemy.GetComponent<HealthManager>()?.IsInvincible = wasInvincible;
    }

    /// <summary>Shifts a GameObject toward its base, will first try to use raycasting to find the surface</summary>
    /// <param name="enemy">          The GameObject to shift</param>
    /// <param name="originalScale">  The original scale of the GameObject before it was scaled</param>
    /// <param name="rotateAdjust">   Optional: Changes the direction the adjustment will take place</param>
    /// <param name="backupSize">     Optional: Alternate size to use if there is no collider</param>
    /// <param name="magicNumber">    Optional: Alternate value used to shift the object</param>
    /// <param name="reverseX">       Optional: Reverses the direction to shift the X value</param>
    /// <param name="reverseY">       Optional: Reverses the direction to shift the Y value</param>
    /// <param name="skipRaycast">    Optional: Skips using a raycast to find the ground and instead just calculate</param>
    /// <param name="cancelYVelocity">Optional: Cancel the Y Velocity of the enemy</param>
    /// <param name="collider">       Optional: The Collider of the GameObject to shift</param>
    /// <param name="transform">      Optional: The Transform of the GameObject to shift</param>
    public static void Shift(
        GameObject enemy,
        Vector2 originalScale,
        float rotateAdjust = 0,
        Vector2? backupSize = null,
        float magicNumber = float.MinValue,
        bool reverseX = false,
        bool reverseY = false,
        bool skipRaycast = false,
        bool cancelYVelocity = true,
        Collider2D? collider = null,
        Transform? transform = null)
    {
        collider ??= enemy.GetComponent<Collider2D>();
        transform ??= enemy.transform;
        Rigidbody2D rigidbody = enemy.GetComponent<Rigidbody2D>();

        Vector2 altSize = backupSize ?? Vector2.zero;

        if (enemy == null || (collider == null && altSize == Vector2.zero) || transform == null) return;

        bool xReverse = reverseX;
        bool yReverse = reverseY;
        Vector3 pos = transform.position;
        Vector2 offset = collider?.offset ?? Vector2.zero;
        Vector2 scale = transform.localScale;
        Vector2 size;
        float rotationDeg = transform.rotation.eulerAngles.z;
        float sinRotation = (float)Math.Sin(rotationDeg * Mathf.Deg2Rad);
        float cosRotation = (float)Math.Cos(rotationDeg * Mathf.Deg2Rad);
        float sinRotationAdjusted = (float)Math.Sin((rotateAdjust + rotationDeg) * Mathf.Deg2Rad);
        float cosRotationAdjusted = (float)Math.Cos((rotateAdjust + rotationDeg) * Mathf.Deg2Rad);
        float yVelocity = 0;

        if (collider is BoxCollider2D boxCollider)
            size = boxCollider.size;
        else if (collider == null)
            size = altSize;
        else
            throw new NotImplementedException();

        Vector2 invertedScale = new((1 / Math.Abs(originalScale.x)), (1 / Math.Abs(originalScale.y)));
        Vector2 calculatedOriginalHalfSize = new(((size.x / 2) * invertedScale.x * Math.Abs(cosRotation))
                                                + ((size.y / 2) * invertedScale.y * Math.Abs(sinRotation)),
                                            ((size.y / 2) * invertedScale.y * Math.Abs(cosRotation))
                                                + ((size.x / 2) * invertedScale.x * Math.Abs(sinRotation)));

        Vector2 calculatedOriginalOffset = ScaleAndRotate(offset, originalScale, cosRotation, sinRotation);

        if (rotationDeg < 315 && rotationDeg > 225 && scale.y > 0)
            xReverse = !xReverse;
        else if (rotationDeg < 135 && rotationDeg > 45 && scale.y > 0)
            xReverse = !xReverse;
        else if (scale.x < 0)
            xReverse = !xReverse;

        float debugDuration = 60f;

#if TESTING
        DebugDrawer.Square(pos + (Vector3)calculatedOriginalOffset, calculatedOriginalHalfSize * 2, Color.grey, duration: debugDuration); // Original position
        DebugDrawer.Circle(pos + (Vector3)calculatedOriginalOffset, 0.05f, color: Color.grey, duration: debugDuration); // Original center
#endif

        if (cancelYVelocity)
        {
            yVelocity = rigidbody.linearVelocityY;

            if (yVelocity != 0)
                rigidbody.linearVelocityY = 0;
        }

        if (!skipRaycast)
        {
            Vector2 rayDirection = new(sinRotationAdjusted * -1 * (xReverse ? -1 : 1),
                    cosRotationAdjusted * -1 * (yReverse ? -1 : 1));

            bool hitTerrain = Helper.IsRayHittingNoTriggers(
                pos,
                rayDirection,
                (calculatedOriginalHalfSize.y * 3) * invertedScale.y,
                8448,
                out var closestHit);

            Vector2 rayMove = (calculatedOriginalHalfSize * scale.Abs()) * rayDirection * -1;

#if TESTING
            Vector3 endTestPos = pos + (Vector3)rayDirection * ((calculatedOriginalHalfSize.y * 3) * invertedScale.y);

            DebugDrawer.Circle(pos, 0.06f, color: Color.cyan, duration: debugDuration); // Center of transformation
            DebugDrawer.Arrow(pos, endTestPos, 0.5f, Color.cyan, duration: debugDuration); // Test Raycast

            if (hitTerrain)
            {
                DebugDrawer.Circle(closestHit.centroid, 0.1f, color: Color.green, duration: debugDuration); // Closest hit
                DebugDrawer.Square(closestHit.centroid + rayMove, calculatedOriginalHalfSize * 2 * scale, Color.yellow, duration: debugDuration); // Calcualted new position
            }

            if (!Enemy_Size_Rando.Instance.UseRaycastBasing)
                goto skipApplyRaycast;
#endif

            if (hitTerrain)
            {
                pos = closestHit.centroid + rayMove - calculatedOriginalOffset * scale.Abs();
                if (pos.z != transform.position.z) CuteRandoCore.Log.LogWarning("Z does not match");
                transform.position = pos;
                return;
            }
        }
    skipApplyRaycast:

        pos.x = magicNumber != float.MinValue ? ShiftN(pos.x, Math.Abs(scale.x), magicNumber, sinRotationAdjusted, xReverse)
            : ShiftN(pos.x, Math.Abs(scale.x), calculatedOriginalHalfSize.x, offset.x, sinRotationAdjusted, xReverse);
        pos.y = magicNumber != float.MinValue ? ShiftN(pos.y, Math.Abs(scale.y), magicNumber, cosRotationAdjusted, yReverse)
            : ShiftN(pos.y, Math.Abs(scale.y), calculatedOriginalHalfSize.y, offset.y, cosRotationAdjusted, yReverse);

#if TESTING
        Vector2 rotatedSize = ScaleAndRotate(size, scale, cosRotation, sinRotation);
        Vector2 rotatedOffset = ScaleAndRotate(offset, scale, cosRotation, sinRotation);
        if ((rotationDeg < 315 && rotationDeg > 225 && scale.y < 0) || (rotationDeg < 135 && rotationDeg > 45 && scale.y < 0))
            xReverse = !xReverse;
        DebugDrawer.Square(pos + (Vector3)(rotatedOffset * new Vector2(xReverse ? -1 : 1, yReverse ? -1 : 1)), rotatedSize, SmolRandomizerMenuBuilder.EPurple, duration: debugDuration);
#endif
        if (pos.z != transform.position.z) CuteRandoCore.Log.LogWarning("Z does not match");
        transform.position = pos;

        Vector2 ScaleAndRotate(Vector2 original, Vector2 scale, float cos, float sin)
        {
            return new(original.x * cos * scale.x + original.y * sin * scale.y, original.y * cos * scale.y + original.x * sin * scale.x);
        }
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
}
#if TESTING

using DebugDrawing;

#endif

using System;

using UnityEngine;

using static Smol_Randomizer.Settings.Settings;

namespace Smol_Randomizer.Patchers;

public static class NearNudge
{
    /// <summary>Nudges a <see cref="Transform" /> if it is too close to a given target <see cref="Collider2D" /></summary>
    /// <param name="obj">      The <see cref="Transform" /> of the <see cref="GameObject" /> to check</param>
    /// <param name="objCol">   The <see cref="Collider2D" /> of the <see cref="GameObject" /> to check</param>
    /// <param name="target">   The target <see cref="Collider2D" /> to check against</param>
    /// <param name="margin">   
    /// Optional: <see cref="Vector2" /> for increasing the check distance, defaults to (1.5f, 1.5f)
    /// </param>
    /// <param name="direction">Optional: <see langword="enum" /> for direction to check, defaults to <see cref="CheckDirection.isAbove" /></param>
    /// <returns>
    /// <see langword="true" /> if the <see cref="Transform" /> was nudged, <see langword="false" /> if it was not
    /// </returns>
    /// <param name="objCols">  
    /// <see cref="Array" /> of <see cref="Collider2D" /> s of the <see cref="GameObject" /> to check
    /// </param>
    /// <param name="targets">  <see cref="Array" /> of target <see cref="Collider2D" /> s to check against</param>
    public static bool TryNudge(Transform obj,
                             Collider2D objCol,
                             Collider2D target,
                             Vector2? margin = null,
                             CheckDirection direction = CheckDirection.isAbove)
    {
        if (obj == null || objCol == null || target == null)
            throw new ArgumentNullException("One or more arguments are null, please check them before calling method.");

        Vector2 actualMargin = margin ?? new Vector2(1.5f, 1.5f);
#if TESTING
        Vector3 oldObjPos = obj.position;
        Primitives.Circle(obj.position, 0.2f, color: Color.cyan.SetAlpha(.5f), duration: 60f);
        Primitives.Square(obj, objCol.offset, objCol.bounds.size, Color.green.SetAlpha(.5f), duration: 60f);
#endif
        bool nudged = false;
        int directions = 0;

        var result = target.ClosestPoint(obj.position) - (Vector2)obj.position;

        if (direction.HasFlag(CheckDirection.isAbove))
        {
            if (result.x.Abs() < objCol.offset.x + objCol.bounds.size.x + actualMargin.x)
            {
                var yClose = objCol.offset.y - objCol.bounds.size.y - actualMargin.y;
                if (yClose - result.y >= yClose && result.y >= yClose)
                {
                    obj.SetPositionY(obj.position.y - ((yClose - result.y) * result.normalized.y.Abs()));
                    nudged = true;
                    directions += (int)CheckDirection.isAbove;
                }
            }
        }

        if (direction.HasFlag(CheckDirection.isBelow))
        {
            if (result.x.Abs() < objCol.offset.x + objCol.bounds.size.x + actualMargin.x)
            {
                var yClose = objCol.offset.y + objCol.bounds.size.y + actualMargin.y;
                if (yClose - result.y <= yClose && result.y <= yClose)
                {
                    obj.SetPositionY(obj.position.y - ((yClose - result.y) * result.normalized.y.Abs()));
                    nudged = true;
                    directions += (int)CheckDirection.isBelow;
                }
            }
        }

        if (direction.HasFlag(CheckDirection.isLeft))
        {
            if (result.y.Abs() < objCol.offset.y + objCol.bounds.size.y + actualMargin.y)
            {
                var xClose = objCol.offset.x - objCol.bounds.size.x - actualMargin.x;
                if (xClose - result.x >= xClose && result.x >= xClose)
                {
                    obj.SetPositionX(obj.position.x - ((xClose - result.x) * result.normalized.x.Abs()));
                    nudged = true;
                    directions += (int)CheckDirection.isLeft;
                }
            }
        }

        if (direction.HasFlag(CheckDirection.isRight))
        {
            if (result.y.Abs() < objCol.offset.y + objCol.bounds.size.y + actualMargin.y)
            {
                var xClose = objCol.offset.x + objCol.bounds.size.x + actualMargin.x;
                if (xClose - result.x <= xClose && result.x <= xClose)
                {
                    obj.SetPositionX(obj.position.x - ((xClose - result.x) * result.normalized.x.Abs()));
                    nudged = true;
                    directions += (int)CheckDirection.isRight;
                }
            }
        }

#if TESTING
        if (nudged)
        {
            Primitives.Circle(obj.position, 0.2f, color: Color.magenta, duration: 60f);
            Primitives.Square(obj, objCol.offset, objCol.bounds.size, Color.red, duration: 60f);
            Primitives.Text((Vector2)obj.position + new Vector2(0f, 0.5f), ((CheckDirection)directions).ToString(), 0.1f, Color.magenta, duration: 60f);
            Primitives.Arrow(oldObjPos, obj.position, 0.25f, Color.magenta, duration: 60f);
        }
#endif

        return nudged;
    }

    /// <inheritdoc cref="TryNudge(Transform, Collider2D, Collider2D, float, CheckDirection)" />
    public static bool TryNudge(Transform obj,
                             Collider2D[] objCols,
                             Collider2D target,
                             Vector2? margin = null,
                             CheckDirection direction = CheckDirection.isAbove)
    {
        var nudged = false;

        foreach (var objCol in objCols)
            nudged = nudged | TryNudge(obj, objCol, target, margin, direction);

        return nudged;
    }

    /// <inheritdoc cref="TryNudge(Transform, Collider2D, Collider2D, float, CheckDirection)" />
    public static bool TryNudge(Transform obj,
                             Collider2D objCol,
                             Collider2D[] targets,
                             Vector2? margin = null,
                             CheckDirection direction = CheckDirection.isAbove)
    {
        var nudged = false;

        foreach (var target in targets)
            nudged = nudged | TryNudge(obj, objCol, target, margin, direction);

        return nudged;
    }

    /// <inheritdoc cref="TryNudge(Transform, Collider2D, Collider2D, float, CheckDirection)" />
    public static bool TryNudge(Transform obj,
                             Collider2D[] objCols,
                             Collider2D[] targets,
                             Vector2? margin = null,
                             CheckDirection direction = CheckDirection.isAbove)
    {
        var nudged = false;

        foreach (var objCol in objCols)
            foreach (var target in targets)
                nudged = nudged | TryNudge(obj, objCol, target, margin, direction);

        return nudged;
    }

    /// <summary>
    /// Checks to see if objects are possibly going to overlap with regions and nudges them over if they may.
    /// </summary>
    /// <param name="points">    <see cref="Array" /> of <see cref="Transform" /> s to check</param>
    /// <param name="targetCols"><see cref="Array" /> of <see cref="Collider2D" /> s to use to check for overlaping</param>
    /// <param name="regions">   <see cref="Array" /> of <see cref="Collider2D" /> s to check against</param>
    /// <returns>
    /// <see langword="true" /> if one or more of the <see cref="Transform" /> s were nudged, <see langword="false" />
    /// if it was not or if one or more of the parameters were empty
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown if any of the parameters were <see langword="null" /></exception>
    public static bool CheckRegions(Transform[] points, Collider2D[] targetCols, GameObject[] regions, Vector2? margin = null)
    {
        if (regions == null || points == null || targetCols == null)
            throw new ArgumentNullException("One or more arguments are null, please check them before calling method.");

        if (regions.Length == 0 || points.Length == 0 || targetCols.Length == 0)
            return false;

        bool adjusted = false;

        foreach (var region in regions)
        {
            var regionBox = region.GetComponent<Collider2D>();

            if (regionBox == null)
                continue;

            foreach (var point in points)
                foreach (var col in targetCols)
                    adjusted = adjusted | NearNudge.TryNudge(point, col, regionBox, margin, (CheckDirection)15);
        }

        return adjusted;
    }
}

[Flags]
public enum CheckDirection
{
    isAbove = 1,
    isBelow = 2,
    isLeft = 4,
    isRight = 8
}
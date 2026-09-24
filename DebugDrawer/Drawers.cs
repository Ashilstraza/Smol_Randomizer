//MIT License

//Copyright (c) 2019 Phillip DaSilva-Damaskin edits by Ashilstraza 2026

//Permission is hereby granted, free of charge, to any person obtaining a copy
//of this software and associated documentation files (the "Software"), to deal
//in the Software without restriction, including without limitation the rights
//to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//copies of the Software, and to permit persons to whom the Software is
//furnished to do so, subject to the following conditions:

//The above copyright notice and this permission notice shall be included in all
//copies or substantial portions of the Software.

//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
//SOFTWARE.

using System;
using System.Buffers;
using System.Collections.Generic;

using UnityEngine;

namespace Smol_Randomizer.DebugDrawing;

public partial class DebugDrawer
{
    /// <summary>Dictionary containing the various drawers to use</summary>
    private static Dictionary<Type, IDebugDrawer> debugDrawerList = new()
    {
        {typeof(LineDrawer), new LineDrawer()},
        {typeof(SquareDrawer), new SquareDrawer()},
        {typeof(CubeDrawer), new CubeDrawer()},
        {typeof(CircleDrawer), new CircleDrawer()},
        {typeof(RawDrawer), new RawDrawer()}
    };

    /// <summary>Buffer to stage a new set of points</summary>
    private static Vector3[] drawerBuffer = new Vector3[4096];

    /// <summary>Prepare a new set of points to be drawn</summary>
    /// <typeparam name="T">Type of drawer needed</typeparam>
    /// <param name="color">   Color of the lines</param>
    /// <param name="dashed">  If the lines should be dashed</param>
    /// <param name="duration">Duration the lines should persist</param>
    /// <param name="values">  Values for configuring the set of points to draw; varies by type of drawer needed</param>
    /// <returns>The point set used</returns>
    public static void Draw<T>(Color? color, bool dashed, int duration, params object[] values)
        where T : class, IDebugDrawer
    {
        if (Instance == null) return;

        DebugDrawerPointSet[] queue = Instance.queue;
        debugDrawerList.TryGetValue(typeof(T), out IDebugDrawer drawer);

        int points = drawer.Draw(ref drawerBuffer, values);
        int position = GetFirstFreeInQueue();
        Vector3[] array = ArrayPool<Vector3>.Shared.Rent(points);
        Array.Copy(drawerBuffer, array, points);

        if (queue[position] == null)
            queue[position] = new DebugDrawerPointSet(array, color, dashed, duration);
        else
        {
            DebugDrawerPointSet pointSet = queue[position];
            pointSet.Color = color ?? Color.white;
        }

        ArrayPool<Vector3>.Shared.Return(array);
    }

    /// <summary>Draw a line in world space</summary>
    /// <param name="a">       The first point</param>
    /// <param name="b">       The second point</param>
    /// <param name="color">   Optional: Color of the line, defaults to white</param>
    /// <param name="dashed">  Optional: If the line should be dashed, defaults to solid</param>
    /// <param name="duration">Optional: Duration of frames for the line to persist, default of one frame</param>
    public static void Line(Vector3 a, Vector3 b, Color? color = null, bool dashed = false, int duration = 0)
        => Draw<LineDrawer>(color, dashed, duration, a, b);

    /// <summary>Draw a line in reference to a given transform</summary>
    /// <param name="transform">The transform to reference</param>
    /// <param name="a">        The first point</param>
    /// <param name="b">        The second point</param>
    /// <param name="color">    Optional: Color of the line, defaults to white</param>
    /// <param name="dashed">   Optional: If the line should be dashed, defaults to solid</param>
    /// <param name="duration"> Optional: Duration of frames for the line to persist, default of one frame</param>
    public static void Line(Transform transform, Vector3 a, Vector3 b, Color? color = null, bool dashed = false, int duration = 0)
        => Line(transform.TransformPoint(a), transform.TransformPoint(b), color, dashed);

    /// <summary>Draws an array of lines, useful for things like paths</summary>
    /// <param name="color">   Optional: Color of the line, defaults to white</param>
    /// <param name="dashed">  Optional: If the line should be dashed, defaults to solid</param>
    /// <param name="duration">Optional: Duration of frames for the line to persist, default of one frame</param>
    public static void Lines(Vector3[] lines, Color? color = null, bool dashed = false, int duration = 0)
        => Draw<RawDrawer>(color, dashed, duration, lines);

    /// <summary>Draws a rectangle in screen space</summary>
    /// <param name="rect">    Points of the rectangle to draw</param>
    /// <param name="camera">  The camera to use</param>
    /// <param name="color">   Optional: Color of the rectangle, defaults to white</param>
    /// <param name="dashed">  Optional: If the rectangle should be dashed, defaults to solid</param>
    /// <param name="duration">Optional: Duration of frames for the rectangle to persist, default of one frame</param>
    public static void Rect(Rect rect, Camera camera, Color? color = null, bool dashed = false, int duration = 0)
    {
        rect.y = Screen.height - rect.y;
        Vector2 corner = camera.ScreenToWorldPoint(new Vector2(rect.x, rect.y - rect.height));
        Draw<SquareDrawer>(color, dashed, duration, corner + rect.size * 0.5f, Quaternion.identity, rect.size);
    }

    /// <summary>Draw square in world space with a rotation parameter.</summary>
    /// <param name="position">Center point of the square</param>
    /// <param name="rotation">Rotation of the square</param>
    /// <param name="size">    Size of the square</param>
    /// <param name="color">   Optional: Color of the square, defaults to white</param>
    /// <param name="dashed">  Optional: If the square should be dashed, defaults to solid</param>
    /// <param name="duration">Optional: Duration of frames for the square to persist, default of one frame</param>
    public static void Square(Vector2 position, Quaternion rotation, Vector2 size, Color? color = null, bool dashed = false, int duration = 0)
        => Draw<SquareDrawer>(color, dashed, duration, position, rotation, size);

    /// <summary>Draw square in world space</summary>
    /// <param name="position">Center point of the square</param>
    /// <param name="size">    Size of the square</param>
    /// <param name="color">   Optional: Color of the square, defaults to white</param>
    /// <param name="dashed">  Optional: If the square should be dashed, defaults to solid</param>
    /// <param name="duration">Optional: Duration of frames for the square to persist, default of one frame</param>
    public static void Square(Vector2 position, Vector2 size, Color? color = null, bool dashed = false, int duration = 0)
        => Square(position, Quaternion.identity, size, color, dashed, duration);

    /// <summary>Draw square in world space with float diameter parameter.</summary>
    /// <param name="position">Center point of the square</param>
    /// <param name="diameter">Diameter of the square</param>
    /// <param name="color">   Optional: Color of the square, defaults to white</param>
    /// <param name="dashed">  Optional: If the square should be dashed, defaults to solid</param>
    /// <param name="duration">Optional: Duration of frames for the square to persist, default of one frame</param>
    public static void Square(Vector2 position, float diameter, Color? color = null, bool dashed = false, int duration = 0)
        => Square(position, Quaternion.identity, Vector2.one * diameter, color, dashed, duration);

    /// <summary>Draw a square in reference to a given transform</summary>
    /// <param name="transform">The transform to reference</param>
    /// <param name="offset">   Offset of the square</param>
    /// <param name="color">    Optional: Color of the square, defaults to white</param>
    /// <param name="dashed">   Optional: If the square should be dashed, defaults to solid</param>
    /// <param name="duration"> Optional: Duration of frames for the square to persist, default of one frame</param>
    public static void Square(Transform transform, Vector2 offset, Vector2 size, Color? color = null, bool dashed = false, int duration = 0)
    {
        double rotation = Math.PI * (transform.rotation.eulerAngles.z) / 180;
        float sinRotation = (float)Math.Sin(rotation);
        float cosRotation = (float)Math.Cos(rotation);

        Vector2 rotatedOffset = new Vector2(offset.x * cosRotation + offset.y * sinRotation, offset.y * cosRotation + offset.x * sinRotation);
        Vector2 position = (Vector2)transform.position + rotatedOffset;
        Square(position, transform.rotation, size * transform.localScale, color, dashed, duration);
    }

    /// <summary>Draws a cube in world space.</summary>
    /// <param name="position">Center point of the cube</param>
    /// <param name="rotation">Rotation of the cube</param>
    /// <param name="size">    Size of the cube</param>
    /// <param name="color">   Optional: Color of the cube, defaults to white</param>
    /// <param name="dashed">  Optional: If the cube should be dashed, defaults to solid</param>
    /// <param name="duration">Optional: Duration of frames for the cube to persist, default of one frame</param>
    public static void Cube(Vector3 position, Quaternion rotation, Vector3 size, Color? color = null, bool dashed = false, int duration = 0)
        => Draw<CubeDrawer>(color, dashed, duration, position, rotation, size);

    /// <summary>Draws a representation of a bounding box</summary>
    /// <param name="bounds">  Bounding box to draw</param>
    /// <param name="color">   Optional: Color of the bounding box, defaults to white</param>
    /// <param name="dashed">  Optional: If the bounding box should be dashed, defaults to solid</param>
    /// <param name="duration">Optional: Duration of frames for the bounding box to persist, default of one frame</param>
    public static void Bounds(Bounds bounds, Color? color = null, bool dashed = false, int duration = 0)
        => Draw<CubeDrawer>(color, dashed, duration, bounds.center, Quaternion.identity, bounds.size);

    /// <summary>Draws a cone similar to the one that spot lights draw</summary>
    /// <param name="position">   Center point of the cube</param>
    /// <param name="rotation">   Rotation of the cube</param>
    /// <param name="length">     Length of the cone lines</param>
    /// <param name="angle">      Angle of the cone</param>
    /// <param name="color">      Optional: Color of the cone, defaults to white</param>
    /// <param name="dashed">     Optional: If the cone should be dashed, defaults to solid</param>
    /// <param name="pointsCount">Optional: Number of points on the cone's base</param>
    /// <param name="duration">   Optional: Duration of frames for the cone to persist, default of one frame</param>
    public static void Cone(Vector3 position, Quaternion rotation, float length, float angle, Color? color = null, bool dashed = false, int pointsCount = 16, int duration = 0)
    {
        // draw the end of the cone
        float endAngle = Mathf.Tan(angle * 0.5f * Mathf.Deg2Rad) * length;
        Vector3 forward = rotation * Vector3.forward;
        Vector3 endPosition = position + forward * length;
        float offset = 0f;
        Draw<CircleDrawer>(color, dashed, duration, endPosition, pointsCount, endAngle, offset, rotation);

        // draw the 4 lines
        for (int i = 0; i < 4; i++)
        {
            float a = i * 90f * Mathf.Deg2Rad;
            Vector3 point = rotation * new Vector3(Mathf.Cos(a), Mathf.Sin(a)) * endAngle;
            Line(position, position + point + forward * length, color, dashed);
        }
    }

    /// <summary>Draws a sphere at position with specified radius</summary>
    /// <param name="position">   Center point of the sphere</param>
    /// <param name="radius">     Radius of the sphere</param>
    /// <param name="color">      Optional: Color of the sphere, defaults to white</param>
    /// <param name="dashed">     Optional: If the sphere should be dashed, defaults to solid</param>
    /// <param name="pointsCount">Optional: Number of points on the sphere's base</param>
    /// <param name="duration">   Optional: Duration of frames for the sphere to persist, default of one frame</param>
    public static void Sphere(Vector3 position, float radius, Color? color = null, bool dashed = false, int pointsCount = 16, int duration = 0)
    {
        float offset = 0f;
        Draw<CircleDrawer>(color, dashed, duration, position, pointsCount, radius, offset, Quaternion.Euler(0f, 0f, 0f));
        Draw<CircleDrawer>(color, dashed, duration, position, pointsCount, radius, offset, Quaternion.Euler(90f, 0f, 0f));
        Draw<CircleDrawer>(color, dashed, duration, position, pointsCount, radius, offset, Quaternion.Euler(0f, 90f, 90f));
    }

    /// <summary>Draws a circle in world space and billboards towards the camera</summary>
    /// <param name="position">   Center point of the circle</param>
    /// <param name="radius">     Radius of the circle</param>
    /// <param name="camera">     The camera to use</param>
    /// <param name="color">      Optional: Color of the circle, defaults to white</param>
    /// <param name="dashed">     Optional: If the circle should be dashed, defaults to solid</param>
    /// <param name="pointsCount">Optional: Number of points on the circle's base</param>
    /// <param name="duration">   Optional: Duration of frames for the circle to persist, default of one frame</param>
    public static void Circle(Vector3 position, float radius, Camera camera, Color? color = null, bool dashed = false, int pointsCount = 16, int duration = 0)
    {
        float offset = 0f;
        Quaternion rotation = Quaternion.LookRotation(position - camera.transform.position);
        Draw<CircleDrawer>(color, dashed, duration, position, pointsCount, radius, offset, rotation);
    }

    /// <summary>Draws a circle in world space with a specified rotation</summary>
    /// <param name="position">   Center point of the circle</param>
    /// <param name="radius">     Radius of the circle</param>
    /// <param name="rotation">   Rotation of the circle</param>
    /// <param name="color">      Optional: Color of the circle, defaults to white</param>
    /// <param name="dashed">     Optional: If the circle should be dashed, defaults to solid</param>
    /// <param name="pointsCount">Optional: Number of points on the circle's base</param>
    /// <param name="duration">   Optional: Duration of frames for the circle to persist, default of one frame</param>
    public static void Circle(Vector3 position, float radius, Quaternion rotation, Color? color = null, bool dashed = false, int pointsCount = 16, int duration = 0)
    {
        float offset = 0f;
        Draw<CircleDrawer>(color, dashed, duration, position, pointsCount, radius, offset, rotation);
    }

    /// <summary>Draws a circle in reference to a given transform</summary>
    /// <param name="transform">  The transform to reference</param>
    /// <param name="position">   Center point of the circle</param>
    /// <param name="radius">     Radius of the circle</param>
    /// <param name="rotation">   Rotation of the circle</param>
    /// <param name="color">      Optional: Color of the circle, defaults to white</param>
    /// <param name="dashed">     Optional: If the circle should be dashed, defaults to solid</param>
    /// <param name="pointsCount">Optional: Number of points on the circle's base</param>
    /// <param name="duration">   Optional: Duration of frames for the circle to persist, default of one frame</param>
    public static void Circle(Transform transform, Vector3 position, float radius, Quaternion rotation, Color? color = null, bool dashed = false, int pointsCount = 16, int duration = 0)
        => Circle(transform.TransformPoint(position), radius, rotation, color, dashed);
}

#region Drawers

public class RawDrawer : IDebugDrawer
{
    public int Draw(ref Vector3[] buffer, params object[] values)
    {
        Vector3[] lines = (Vector3[])values[0];

        if (lines.Length > buffer.Length)
            throw new OverflowException("Line length exceeds buffer size.");

        for (int i = 0; i < lines.Length; i++)
        {
            buffer[i] = lines[i];
        }

        return lines.Length;
    }
}

public class LineDrawer : IDebugDrawer
{
    public int Draw(ref Vector3[] buffer, params object[] values)
    {
        buffer[0] = (Vector3)values[0];
        buffer[1] = (Vector3)values[1];
        return 2;
    }
}

public class SquareDrawer : IDebugDrawer
{
    public int Draw(ref Vector3[] buffer, params object[] values)
    {
        Vector2 position = default;
        if (values[0] is Vector2 p2)
        {
            position = p2;
        }
        else if (values[0] is Vector3 p3)
        {
            position = p3;
        }

        Quaternion rotation = (Quaternion)values[1];

        Vector2 size = default;
        if (values[2] is Vector2 s2)
        {
            size = s2;
        }
        else if (values[2] is Vector3 s3)
        {
            size = s3;
        }

        size *= 0.5f;

        Vector2 point1 = new Vector3(position.x - size.x, position.y - size.y);
        Vector2 point2 = new Vector3(position.x + size.x, position.y - size.y);
        Vector2 point3 = new Vector3(position.x + size.x, position.y + size.y);
        Vector2 point4 = new Vector3(position.x - size.x, position.y + size.y);

        point1 = rotation * (point1 - position);
        point1 += position;

        point2 = rotation * (point2 - position);
        point2 += position;

        point3 = rotation * (point3 - position);
        point3 += position;

        point4 = rotation * (point4 - position);
        point4 += position;

        //square
        buffer[0] = point1;
        buffer[1] = point2;

        buffer[2] = point2;
        buffer[3] = point3;

        buffer[4] = point3;
        buffer[5] = point4;

        //loop back to start
        buffer[6] = point4;
        buffer[7] = point1;

        return 8;
    }
}

public class CubeDrawer : IDebugDrawer
{
    public int Draw(ref Vector3[] buffer, params object[] values)
    {
        Vector3 position = (Vector3)values[0];
        Quaternion rotation = (Quaternion)values[1];
        Vector3 size = (Vector3)values[2];

        size *= 0.5f;

        Vector3 point1 = new Vector3(position.x - size.x, position.y - size.y, position.z - size.z);
        Vector3 point2 = new Vector3(position.x + size.x, position.y - size.y, position.z - size.z);
        Vector3 point3 = new Vector3(position.x + size.x, position.y + size.y, position.z - size.z);
        Vector3 point4 = new Vector3(position.x - size.x, position.y + size.y, position.z - size.z);

        Vector3 point5 = new Vector3(position.x - size.x, position.y - size.y, position.z + size.z);
        Vector3 point6 = new Vector3(position.x + size.x, position.y - size.y, position.z + size.z);
        Vector3 point7 = new Vector3(position.x + size.x, position.y + size.y, position.z + size.z);
        Vector3 point8 = new Vector3(position.x - size.x, position.y + size.y, position.z + size.z);

        point1 = rotation * (point1 - position);
        point1 += position;

        point2 = rotation * (point2 - position);
        point2 += position;

        point3 = rotation * (point3 - position);
        point3 += position;

        point4 = rotation * (point4 - position);
        point4 += position;

        point5 = rotation * (point5 - position);
        point5 += position;

        point6 = rotation * (point6 - position);
        point6 += position;

        point7 = rotation * (point7 - position);
        point7 += position;

        point8 = rotation * (point8 - position);
        point8 += position;

        //square
        buffer[0] = point1;
        buffer[1] = point2;

        buffer[2] = point2;
        buffer[3] = point3;

        buffer[4] = point3;
        buffer[5] = point4;

        buffer[6] = point4;
        buffer[7] = point1;

        //other square
        buffer[8] = point5;
        buffer[9] = point6;

        buffer[10] = point6;
        buffer[11] = point7;

        buffer[12] = point7;
        buffer[13] = point8;

        buffer[14] = point8;
        buffer[15] = point5;

        //connectors
        buffer[16] = point1;
        buffer[17] = point5;

        buffer[18] = point2;
        buffer[19] = point6;

        buffer[20] = point3;
        buffer[21] = point7;

        buffer[22] = point4;
        buffer[23] = point8;

        return 24;
    }
}

public class CircleDrawer : IDebugDrawer
{
    public int Draw(ref Vector3[] buffer, params object[] values)
    {
        Vector3 position = (Vector3)values[0];
        int points = (int)values[1];
        float radius = (float)values[2];
        float offset = (float)values[3];
        Quaternion rotation = (Quaternion)values[4];

        float step = 360f / points;
        offset *= Mathf.Deg2Rad;

        for (int i = 0; i < points; i++)
        {
            float cx = Mathf.Cos(Mathf.Deg2Rad * step * i + offset) * radius;
            float cy = Mathf.Sin(Mathf.Deg2Rad * step * i + offset) * radius;
            Vector3 current = new Vector3(cx, cy);

            float nx = Mathf.Cos(Mathf.Deg2Rad * step * (i + 1) + offset) * radius;
            float ny = Mathf.Sin(Mathf.Deg2Rad * step * (i + 1) + offset) * radius;
            Vector3 next = new Vector3(nx, ny);

            buffer[i * 2] = position + (rotation * current);
            buffer[(i * 2) + 1] = position + (rotation * next);
        }

        return points * 2;
    }
}

#endregion Drawers

public interface IDebugDrawer
{
    public abstract int Draw(ref Vector3[] buffer, params object[] args);
}
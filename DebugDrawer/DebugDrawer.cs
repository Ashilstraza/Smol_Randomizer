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
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Rendering;

namespace Smol_Randomizer.DebugDrawing;

/// <summary>Based off of Gizmos by Phillip DaSilva-Damaskin; copy of License at the top of this file for posterity.</summary>
public partial class DebugDrawer : MonoBehaviour
{
    private static Material drawMaterial;

    public static Material DrawMaterial
    {
        get
        {
            if (!drawMaterial)
            {
                Shader shader = Shader.Find("Hidden/Internal-Colored");
                drawMaterial = new Material(shader)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                };

                drawMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                drawMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                drawMaterial.SetInt("_Cull", (int)CullMode.Off);
                drawMaterial.SetInt("_ZWrite", 0);
            }

            return drawMaterial;
        }
    }

    private static DebugDrawer instance;

    internal static DebugDrawer? Instance
    {
        get
        {
#if !TESTING
            return null;
#else
            if (!instance)
            {
                instance = new GameObject(typeof(DebugDrawer).FullName).AddComponent<DebugDrawer>();
            }
            return instance;
#endif
        }
    }

    private float dashGap = 0.1f;
    public float DashGap => dashGap;

    private bool frustumCulling = true;
    public bool FrustumCulling => frustumCulling;

    private DebugDrawerPointSet[] queue = new DebugDrawerPointSet[4096];

    private void OnEnable()
    {
        RenderPipelineManager.endCameraRendering += OnRender;
    }

    private void OnDisable()
    {
        RenderPipelineManager.endCameraRendering -= OnRender;
    }

    private void Update()
    {
    }

    private static void OnRender(ScriptableRenderContext context, Camera camera)
    {
        DrawMaterial.SetPass(0);
        DebugDrawer? instance = Instance;

        if (instance == null) return;

        // Hop out early if this is disabled or the camera is not the main camera.
        if (!instance.enabled || !camera.CompareTag("MainCamera"))
            return;

        GL.PushMatrix();
        GL.MultMatrix(Matrix4x4.identity);
        GL.Begin(GL.LINES);

        bool decrementDuration = Time.deltaTime != 0;
        bool alt = Time.time % 1 > 0.5f;
        float dashGap = Mathf.Clamp(instance.DashGap, 0.01f, 32f);
        bool frustumCull = instance.FrustumCulling;

        foreach (DebugDrawerPointSet pointSet in instance.queue)
        {
            if (pointSet.DurationRemaining < 0) // Point Set expired
                continue;

            if (frustumCull && !IsVisibleByCamera(pointSet, camera)) // Check to see if the thing is out of view
                continue;

            GL.Color(pointSet.Color);

            foreach (var point in pointSet.GetVertexArray(alt, decrementDuration))
                GL.Vertex(point);
        }
    }

    internal static int GetFirstFreeInQueue()
    {
        DebugDrawer? instance = Instance;

        if (instance == null) return -1;

        int queueLength = instance.queue.Length;

        for (int i = 0; i < queueLength; i++)
        {
            if (instance.queue[i] == null)
                return i;
            else if (instance.queue[i].DurationRemaining < 0)
                return i;
        }

        // Buffer is full, make it bigger!
        DebugDrawerPointSet[] newQueue = new DebugDrawerPointSet[queueLength + 4096];
        Array.Copy(instance.queue, newQueue, queueLength);
        instance.queue = newQueue;

        return queueLength + 1;
    }

    /// <summary>Check if at least one point is visible by the camera</summary>
    /// <param name="pointSet">Set of points to check</param>
    /// <param name="camera">  The camera to use</param>
    /// <returns>True if at least one point is visible, false if not visible</returns>
    private static bool IsVisibleByCamera(DebugDrawerPointSet pointSet, Camera camera)
    {
        foreach (var points in pointSet.Points)
        {
            Vector3 vp = camera.WorldToViewportPoint(points, camera.stereoActiveEye);

            if (vp.x is >= 0 and <= 1 && vp.y is >= 0 and <= 1)
                return true;
        }

        return false;
    }

    public class DebugDrawerPointSet
    {
        private Vector3[] points;

        public Vector3[] Points
        {
            get => points;
            set
            {
                points = value;
                UpdateDashedPoints();
            }
        }

        private Vector3[] dashedPointsA = [];

        public Vector3[] DashedPointsA
        {
            get
            {
                if (dashed)
                    return dashedPointsA;
                return [];
            }
        }

        private Vector3[] dashedPointsB = [];

        public Vector3[] DashedPointsB
        {
            get
            {
                if (dashed)
                    return DashedPointsB;
                return [];
            }
        }

        private Color color;

        public Color Color
        {
            get => color;
            set => color = value;
        }

        private bool dashed;

        public bool Dashed
        {
            get => dashed;
            set
            {
                dashed = value;
                UpdateDashedPoints();
            }
        }

        private int duration;

        public int Duration
        {
            get => duration;
            set => duration = value;
        }

        private int durationRemaining;
        public int DurationRemaining => durationRemaining;

        public DebugDrawerPointSet(Vector3[] points, Color? color, bool dashed = false, int duration = 0)
        {
            this.points = points;
            this.color = color ?? Color.white;
            this.dashed = dashed;
            this.duration = duration;

            UpdateDashedPoints();
        }

        public DebugDrawerPointSet(Vector3[] points, bool dashed = false, int duration = 0)
            : this(points, null, dashed, duration) { }

        /// <summary>Precompute dashed points</summary>
        /// <param name="points">      Array of points to be dashed</param>
        /// <param name="dashedPoints">Reference to the array of dashed points</param>
        /// <param name="alt">         If the alternate set of dashes should be calculated</param>
        private void DashedPoints(Vector3[] points, ref Vector3[] dashedPoints, bool alt)
        {
            float dashGap = DebugDrawer.Instance?.dashGap ?? 0.1f;

            List<Vector3> workingPoints = new List<Vector3>();

            for (int i = 0; i < points.Length - 1; i++)
            {
                Vector3 pointA = points[i];
                Vector3 pointB = points[i + 1];
                Vector3 direction = pointB - pointA;
                if (direction.sqrMagnitude > dashGap * dashGap * 2f)
                {
                    float magnitude = direction.magnitude;
                    int amount = Mathf.RoundToInt(magnitude / dashGap);
                    direction /= magnitude;

                    for (int p = 0; p < amount - 1; p++)
                    {
                        if (p % 2 == (alt ? 1 : 0))
                        {
                            float startLerp = p / (amount - 1f);
                            float endLerp = (p + 1) / (amount - 1f);
                            Vector3 start = Vector3.Lerp(pointA, pointB, startLerp);
                            Vector3 end = Vector3.Lerp(pointA, pointB, endLerp);
                            workingPoints.Add(start);
                            workingPoints.Add(end);
                        }
                    }
                }
                else
                {
                    workingPoints.Add(pointA);
                    workingPoints.Add(pointB);
                }
            }

            dashedPoints = workingPoints.ToArray();
        }

        /// <summary>Updates the dashed points. Use if you changed the dash gap.</summary>
        public void UpdateDashedPoints()
        {
            if (!dashed) return;
            DashedPoints(points, ref dashedPointsA, false);
            DashedPoints(points, ref dashedPointsB, true);
        }

        public Vector3[] GetVertexArray(bool alt = false, bool updateDuration = false)
        {
            if (updateDuration)
                duration--;
            if (dashed)
                return alt ? dashedPointsB : dashedPointsA;
            return points;
        }
    }
}
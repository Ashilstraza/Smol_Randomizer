using HarmonyLib;

using UnityEngine;

namespace Smol_Randomizer.DebugDrawing;

internal static class UnityDebugPatches
{
    internal static void RegisterHarmonyPatches()
    {
        CuteRandoCore.harmony.Patch(AccessTools.Method(
            typeof(Debug),
            "DrawLine",
            parameters: [typeof(Vector3), typeof(Vector3), typeof(Color), typeof(float)]),
            postfix: new HarmonyMethod(
                typeof(UnityDebugPatches),
                nameof(Debug_DrawLine_Postfix),
                argumentTypes: [typeof(Vector3), typeof(Vector3), typeof(Color), typeof(float)]));
        CuteRandoCore.harmony.Patch(AccessTools.Method(
            typeof(Debug),
            "DrawLine",
            parameters: [typeof(Vector3), typeof(Vector3), typeof(Color)]),
            postfix: new HarmonyMethod(
                typeof(UnityDebugPatches),
                nameof(Debug_DrawLine_Postfix),
                argumentTypes: [typeof(Vector3), typeof(Vector3), typeof(Color)]));
        CuteRandoCore.harmony.Patch(AccessTools.Method(
            typeof(Debug),
            "DrawLine",
            parameters: [typeof(Vector3), typeof(Vector3)]),
            postfix: new HarmonyMethod(
                typeof(UnityDebugPatches),
                nameof(Debug_DrawLine_Postfix),
                argumentTypes: [typeof(Vector3), typeof(Vector3)]));
    }

    private static void Debug_DrawLine_Postfix(Vector3 start, Vector3 end, Color? color, float duration)
    {
        int frames = (int)(UnityEngine.Application.targetFrameRate * duration);
        DebugDrawer.Line(start, end, color, false, frames);
    }

    private static void Debug_DrawLine_Postfix(Vector3 start, Vector3 end, Color color)
    {
        Debug_DrawLine_Postfix(start, end, color, 0f);
    }

    private static void Debug_DrawLine_Postfix(Vector3 start, Vector3 end)
    {
        Debug_DrawLine_Postfix(start, end, Color.white, 0f);
    }
}
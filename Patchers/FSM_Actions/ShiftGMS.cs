using HutongGames.PlayMaker;

using UnityEngine;

namespace Smol_Randomizer.Patchers.FSM_Actions;

/// <summary>Special <see cref="FsmStateAction" /> to adjust Grand Mother Silk's dash attack height.</summary>
internal class ShiftGMS : FsmStateAction
{
    /// <summary>Offset for dash attack antic</summary>
    private const float ANTIC_Y_OFFSET = 3.18855f;

    /// <summary>Offset for dash attack</summary>
    private const float ATTACK_Y_OFFSET = 0.18855f;

    /// <summary>Height of the platform</summary>
    private const float PLATFORM_Y = 132.687f;

    /// <summary>Multiplier for gms to get proper height</summary>
    private const float GMS_MULTIPLIER = 3.085f;

    /// <summary>GMS' <see cref="GameObject" /></summary>
    public GameObject? GMS;

    /// <summary><see cref="FsmFloat" /> for the Y position of the attack</summary>
    public FsmFloat? YPosition;

    /// <summary>
    /// <see langword="true" /> If this is for the dash attack antic, <see langword="false" /> if it is for the attack itself
    /// </summary>
    public bool Antic = true;

    /// <summary>Hornet's <see cref="BoxCollider2D" /></summary>
    private BoxCollider2D? hornet;

    /// <summary>Reset this to the default values</summary>
    public override void Reset()
    {
        GMS = null;
        YPosition = null;
        Antic = true;

        hornet = null;
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

    /// <summary>Adjusts <see cref="YPosition" /> to be located just above Hornet's standing Y center point</summary>
    private void Adjust()
    {
        if (hornet == null)
            hornet = FsmVariables.GlobalVariables.FindFsmGameObject("Hero").Value.GetComponent<BoxCollider2D>();

        if (hornet == null || GMS == null || YPosition == null) return;

        YPosition.Value = PLATFORM_Y + hornet.bounds.extents.y + (Antic ? ANTIC_Y_OFFSET : ATTACK_Y_OFFSET) + GMS_MULTIPLIER * GMS.transform.GetAbsScaleY();
    }
}
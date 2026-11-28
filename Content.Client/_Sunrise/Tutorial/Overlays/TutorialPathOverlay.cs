using System.Numerics;
using Content.Shared._Sunrise.Tutorial.Components;
using Content.Shared._Sunrise.Tutorial.Conditions;
using Content.Shared._Sunrise.Tutorial.Prototypes;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Sunrise.Tutorial.Overlays;

/// <summary>
/// Draws an animated dotted trail from the local player toward the current tutorial
/// navigation target, and an interaction-zone ring around goal markers.
/// </summary>
public sealed class TutorialPathOverlay(
    IEntityManager entManager,
    IPlayerManager player,
    IGameTiming timing,
    SharedTransformSystem transform,
    IPrototypeManager proto) : Overlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    private const float DotRadius = 0.07f;
    private const float DotSpacing = 0.9f;
    private const float MinPlayerDist = 1.2f;
    private const float MinTargetDist = 0.9f;
    private const float AnimSpeed = 2.5f;
    private const int ZoneSegments = 48;
    private const float DefaultZoneRadius = 2f;

    private static readonly Color PathColor = Color.FromHex("#FFD84D");
    private static readonly Color ZoneColor = Color.FromHex("#FFD84D66");

    protected override void Draw(in OverlayDrawArgs args)
    {
        var localPlayer = player.LocalEntity;
        if (localPlayer == null)
            return;

        if (!entManager.TryGetComponent<TutorialPlayerComponent>(localPlayer, out var tutComp))
            return;

        if (!entManager.TryGetComponent<TransformComponent>(localPlayer, out var playerXform))
            return;

        var playerMap = transform.GetMapCoordinates(localPlayer.Value, xform: playerXform);
        if (playerMap.MapId == MapId.Nullspace || playerMap.MapId != args.MapId)
            return;

        var targetUid = tutComp.Target ?? tutComp.CurrentBubbleTarget;

        // Dotted path toward target
        if (targetUid is { } tid && entManager.EntityExists(tid) && tid != localPlayer
            && entManager.TryGetComponent<TransformComponent>(tid, out var targetXform))
        {
            var targetMap = transform.GetMapCoordinates(tid, xform: targetXform);
            if (targetMap.MapId == playerMap.MapId)
                DrawDottedPath(args.WorldHandle, playerMap.Position, targetMap.Position);
        }

        // Zone ring around goal-marker targets
        if (tutComp.Target is { } markerUid
            && entManager.HasComponent<TutorialGoalMarkerComponent>(markerUid)
            && entManager.TryGetComponent<TransformComponent>(markerUid, out var markerXform))
        {
            var markerMap = transform.GetMapCoordinates(markerUid, xform: markerXform);
            if (markerMap.MapId == args.MapId)
                DrawZoneRing(args.WorldHandle, markerMap.Position, GetMarkerDistance(tutComp));
        }
    }

    private void DrawDottedPath(DrawingHandleWorld handle, Vector2 from, Vector2 to)
    {
        var delta = to - from;
        var totalDist = delta.Length();
        if (totalDist < MinPlayerDist + MinTargetDist)
            return;

        var dir = delta / totalDist;
        var time = (float)timing.CurTime.TotalSeconds;

        for (var dist = MinPlayerDist; dist <= totalDist - MinTargetDist; dist += DotSpacing)
        {
            var phase = dist / DotSpacing - time * AnimSpeed;
            var wave = (MathF.Sin(phase * MathF.PI) + 1f) * 0.5f;
            handle.DrawCircle(
                from + dir * dist,
                DotRadius * (0.6f + 0.4f * wave),
                PathColor.WithAlpha(0.25f + 0.75f * wave));
        }
    }

    private static void DrawZoneRing(DrawingHandleWorld handle, Vector2 center, float radius)
    {
        var step = MathF.PI * 2f / ZoneSegments;
        var prev = center + new Vector2(radius, 0f);
        for (var i = 1; i <= ZoneSegments; i++)
        {
            var angle = i * step;
            var pt = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
            handle.DrawLine(prev, pt, ZoneColor);
            prev = pt;
        }
    }

    private float GetMarkerDistance(TutorialPlayerComponent comp)
    {
        TutorialStepPrototype? step;
        if (comp.ActiveStepOverride is { } overrideStep)
        {
            if (!proto.TryIndex(overrideStep, out step))
                return DefaultZoneRadius;
        }
        else
        {
            if (!proto.TryIndex(comp.SequenceId, out var sequence))
                return DefaultZoneRadius;
            if (comp.StepIndex < 0 || comp.StepIndex >= sequence.Steps.Count)
                return DefaultZoneRadius;
            if (!proto.TryIndex(sequence.Steps[comp.StepIndex], out step))
                return DefaultZoneRadius;
        }

        if (TryGetMarkerDistance(step.Conditions, out var distance) ||
            TryGetMarkerDistance(step.AnyConditions, out distance))
            return distance;

        return DefaultZoneRadius;
    }

    private static bool TryGetMarkerDistance(List<TutorialCondition> conditions, out float distance)
    {
        foreach (var condition in conditions)
        {
            switch (condition)
            {
                case ReachMarkerCondition reach:
                    distance = reach.Distance;
                    return true;
                case ItemPlacedNearMarkerCondition placed:
                    distance = placed.Distance;
                    return true;
            }
        }

        distance = default;
        return false;
    }
}

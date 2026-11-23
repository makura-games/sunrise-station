using System.Threading;
using Content.Shared._Sunrise.BloodCult.Components;
using Content.Shared._Sunrise.BloodCult.Items;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Maps;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Timing;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Server._Sunrise.BloodCult.Items.Systems;

public sealed partial class VoidTeleportSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private PullingSystem _pulling = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VoidTeleportComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<VoidTeleportComponent, ComponentInit>(OnInit);
    }

    private void OnInit(EntityUid uid, VoidTeleportComponent component, ComponentInit args)
    {
        UpdateAppearance(uid, component);
    }

    private void OnUseInHand(EntityUid uid, VoidTeleportComponent component, UseInHandEvent args)
    {
        if (!HasComp<BloodCultistComponent>(args.User))
        {
            _hands.TryDrop(args.User);
            _popup.PopupEntity(Loc.GetString("void-teleport-not-cultist"), args.User, args.User);
            return;
        }

        if (!component.Active || component.UsesLeft <= 0)
        {
            _popup.PopupEntity(Loc.GetString("void-teleport-drained"), args.User, args.User);
            return;
        }

        if (component.NextUse > _timing.CurTime)
        {
            _popup.PopupEntity(Loc.GetString("void-teleport-cooldown"), args.User, args.User);
            return;
        }

        if (!TryComp(args.User, out TransformComponent? transform))
            return;

        var oldCoords = transform.Coordinates;

        EntityCoordinates coords = default;
        var attempts = 10;
        //Repeat until proper place for tp is found
        while (attempts > 0)
        {
            attempts--;
            //Get coords to where tp
            var random = new Random().Next(component.MinRange, component.MaxRange);
            var offset = transform.LocalRotation.ToWorldVec().Normalized();
            var direction = transform.LocalRotation.GetDir().ToVec();
            var newOffset = offset + direction * random;
            coords = transform.Coordinates.Offset(newOffset).SnapToGrid(EntityManager);

            var tile = _turf.GetTileRef(coords);

            if (tile == null)
                continue;

            if (_turf.IsTileBlocked(tile.Value, CollisionGroup.AllMask))
                continue;

            break;
        }

        CreatePulse(uid, component);

        _xform.SetCoordinates(args.User, coords);
        transform.AttachToGridOrMap();

        var pulled = GetPulledEntity(args.User);
        if (pulled != null)
        {
            _xform.SetCoordinates(pulled.Value, coords);
            _pulling.TryStopPull(pulled.Value.Owner, pulled.Value.Comp);

            if (TryComp(pulled.Value, out TransformComponent? pulledTransform))
                pulledTransform.AttachToGridOrMap();
        }

        //Play tp sound
        _audio.PlayPvs(component.TeleportInSound, coords);
        _audio.PlayPvs(component.TeleportOutSound, oldCoords);

        //Create tp effect
        _entMan.SpawnEntity(component.TeleportInEffect, coords);
        _entMan.SpawnEntity(component.TeleportOutEffect, oldCoords);

        component.UsesLeft--;
        component.NextUse = _timing.CurTime + component.Cooldown;
    }

    private void UpdateAppearance(EntityUid uid, VoidTeleportComponent comp)
    {
        AppearanceComponent? appearance = null;
        if (!Resolve(uid, ref appearance, false))
            return;

        _appearance.SetData(uid, VeilVisuals.Activated, comp.Active, appearance);
    }

    private Entity<PullableComponent>? GetPulledEntity(EntityUid user)
    {
        Entity<PullableComponent>? pulled = null;

        if (TryComp<PullerComponent>(user, out var puller) && puller.Pulling != null &&
            TryComp<PullableComponent>(puller.Pulling.Value, out var pullableComponent))
            pulled = (puller.Pulling.Value, pullableComponent);

        return pulled;
    }

    private void CreatePulse(EntityUid uid, VoidTeleportComponent component)
    {
        if (TryComp<PointLightComponent>(uid, out var light))
#pragma warning disable RA0002
            light.Energy = 5f;
#pragma warning restore RA0002

        Timer.Spawn(component.TimerDelay, () => TurnOffPulse(uid, component), component.Token.Token);
    }

    private void TurnOffPulse(EntityUid uid, VoidTeleportComponent comp)
    {
        if (!TryComp<PointLightComponent>(uid, out var light))
            return;

#pragma warning disable RA0002
        light.Energy = 1f;
#pragma warning restore RA0002

        comp.Token = new CancellationTokenSource();

        if (comp.UsesLeft <= 0)
        {
            comp.Active = false;
            UpdateAppearance(uid, comp);

#pragma warning disable RA0002
            light.Enabled = false;
#pragma warning restore RA0002
        }
    }
}

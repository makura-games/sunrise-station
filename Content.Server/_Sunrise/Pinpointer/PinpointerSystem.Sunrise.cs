using Content.Server.Popups;
using Content.Shared.Pinpointer;
using Content.Shared.Verbs;
using Robust.Shared.Random;
using Robust.Shared.Utility;

#pragma warning disable IDE0130 // Sunrise-Edit — partial для ванильной системы хранится в _Sunrise
namespace Content.Server.Pinpointer;

public sealed partial class PinpointerSystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private PopupSystem _popups = default!;
    [Dependency] private EntityQuery<TransformComponent> _xformQuery = default!;

    private void InitializeSunrise()
    {
        SubscribeLocalEvent<PinpointerComponent, GetVerbsEvent<AlternativeVerb>>(AddSwitchVerb);
    }

    private void AddSwitchVerb(Entity<PinpointerComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Act = () => SwitchTarget(ent, user),
            Text = Loc.GetString("pinpointer-switch-target"),
            Priority = 2,
        });
    }

    private void SwitchTarget(Entity<PinpointerComponent> ent, EntityUid user)
    {
        if (ent.Comp.IsActive && ent.Comp.Component != null)
        {
            if (!EntityManager.ComponentFactory.TryGetRegistration(ent.Comp.Component, out var registration))
            {
                Log.Error($"Unable to find component registration for {ent.Comp.Component} for pinpointer!");
                DebugTools.Assert(false);
                return;
            }

            var target = FindNextSunriseTargetFromComponent(ent.Owner, registration.Type, ent.Comp.Target);
            SetTarget(ent.AsNullable(), target);
        }

        _popups.PopupEntity(Loc.GetString("pinpointer-target-switched"), user, user);
    }

    private EntityUid? FindNextSunriseTargetFromComponent(
        Entity<TransformComponent?> ent,
        Type componentType,
        EntityUid? currentTarget)
    {
        if (!Resolve(ent, ref ent.Comp))
            return null;

        var mapId = ent.Comp.MapID;
        List<EntityUid> targets = [];

        foreach (var (otherUid, _) in EntityManager.GetAllComponents(componentType))
        {
            if (!_xformQuery.TryGetComponent(otherUid, out var otherTransform) || otherTransform.MapID != mapId)
                continue;

            targets.Add(otherUid);
        }

        if (targets.Count > 1 && currentTarget is { } current)
            targets.Remove(current);

        return targets.Count > 0 ? _random.Pick(targets) : null;
    }
}

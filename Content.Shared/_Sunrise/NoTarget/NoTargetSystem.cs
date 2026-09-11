// Content.Shared/NoTarget/NoTargetSystem.cs

using System.Diagnostics.CodeAnalysis;
using Content.Shared._Sunrise.NoTarget.Components;
using Content.Shared.Stealth.Components;
using Content.Shared.Whitelist;

namespace Content.Shared._Sunrise.NoTarget;

public sealed partial class NpcNoTargetSystem : EntitySystem
{
    //[Dependency] private readonly IComponentFactory _componentFactory = null!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = null!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NpcNoTargetComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<NpcNoTargetComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStartup(Entity<NpcNoTargetComponent> ent, ref ComponentStartup args)
    {
        NpcNoTargetSetActive(ent);
    }

    private static void OnShutdown(Entity<NpcNoTargetComponent> ent, ref ComponentShutdown args)
    {
        ent.Comp.Enabled = false;
    }

    private void NpcNoTargetSetActive(EntityUid uid)
    {
        if (!TryComp(uid, out NpcNoTargetComponent? ent))
            return;

        var active = CheckActivation(uid);

        if (ent.Enabled == active)
            return;

        if (HasComp<StealthComponent>(uid))
        {
            ent.Enabled = StealthActive(uid);
            Dirty(uid, ent);
        }

        ent.Enabled = active;
        Dirty(uid, ent);
    }

    public bool NpcNoTargetActivation(EntityUid uid)
    {
        if (!CheckActivation(uid))
        {
            NpcNoTargetSetActive(uid);
            return false;
        }

        NpcNoTargetSetActive(uid);
        return true;
    }

    public bool HasCompNpcNoTarget<T>([NotNullWhen(true)] EntityUid? uid, Entity<NpcNoTargetComponent> ent)
        where T : IComponent, new()
    {
        var whitelist = ent.Comp.Whitelist;
        //var compName = _componentFactory.GetComponentName<T>();

        if (_whitelist.IsWhitelistFail(whitelist, ent.Owner))
            return false;
        return HasComp<T>(uid) && CheckActivation(ent);
    }

    private bool CheckActivation(EntityUid uid)
    {
        if (!TryComp(uid, out NpcNoTargetComponent? comp) || comp.Whitelist == null)
            return false;

        return !_whitelist.IsWhitelistFail(comp.Whitelist, uid);
        // return comp.RequireAll
        //     ? comp.Whitelist.Any(component => !HasComp(uid, component.GetType()))
        //     : comp.Whitelist.Values.Any(component => HasComp(uid, component.GetType()));
    }
}

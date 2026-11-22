// © SUNRISE, An EULA/CLA with a hosting restriction, full text: https://github.com/makura-games/sunrise-station/blob/master/CLA.txt;

using Content.Server.DoAfter;
using Content.Shared._Sunrise.Humanoid;
using Content.Shared._Sunrise.Razor;
using Content.Shared.Buckle.Components;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Robust.Shared.Audio.Systems;

namespace Content.Server._Sunrise.Razor;

public sealed partial class RazorSystem : SharedRazorSystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private DoAfterSystem _doAfterSystem = default!;
    [Dependency] private SunriseHumanoidMarkingSystem _sunriseMarking = default!;

    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<RazorComponent>(RazorUiKey.Key,
            subs =>
        {
            subs.Event<BoundUIClosedEvent>(OnUiClosed);
            subs.Event<RazorSelectMessage>(OnRazorSelect);
            subs.Event<RazorAddSlotMessage>(OnTryRazorAddSlot);
            subs.Event<RazorRemoveSlotMessage>(OnTryRazorRemoveSlot);
        });


        SubscribeLocalEvent<RazorComponent, RazorSelectDoAfterEvent>(OnSelectSlotDoAfter);
        SubscribeLocalEvent<RazorComponent, RazorRemoveSlotDoAfterEvent>(OnRemoveSlotDoAfter);
        SubscribeLocalEvent<RazorComponent, RazorAddSlotDoAfterEvent>(OnAddSlotDoAfter);
    }

    private void OnRazorSelect(EntityUid uid, RazorComponent component, RazorSelectMessage message)
    {
        if (component.Target is not { } target)
            return;

        _doAfterSystem.Cancel(component.DoAfter);
        component.DoAfter = null;

        var doAfter = new RazorSelectDoAfterEvent()
        {
            Category = message.Category,
            Slot = message.Slot,
            Marking = message.Marking,
        };

        var time = component.AddSlotTime;
        if (TryComp<BuckleComponent>(component.Target, out var buckleComponent))
        {
            if (buckleComponent.BuckledTo != null)
            {
                var proto = Prototype(buckleComponent.BuckledTo.Value);
                if (proto is { ID: "ChairBarber" })
                    time *= 0.5f;
            }
        }

        _doAfterSystem.TryStartDoAfter(new DoAfterArgs(EntityManager, message.Actor, time, doAfter, uid, target: target, used: uid)
        {
            DistanceThreshold = SharedInteractionSystem.InteractionRange,
            BreakOnDamage = true,
            BreakOnMove = true,
            BreakOnHandChange = false,
            NeedHand = true
        },
            out var doAfterId);

        component.DoAfter = doAfterId;
        _audio.PlayPvs(component.ChangeHairSound, uid);
    }

    private void OnSelectSlotDoAfter(EntityUid uid, RazorComponent component, RazorSelectDoAfterEvent args)
    {
        if (args.Handled || args.Target == null || args.Cancelled)
            return;

        if (component.Target != args.Target)
            return;

        var layer = LayerFromCategory(args.Category);
        _sunriseMarking.SetMarkingId(component.Target.Value, layer, args.Slot, args.Marking);

        UpdateInterface(uid, component.Target.Value, component);
    }

    private void OnTryRazorRemoveSlot(EntityUid uid, RazorComponent component, RazorRemoveSlotMessage message)
    {
        if (component.Target is not { } target)
            return;

        _doAfterSystem.Cancel(component.DoAfter);
        component.DoAfter = null;

        var doAfter = new RazorRemoveSlotDoAfterEvent()
        {
            Category = message.Category,
            Slot = message.Slot,
        };

        var time = component.AddSlotTime;

        Log.Debug($"id: {component.Target}");
        if (TryComp<BuckleComponent>(component.Target, out var buckleComponent))
        {
            if (buckleComponent.BuckledTo != null)
            {
                var proto = Prototype(buckleComponent.BuckledTo.Value);
                if (proto is { ID: "ChairBarber" })
                    time *= 0.5f;
            }
        }

        _doAfterSystem.TryStartDoAfter(new DoAfterArgs(EntityManager, message.Actor, time, doAfter, uid, target: target, used: uid)
        {
            DistanceThreshold = SharedInteractionSystem.InteractionRange,
            BreakOnDamage = true,
            BreakOnHandChange = false,
            NeedHand = true
        },
            out var doAfterId);

        component.DoAfter = doAfterId;
        _audio.PlayPvs(component.ChangeHairSound, uid);
    }

    private void OnRemoveSlotDoAfter(EntityUid uid, RazorComponent component, RazorRemoveSlotDoAfterEvent args)
    {
        if (args.Handled || args.Target == null || args.Cancelled)
            return;

        if (component.Target != args.Target)
            return;

        var layer = LayerFromCategory(args.Category);
        _sunriseMarking.RemoveMarking(component.Target.Value, layer, args.Slot);

        UpdateInterface(uid, component.Target.Value, component);
    }

    private void OnTryRazorAddSlot(EntityUid uid, RazorComponent component, RazorAddSlotMessage message)
    {
        if (component.Target == null)
            return;

        _doAfterSystem.Cancel(component.DoAfter);
        component.DoAfter = null;

        var doAfter = new RazorAddSlotDoAfterEvent()
        {
            Category = message.Category,
        };

        var time = component.AddSlotTime;

        Log.Debug($"id: {component.Target}");
        if (TryComp<BuckleComponent>(component.Target, out var buckleComponent))
        {
            if (buckleComponent.BuckledTo != null)
            {
                var proto = Prototype(buckleComponent.BuckledTo.Value);
                if (proto is { ID: "ChairBarber" })
                    time *= 0.5f;
            }
        }

        _doAfterSystem.TryStartDoAfter(new DoAfterArgs(EntityManager, message.Actor, time, doAfter, uid, target: component.Target.Value, used: uid)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            BreakOnHandChange = false,
            NeedHand = true
        },
            out var doAfterId);

        component.DoAfter = doAfterId;
        _audio.PlayPvs(component.ChangeHairSound, uid);
    }

    private void OnAddSlotDoAfter(EntityUid uid, RazorComponent component, RazorAddSlotDoAfterEvent args)
    {
        if (args.Handled || args.Target == null || args.Cancelled)
            return;

        if (component.Target != args.Target)
            return;

        var layer = LayerFromCategory(args.Category);
        _sunriseMarking.AddFirstAvailableMarking(component.Target.Value, layer, Color.Black);

        UpdateInterface(uid, component.Target.Value, component);
    }

    private void OnUiClosed(Entity<RazorComponent> ent, ref BoundUIClosedEvent args)
    {
        ent.Comp.Target = null;
        Dirty(ent);
    }
}

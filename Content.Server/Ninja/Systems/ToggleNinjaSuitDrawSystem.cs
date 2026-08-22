using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Ninja.Components;
using Content.Shared.Ninja.Systems;

namespace Content.Server.Ninja.Systems;

/// <summary>
/// Handles events to integrate NinjaSuitDraw with ItemToggle
/// </summary>
public sealed partial class ToggleNinjaSuitDrawSystem : EntitySystem
{
    [Dependency] private ItemToggleSystem _toggle = default!;
    [Dependency] private NinjaSuitDrawSystem _suitDraw = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ToggleNinjaSuitDrawComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ToggleNinjaSuitDrawComponent, ItemToggleActivateAttemptEvent>(OnActivateAttempt);
        SubscribeLocalEvent<ToggleNinjaSuitDrawComponent, ItemToggledEvent>(OnToggled);
    }

    private void OnMapInit(Entity<ToggleNinjaSuitDrawComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp<NinjaSuitDrawComponent>(ent, out var draw))
        {
            // Если тут будет стоять еррор, то AllComponentsToOneDeleteTest насрет, что все плохо.
            Log.Warning($"Found entity {ToPrettyString(ent)} with {nameof(ToggleNinjaSuitDrawComponent)} but without {nameof(NinjaSuitDrawComponent)}! Toggle component will be removed");
            RemComp<ToggleNinjaSuitDrawComponent>(ent);

            return;
        }

        _suitDraw.SetEnabled((ent, draw), _toggle.IsActivated(ent.Owner));
    }

    private void OnActivateAttempt(Entity<ToggleNinjaSuitDrawComponent> ent, ref ItemToggleActivateAttemptEvent args)
    {
        if (!_suitDraw.CanDrawPower((ent.Owner, Comp<NinjaSuitDrawComponent>(ent.Owner)))
            || !_suitDraw.CanUse((ent.Owner, Comp<NinjaSuitDrawComponent>(ent.Owner))))
            args.Cancelled = true;
    }

    private void OnToggled(Entity<ToggleNinjaSuitDrawComponent> ent, ref ItemToggledEvent args)
    {
        var uid = ent.Owner;
        var draw = Comp<NinjaSuitDrawComponent>(uid);
        _suitDraw.SetEnabled((uid, draw), args.Activated);
    }
}

/// <summary>
/// Component that integrates NinjaSuitDraw with ItemToggle functionality.
/// </summary>
[RegisterComponent]
public sealed partial class ToggleNinjaSuitDrawComponent : Component
{
}

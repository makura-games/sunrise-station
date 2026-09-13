using Content.Shared.Actions;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DoAfter;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Content.Shared.Strip;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry;
using Content.Shared.Administration.Logs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;
using Content.Shared._Sunrise.HardsuitInjection.Components;
using Content.Shared.Clothing.EntitySystems;

namespace Content.Shared._Sunrise.HardsuitInjection.EntitySystems;

public sealed partial class InjectSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _containerSystem = default!;
    [Dependency] private SharedActionsSystem _actionsSystem = default!;
    [Dependency] private InventorySystem _inventorySystem = default!;
    [Dependency] private SharedPopupSystem _popupSystem = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedStrippableSystem _strippable = default!;
    [Dependency] private IEntityManager _entManager = default!;
    [Dependency] private INetManager _netManager = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private ReactiveSystem _reactiveSystem = default!;
    [Dependency] private ISharedAdminLogManager _sharedAdminLogSystem = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ItemSlotsSystem _itemSlotsSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<InjectComponent, UpdateECEvent>(OnUpdateEC);

        InitializeBaseEvents();
        InitializeActionEvents();
        InitializeDoAfterEvents();
    }

    #region Own Events

    private void OnUpdateEC(EntityUid uid, InjectComponent component, UpdateECEvent args)
    {
        var beakerUid = GetEntity(args.BeakerUid);

        if (!_solutions.TryGetSolution(beakerUid, "beaker", out var solutionEntity, out _)) return;

        var removedSolution = _solutions.SplitSolution(solutionEntity.Value, args.ReagentTransfer.Value);
        args.RemovedReagentAmount = removedSolution;
    }

    #endregion
}

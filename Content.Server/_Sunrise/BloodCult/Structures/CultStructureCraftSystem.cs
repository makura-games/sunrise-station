using Content.Shared._Sunrise.BloodCult.Components;
using Content.Shared._Sunrise.BloodCult.Structures;
using Content.Shared.Interaction.Events;
using Robust.Server.GameObjects;
using Robust.Server.Player;

namespace Content.Server._Sunrise.BloodCult.Structures;

public sealed partial class CultStructureCraftSystem : EntitySystem
{
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private UserInterfaceSystem _uiSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RunicMetalComponent, UseInHandEvent>(OnUseInHand);
    }

    private void OnUseInHand(EntityUid uid, RunicMetalComponent component, UseInHandEvent args)
    {
        if (!HasComp<BloodCultistComponent>(args.User))
            return;

        if (!_playerManager.TryGetSessionByEntity(args.User, out var session) || session is not { } playerSession)
            return;

        _uiSystem.TryToggleUi(uid, CultStructureCraftUiKey.Key, playerSession);
    }
}

using Content.Server._Sunrise.BloodCult.Runes.Comps;
using Content.Server._Sunrise.BloodCult.Runes.Systems;
using Content.Server.EUI;
using Content.Server.Popups;
using Content.Shared._Sunrise.BloodCult.Actions;
using Content.Shared._Sunrise.BloodCult.Components;
using Content.Shared._Sunrise.BloodCult.UI;
using Content.Shared.Eui;
using Content.Shared.Popups;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Sunrise.BloodCult.UI;

public sealed partial class TeleportSpellEui : BaseEui
{
    [Dependency] private EntityManager _entityManager = default!;


    public readonly EntProtoId TeleportInEffect = "CultTeleportInEffect";
    public readonly EntProtoId TeleportOutEffect = "CultTeleportOutEffect";
    private readonly SoundPathSpecifier _teleportInSound = new("/Audio/_Sunrise/BloodCult/veilin.ogg");
    private readonly SoundPathSpecifier _teleportOutSound = new("/Audio/_Sunrise/BloodCult/veilout.ogg");
    private EntityUid _performer;
    private PopupSystem _popupSystem;
    private EntityUid _target;
    private SharedTransformSystem _transformSystem;
    private SharedAudioSystem _audio;
    private BloodCultSystem _bloodCult;

    private bool _used;


    public TeleportSpellEui(EntityUid performer, EntityUid target)
    {
        IoCManager.InjectDependencies(this);

        _transformSystem = _entityManager.System<SharedTransformSystem>();
        _audio = _entityManager.System<SharedAudioSystem>();
        _popupSystem = _entityManager.System<PopupSystem>();
        _bloodCult = _entityManager.System<BloodCultSystem>();

        _performer = performer;
        _target = target;

        Timer.Spawn(TimeSpan.FromSeconds(10), Close);
    }

    public override EuiStateBase GetNewState()
    {
        var state = new TeleportSpellEuiState();

        var query = _entityManager.EntityQueryEnumerator<CultRuneTeleportComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            state.Runes.Add((int)uid, comp.Label!);
            state.Distance.Add(_bloodCult.GetDistance(_performer, xform));
        }

        return state;
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (_used)
            return;

        if (msg is not TeleportSpellTargetRuneSelected cast)
        {
            return;
        }

        if (!_entityManager.TryGetComponent<BloodCultistComponent>(_performer, out var cultistComponent))
        {
            return;
        }

        var performerPosition = _entityManager.GetComponent<TransformComponent>(_performer).Coordinates;
        var targetPosition = _entityManager.GetComponent<TransformComponent>(_target).Coordinates;
        ;

        performerPosition.TryDistance(_entityManager, targetPosition, out var distance);

        if (distance > 1.5f)
        {
            _popupSystem.PopupEntity("Too far", _performer, PopupType.Medium);
            return;
        }

        TransformComponent? runeTransform = null!;
        TransformComponent? targetTransform = null!;

        foreach (var runeComponent in _entityManager.EntityQuery<CultRuneTeleportComponent>())
        {
            if (runeComponent.Owner == new EntityUid(cast.RuneUid))
            {
                runeTransform = _entityManager.GetComponent<TransformComponent>(runeComponent.Owner);
            }
        }

        if (_entityManager.TryGetComponent<TransformComponent>(_target, out var targetm))
        {
            targetTransform = targetm;
        }

        if (runeTransform is null)
        {
            _popupSystem.PopupEntity("Rune is gone", _performer);
            DoStateUpdate();
            return;
        }

        _used = true;

        _entityManager.SpawnEntity(TeleportInEffect, runeTransform.Coordinates);
        _entityManager.SpawnEntity(TeleportOutEffect, targetTransform.Coordinates);
        _audio.PlayPvs(_teleportInSound, runeTransform.Coordinates);
        _audio.PlayPvs(_teleportOutSound, targetTransform.Coordinates);
        _transformSystem.SetCoordinates(_target, runeTransform.Coordinates);
        var ev = new TeleportSpellUsedEvent();
        _entityManager.EventBus.RaiseLocalEvent(_performer, ev);
        Close();
    }
}

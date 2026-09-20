using System.Numerics;
using Content.Shared._Sunrise.Antags.Vampires.Components;
using Robust.Client.GameObjects;

namespace Content.Client._Sunrise.Antags.Vampires;

/// <summary>
/// Клиентская система плавной визуализации лучей вампира
/// </summary>
public sealed partial class VampireDrainBeamSystem : EntitySystem
{
    private enum BeamKind
    {
        Drain,
    }

    [Dependency] private TransformSystem _transform = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    private EntityQuery<VampireBeamVisualComponent> _beamVisualQuery;

    /// <summary>
    /// Отслеживает клиентские визуальные сущности лучей
    /// Ключ = пара (kind, source, target), значение = визуальная сущность луча
    /// </summary>
    private readonly Dictionary<(BeamKind, EntityUid, EntityUid), EntityUid> _activeBeamVisuals = [];

    public override void Initialize()
    {
        base.Initialize();
        _beamVisualQuery = GetEntityQuery<VampireBeamVisualComponent>();
        SubscribeNetworkEvent<VampireDrainBeamEvent>(OnDrainBeamEvent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Обновляем визуалы всех активных лучей каждый кадр для плавного следования
        var toRemove = new List<(BeamKind, EntityUid, EntityUid)>();

        foreach (var ((kind, source, target), beamEntity) in _activeBeamVisuals)
        {
            // Проверка, существуют ли ещё сущности
            if (!Exists(source) || !Exists(target) || !Exists(beamEntity))
            {
                toRemove.Add((kind, source, target));
                if (Exists(beamEntity))
                    QueueDel(beamEntity);
                continue;
            }

            UpdateBeamVisual(beamEntity, source, target);
        }

        foreach (var key in toRemove)
        {
            _activeBeamVisuals.Remove(key);
        }
    }

    private void OnDrainBeamEvent(VampireDrainBeamEvent ev)
        => HandleBeamEvent(ev.Source, ev.Target, ev.Create, BeamKind.Drain, ev.VisualPrototype);

    private void HandleBeamEvent(NetEntity sourceNet, NetEntity targetNet, bool create, BeamKind kind, string prototype)
    {
        var source = GetEntity(sourceNet);
        var target = GetEntity(targetNet);

        if (!Exists(source) || !Exists(target))
            return;

        var key = (kind, source, target);

        if (create)
        {
            CreateBeamVisual(kind, prototype, source, target);
            return;
        }

        if (_activeBeamVisuals.TryGetValue(key, out var beamEntity))
        {
            QueueDel(beamEntity);
            _activeBeamVisuals.Remove(key);
        }
    }

    private void CreateBeamVisual(BeamKind kind, string prototype, EntityUid source, EntityUid target)
    {
        var key = (kind, source, target);

        // Удаляем существующий луч, если он есть
        if (_activeBeamVisuals.TryGetValue(key, out var existingBeam))
        {
            QueueDel(existingBeam);
        }

        var beam = Spawn(prototype, Transform(source).Coordinates);

        _activeBeamVisuals[key] = beam;

        UpdateBeamVisual(beam, source, target);
    }

    private void UpdateBeamVisual(EntityUid beam, EntityUid source, EntityUid target)
    {
        if (!TryComp<SpriteComponent>(beam, out var sprite)
            || !_beamVisualQuery.TryComp(beam, out var beamVisual))
            return;

        var sourcePos = _transform.GetWorldPosition(source);
        var targetPos = _transform.GetWorldPosition(target);

        var direction = targetPos - sourcePos;
        var distance = direction.Length();

        if (distance < beamVisual.MinDistance)
            return;

        var worldAngle = direction.ToWorldAngle() + beamVisual.AngleOffset;

        var midpoint = sourcePos + (direction * 0.5f);
        _transform.SetWorldPosition(beam, midpoint);

        _transform.SetWorldRotation(beam, worldAngle);
        _sprite.SetRotation((beam, sprite), Angle.Zero);

        // Масштабируем луч под расстояние. Вертикальный ? масштабируем Y : масштабируем X
        var length = MathF.Max(beamVisual.MinLength, distance);
        var scale = beamVisual.SpriteIsVertical
            ? new Vector2(beamVisual.Thickness, length)
            : new Vector2(length, beamVisual.Thickness);
        _sprite.SetScale((beam, sprite), scale);
        _sprite.SetOffset((beam, sprite), Vector2.Zero);
    }

    public override void Shutdown()
    {
        // Очищаем все визуалы лучей
        foreach (var beamEntity in _activeBeamVisuals.Values)
        {
            if (Exists(beamEntity))
                QueueDel(beamEntity);
        }
        _activeBeamVisuals.Clear();

        base.Shutdown();
    }
}

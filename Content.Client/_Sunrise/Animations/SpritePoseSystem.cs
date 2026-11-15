using Content.Client._Sunrise.Animations.SpriteAnimation.Systems;
using Content.Shared.Rotation;
using Robust.Client.GameObjects;

namespace Content.Client._Sunrise.Animations;

/// <summary>
/// Переходы между базовой и временной позой без захвата вращения от других анимаций
/// </summary>
public sealed partial class SpritePoseSystem : EntitySystem
{
    [Dependency] private SpriteAnimationSystem _spriteAnimation = default!;

    public const string AnimationKey = "sprite-pose";

    public override void Initialize()
    {
        UpdatesAfter.Add(typeof(AppearanceSystem));
        UpdatesBefore.Add(typeof(SpriteAnimationSystem));
        base.Initialize();
        SubscribeLocalEvent<RotationVisualsComponent, ComponentRemove>(OnRotationRemove);
        SubscribeLocalEvent<SpritePoseComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnRotationRemove(Entity<RotationVisualsComponent> ent, ref ComponentRemove args)
    {
        if (!TerminatingOrDeleted(ent))
            RemComp<SpritePoseComponent>(ent);
    }

    private void OnShutdown(Entity<SpritePoseComponent> ent, ref ComponentShutdown args)
    {
        _spriteAnimation.Stop(ent.Owner, AnimationKey);
        if (ent.Comp.HasSavedDirection && TryComp<SpriteComponent>(ent, out var sprite))
        {
            sprite.EnableDirectionOverride = ent.Comp.SavedEnableDirectionOverride;
            sprite.DirectionOverride = ent.Comp.SavedDirectionOverride;
        }
    }

    /// <summary>
    /// Сохраняет базовую позу и применяет её, если нет временной позы
    /// duration задаётся в секундах, неположительное значение применяет позу сразу
    /// </summary>
    public void SetBasePose(Entity<SpriteComponent> ent, Angle rotation, float duration)
    {
        var pose = EnsureComp<SpritePoseComponent>(ent);
        pose.BaseRotation = rotation;

        if (!pose.OverrideRotation.HasValue)
            Animate(ent, rotation, duration, pose);
    }

    /// <summary>
    /// Применяет временную позу, сохраняя базовую для последующего восстановления
    /// duration задаётся в секундах, неизменная цель не прерывает текущий переход
    /// </summary>
    public void SetOverride(Entity<SpriteComponent> ent, Angle rotation, float duration, Direction? direction = null)
    {
        var pose = EnsureComp<SpritePoseComponent>(ent);
        pose.OverrideRotation = rotation;
        pose.Direction = direction;
        ApplyDirection(ent, pose);
        Animate(ent, rotation, duration, pose);
    }

    /// <summary>
    /// Снимает временную позу и возвращает базовую за duration секунд
    /// Без временной позы ничего не меняет, неположительное время восстанавливает базу сразу
    /// </summary>
    public void ClearOverride(Entity<SpriteComponent> ent, float duration)
    {
        if (!TryComp<SpritePoseComponent>(ent, out var pose) || !pose.OverrideRotation.HasValue)
            return;

        pose.OverrideRotation = null;
        pose.Direction = null;
        ApplyDirection(ent, pose);
        Animate(ent, pose.BaseRotation, duration, pose);
    }

    /// <summary>
    /// Приостанавливает применение позы до Resume, сохраняя её цель
    /// </summary>
    public void Suspend(Entity<SpriteComponent> ent, Direction? direction = null)
    {
        if (!TryComp<SpritePoseComponent>(ent, out var pose))
        {
            pose = AddComp<SpritePoseComponent>(ent);
            pose.BaseRotation = _spriteAnimation.GetBaseRotation(ent.AsNullable());
        }
        pose.Suspended = true;
        pose.SuspendedDirection = direction;
        pose.HasPose = false;
        ApplyDirection(ent, pose);
        _spriteAnimation.Stop(ent.Owner, AnimationKey);
    }

    /// <summary>
    /// Снимает приостановку и сразу применяет актуальную позу
    /// </summary>
    public void Resume(Entity<SpriteComponent> ent)
    {
        if (!TryComp<SpritePoseComponent>(ent, out var pose))
            return;

        pose.Suspended = false;
        pose.SuspendedDirection = null;
        ApplyDirection(ent, pose);
        Animate(ent, pose.OverrideRotation ?? pose.BaseRotation, 0f, pose);
    }

    private static void ApplyDirection(Entity<SpriteComponent> ent, SpritePoseComponent pose)
    {
        var direction = pose.Suspended ? pose.SuspendedDirection : pose.Direction;
        if (direction.HasValue)
        {
            if (!pose.HasSavedDirection)
            {
                pose.SavedEnableDirectionOverride = ent.Comp.EnableDirectionOverride;
                pose.SavedDirectionOverride = ent.Comp.DirectionOverride;
                pose.HasSavedDirection = true;
            }

            ent.Comp.EnableDirectionOverride = true;
            ent.Comp.DirectionOverride = direction.Value;
        }
        else if (pose.HasSavedDirection)
        {
            ent.Comp.EnableDirectionOverride = pose.SavedEnableDirectionOverride;
            ent.Comp.DirectionOverride = pose.SavedDirectionOverride;
            pose.HasSavedDirection = false;
        }
    }

    private void Animate(Entity<SpriteComponent> ent, Angle rotation, float duration, SpritePoseComponent pose)
    {
        if (pose.Suspended)
            return;

        var baseRotation = _spriteAnimation.GetBaseRotation(ent.AsNullable());
        if (pose.HasPose && pose.Rotation.Equals(rotation) && baseRotation.Equals(rotation))
            return;

        var from = baseRotation + _spriteAnimation.GetRotationOffset(ent.Owner, AnimationKey);
        _spriteAnimation.Stop(ent.Owner, AnimationKey);
        _spriteAnimation.SetBaseRotation(ent.AsNullable(), rotation);
        if (duration > 0f)
            _spriteAnimation.PlayRotation(ent.Owner, AnimationKey, (from - rotation, 0f), (Angle.Zero, duration));

        pose.Rotation = rotation;
        pose.HasPose = true;
    }
}

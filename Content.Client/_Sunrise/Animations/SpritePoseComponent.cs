namespace Content.Client._Sunrise.Animations;

[RegisterComponent, Access(typeof(SpritePoseSystem))]
public sealed partial class SpritePoseComponent : Component
{
    /// <summary>
    /// Была ли целевая поза применена после последнего сброса при PVS или переноске
    /// </summary>
    public bool HasPose;

    /// <summary>
    /// Поза временно управляется внешней системой
    /// </summary>
    public bool Suspended;

    /// <summary>
    /// Направление временной позы
    /// </summary>
    public Direction? Direction;

    /// <summary>
    /// Направление внешней системы на время приостановки позы
    /// </summary>
    public Direction? SuspendedDirection;

    /// <summary>
    /// Исходные флаги спрайта до первого переопределения направления
    /// </summary>
    public bool HasSavedDirection;

    /// <summary>
    /// Был ли override направления включён до применения позы
    /// </summary>
    public bool SavedEnableDirectionOverride;

    /// <summary>
    /// Направление до применения позы
    /// </summary>
    public Direction SavedDirectionOverride;

    /// <summary>
    /// Последний применённый целевой угол без временных вкладов анимаций
    /// </summary>
    public Angle Rotation;

    /// <summary>
    /// Поза, используемая без временного переопределения
    /// </summary>
    public Angle BaseRotation;

    /// <summary>
    /// Временная поза, имеющая приоритет над BaseRotation до сброса
    /// </summary>
    public Angle? OverrideRotation;
}

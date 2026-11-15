using System.Numerics;

namespace Content.Client._Sunrise.Animations.SpriteAnimation;

/// <summary>
/// Базовые значения спрайта и независимые вклады анимаций одной сущности
/// </summary>
[RegisterComponent, Access(typeof(Systems.SpriteAnimationSystem))]
public sealed partial class SpriteAnimationComponent : Component
{
    /// <summary>
    /// Добавочные смещения по ключам анимаций
    /// </summary>
    [ViewVariables]
    internal readonly Dictionary<string, SpriteAnimationTrack<Vector2>> Offsets = new();

    /// <summary>
    /// Множители масштаба по ключам анимаций
    /// </summary>
    [ViewVariables]
    internal readonly Dictionary<string, SpriteAnimationTrack<Vector2>> Scales = new();

    /// <summary>
    /// Добавочные углы по ключам анимаций
    /// </summary>
    [ViewVariables]
    internal readonly Dictionary<string, SpriteAnimationTrack<float>> Rotations = new();

    /// <summary>
    /// Фабрики циклов, сохраняющиеся при выходе из PVS
    /// </summary>
    [ViewVariables]
    internal readonly Dictionary<string, SpriteAnimationLoop> Loops = new();

    /// <summary>
    /// Временный список ключей для удаления после обхода канала
    /// </summary>
    [ViewVariables]
    internal readonly List<string> Done = new();

    /// <summary>
    /// Базовое смещение без вкладов анимаций
    /// </summary>
    [ViewVariables]
    public Vector2 Offset;

    /// <summary>
    /// Последнее смещение, записанное контроллером в спрайт
    /// </summary>
    [ViewVariables]
    public Vector2 LastOffset;

    /// <summary>
    /// Базовый масштаб без множителей анимаций
    /// </summary>
    [ViewVariables]
    public Vector2 Scale = Vector2.One;

    /// <summary>
    /// Последний масштаб, записанный контроллером в спрайт
    /// </summary>
    [ViewVariables]
    public Vector2 LastScale = Vector2.One;

    /// <summary>
    /// Сумма текущих вкладов смещения
    /// </summary>
    [ViewVariables]
    public Vector2 OffsetContribution;

    /// <summary>
    /// Произведение текущих множителей масштаба
    /// </summary>
    [ViewVariables]
    public Vector2 ScaleContribution = Vector2.One;

    /// <summary>
    /// Базовый угол без вкладов анимаций
    /// </summary>
    [ViewVariables]
    public Angle Rotation;

    /// <summary>
    /// Последний угол, записанный контроллером в спрайт
    /// </summary>
    [ViewVariables]
    public Angle LastRotation;

    /// <summary>
    /// Сумма текущих вкладов угла
    /// </summary>
    [ViewVariables]
    public Angle RotationContribution;

    /// <summary>
    /// Канал смещения хранит базу, которую нужно пересобрать
    /// </summary>
    [ViewVariables]
    public bool HasOffset;

    /// <summary>
    /// Канал масштаба хранит базу, которую нужно пересобрать
    /// </summary>
    [ViewVariables]
    public bool HasScale;

    /// <summary>
    /// Канал угла хранит базу, которую нужно пересобрать
    /// </summary>
    [ViewVariables]
    public bool HasRotation;

}

/// <summary>
/// Фабрика следующего прохода анимации и условие сохранения цикла
/// </summary>
internal sealed record SpriteAnimationLoop(Action<EntityUid> Play, Func<EntityUid, bool>? CanPlay);

using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.Antags.Vampires.Components;

/// <summary>
/// Обрабатывает простое поведение сущностей-приманок вампира.
/// </summary>
[RegisterComponent]
public sealed partial class VampireDecoyComponent : Component
{
    /// <summary>
    /// Гарантирует, что вспышка/взрыв произойдёт только один раз.
    /// </summary>
    public bool Detonated;

    /// <summary>
    /// Прототип визуального эффекта вспышки при срабатывании приманки.
    /// </summary>
    [DataField]
    public EntProtoId FlashEffectId = "GrenadeFlashEffect";

    /// <summary>
    /// Радиус вспышки при срабатывании приманки.
    /// </summary>
    [DataField]
    public float FlashRange = 3f;

    /// <summary>
    /// Длительность вспышки при срабатывании приманки.
    /// </summary>
    [DataField]
    public TimeSpan FlashDuration = TimeSpan.FromSeconds(4);

    /// <summary>
    /// Множитель замедления целей вспышки.
    /// </summary>
    [DataField]
    public float SlowTo = 0.5f;

    /// <summary>
    /// Показывать ли всплывающее уведомление цели при срабатывании.
    /// </summary>
    [DataField]
    public bool DisplayPopup = true;

    /// <summary>
    /// Вероятность срабатывания вспышки (0.0 - 1.0).
    /// </summary>
    [DataField]
    public float Probability = 1f;

    /// <summary>
    /// Звук вспышки при срабатывании приманки.
    /// </summary>
    [DataField]
    public SoundSpecifier FlashSound = new SoundPathSpecifier("/Audio/Weapons/flash.ogg");
}

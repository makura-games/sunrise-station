using Content.Shared.FixedPoint;
using Robust.Shared.Audio;

namespace Content.Shared._Sunrise.Antags.Vampires.Components;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class VampireSunlightComponent : Component
{
    /// <summary>
    ///     Тепловой урон при срабатывании горения
    /// </summary>
    [DataField]
    public FixedPoint2 BurnDamage = FixedPoint2.New(3);

    /// <summary>
    ///     Интервал между тиками экспозиции в космосе
    /// </summary>
    [DataField]
    public TimeSpan DamageInterval = TimeSpan.FromSeconds(2f);

    /// <summary>
    ///     Стоимость крови за тик экспозиции, пока у вампира есть запасы
    /// </summary>
    [DataField]
    public int BloodDrainPerInterval = 10;

    /// <summary>
    ///     Шанс поджечь цель, пока у вампира есть кровь
    /// </summary>
    [DataField]
    public float BloodEffectChance = 0.1f;

    /// <summary>
    ///     Шанс поджечь цель, когда у вампира нет крови
    /// </summary>
    [DataField]
    public float BloodlessEffectChance = 0.85f;

    /// <summary>
    ///     Слои горения при возгорании вампира
    /// </summary>
    [DataField]
    public float FireStacksOnIgnite = 2f;

    /// <summary>
    ///     Генетический урон за тик, когда у вампира нет крови
    /// </summary>
    [DataField]
    public FixedPoint2 GeneticDamagePerInterval = FixedPoint2.New(10);

    /// <summary>
    ///     Порог накопленного генетического урона, после которого вампир обращается в пепел
    /// </summary>
    [DataField]
    public FixedPoint2 GeneticDustThreshold = FixedPoint2.New(100);

    /// <summary>
    ///     Как долго вампир может находиться в космосе до начала урона
    /// </summary>
    [DataField]
    public TimeSpan GracePeriod = TimeSpan.FromSeconds(1.5f);

    /// <summary>
    ///     Минимальные секунды между всплывающими предупреждениями игроку
    /// </summary>
    [DataField]
    public TimeSpan WarningPopupCooldown = TimeSpan.FromSeconds(5f);

    /// <summary>
    ///     Ключ локализации, показываемый при возгорании вампира
    /// </summary>
    [DataField]
    public LocId WarningPopup = "vampire-space-burn-warning";

    /// <summary>
    /// Время входа вампира в космос (null — вне космоса).
    /// </summary>
    [ViewVariables]
    [AutoPausedField]
    public TimeSpan? TimeEnteredSpace;

    /// <summary>
    /// Время следующего тика урона от космоса.
    /// </summary>
    [ViewVariables]
    [AutoPausedField]
    public TimeSpan? NextDamageTime;

    /// <summary>
    /// Время следующего предупреждения о горении в космосе.
    /// </summary>
    [ViewVariables]
    [AutoPausedField]
    public TimeSpan NextWarningPopup;

    /// <summary>
    /// Звук горения вампира в космосе.
    /// </summary>
    [DataField]
    public SoundSpecifier SpaceBurnSound = new SoundPathSpecifier("/Audio/Effects/lightburn.ogg");
}

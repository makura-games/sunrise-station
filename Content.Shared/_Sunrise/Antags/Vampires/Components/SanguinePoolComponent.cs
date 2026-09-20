using Robust.Shared.Audio;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Antags.Vampires.Components;

/// <summary>
///     Маркер на форме-полиморфе, созданной Кровавой лужей.
///     Обрабатывает фильтрацию коллизий на клиенте и сервере и предоставляет
///     настраиваемые параметры, используемые пока активна форма (спавн следа и т.п.).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SanguinePoolComponent : Component
{
    /// <summary>
    ///     Прототип, спавнящийся при входе лужи на новый тайл
    /// </summary>
    [DataField]
    public EntProtoId? TrailPrototype = "PuddleBlood";

    /// <summary>
    /// Прототип эффекта при выходе из формы Кровавой лужи.
    /// </summary>
    [DataField]
    public EntProtoId ExitEffectPrototype = "VampireSanguinePoolIn";

    /// <summary>
    /// Звук выхода из формы Кровавой лужи.
    /// </summary>
    [DataField]
    public SoundSpecifier ExitSound = new SoundPathSpecifier("/Audio/_Sunrise/Effects/vampire/exit_blood.ogg");

    /// <summary>
    /// Реагент, добавляемый в лужу-след при движении.
    /// </summary>
    [DataField]
    public ProtoId<ReagentPrototype> TrailReagent = "Blood";

    /// <summary>
    /// Количество реагента, добавляемого в лужу-след.
    /// </summary>
    [DataField]
    public FixedPoint2 TrailReagentQuantity = FixedPoint2.New(30);

    /// <summary>
    /// Последний тайл, на котором оставлен след (для защиты от дублей).
    /// </summary>
    [ViewVariables]
    public (EntityUid Grid, Vector2i Tile)? LastTrail;
}

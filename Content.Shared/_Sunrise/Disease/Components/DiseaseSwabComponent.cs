using Robust.Shared.GameStates;

namespace Content.Shared._Sunrise.Disease.Components;

/// <summary>
/// Одноразовый мазок: снимает образец с существа или поверхности предмета.
/// </summary>
/// <remarks>
/// Взятие мазка предсказывается на клиенте, поэтому сам факт использования синхронизируется.
/// Что именно попало на мазок, хранит только сервер: иначе мазок выдавал бы заражённость на глаз.
/// </remarks>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DiseaseSwabComponent : Component
{
    /// <summary>
    /// Время взятия образца.
    /// </summary>
    [DataField]
    public TimeSpan SwabDelay = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Взят ли уже образец этим мазком.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Used;
}

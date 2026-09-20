using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Sunrise.Antags.Vampires.Components;

/// <summary>
/// Компонент для сущностей, испытывающих истерическое зрение.
/// Они видят других гуманоидов как случайных монстров
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HysteriaVisionComponent : Component
{
    /// <summary>
    /// Время окончания эффекта истерического зрения.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan EndTime;

    /// <summary>
    /// Вампир, применивший этот эффект
    /// </summary>
    [AutoNetworkedField]
    public EntityUid Source;

    /// <summary>
    /// Визуальные маскировки, показываемые этому клиенту при активном истерическом зрении.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<HysteriaDisguiseSprite> DisguiseSprites = new();
}

/// <summary>
/// Определяет спрайт маскировки для истерического зрения.
/// </summary>
[DataRecord, Serializable, NetSerializable]
public partial record struct HysteriaDisguiseSprite(string Path, string State, Vector2 Size);

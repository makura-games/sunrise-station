using Content.Shared.Inventory;

namespace Content.Shared._Sunrise.Disease.Components;

/// <summary>
/// Надетый предмет подсвечивает владельцу заражённые предметы.
/// </summary>
/// <remarks>
/// Живёт в shared, чтобы прототип загружался на клиенте. Что подсвечивать, решает сервер:
/// клиент получает метки на очаги своей карты и не знает ни болезней, ни больных людей.
/// Отдельной дальности у подсветки нет — её задаёт обычная видимость клиента: метку на предмет,
/// которого клиент не знает, он просто не рисует, а когда предмет входит в PVS, ореол появляется.
/// </remarks>
[RegisterComponent]
public sealed partial class DiseaseVisionComponent : Component
{
    /// <summary>
    /// В каких слотах предмет работает.
    /// </summary>
    [DataField]
    public SlotFlags Slots = SlotFlags.EYES;
}

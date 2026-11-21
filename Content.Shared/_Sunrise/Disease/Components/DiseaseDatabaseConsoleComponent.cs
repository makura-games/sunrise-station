using Content.Shared._Sunrise.Disease.Database;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Disease.Components;

/// <summary>
/// Консоль базы данных вирусов: показывает всё известное о штаммах и прогресс их исследования,
/// а также выдаёт лабораторные образцы штаммов для засева чашек Петри.
/// </summary>
/// <remarks>
/// Записи базы собирает сервер, но хранятся они в состоянии компонента: окно рисуется
/// из уже имеющихся у клиента данных, а готовность синтезатора клиент считает сам по времени.
/// Настоящие идентификаторы болезней сюда не попадают — только коды штаммов и строки локали.
/// </remarks>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class DiseaseDatabaseConsoleComponent : Component
{
    /// <summary>
    /// Прототип пробирки с лабораторным образцом.
    /// </summary>
    [DataField]
    public EntProtoId VialPrototype = "SunriseDiseaseStrainVial";

    /// <summary>
    /// Перезарядка между выдачами: не даёт завалить отдел пробирками за один клик.
    /// </summary>
    [DataField]
    public TimeSpan DispenseDelay = TimeSpan.FromSeconds(20);

    /// <summary>
    /// Когда консоль сможет выдать следующую пробирку.
    /// </summary>
    /// <remarks>
    /// Сетевое поле: по нему клиент сам разблокирует кнопку, когда перезарядка истечёт.
    /// </remarks>
    [ViewVariables, AutoNetworkedField]
    public TimeSpan NextDispense;

    /// <summary>
    /// Известные станции штаммы и прогресс их исследования.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public List<DiseaseDatabaseEntry> Entries = new();

    /// <summary>
    /// Звук выдачи пробирки: хлопок выдачи, как у торгового автомата. Принтер тут не к месту —
    /// консоль выдаёт ампулу, а не бумагу.
    /// </summary>
    [DataField]
    public SoundSpecifier? DispenseSound = new SoundPathSpecifier("/Audio/Machines/machine_vend.ogg");
}

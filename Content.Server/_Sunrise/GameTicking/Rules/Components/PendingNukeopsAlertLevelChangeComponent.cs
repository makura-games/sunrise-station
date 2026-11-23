using Content.Server.GameTicking.Rules;

namespace Content.Server._Sunrise.GameTicking.Rules.Components;

/// <summary>
/// Отмечает правило ядерных оперативников, ожидающее смены уровня угрозы.
/// </summary>
[RegisterComponent, Access(typeof(NukeopsRuleSystem))]
public sealed partial class PendingNukeopsAlertLevelChangeComponent : Component;

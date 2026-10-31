#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Shared.GameTicking.Components;

public sealed partial class GameRuleComponent
{
    /// <summary>
    /// Минимальное количество доступных должностей командования для запуска правила.
    /// </summary>
    [DataField]
    public int MinCommandStaff;
}

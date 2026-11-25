// ReSharper disable once CheckNamespace
namespace Content.Shared.Electrocution;

public sealed partial class InsulatedComponent
{
    /// <summary>
    /// Запрещает владельцу использовать оружие без увеличенной спусковой скобы.
    /// </summary>
    [DataField("preventOpperatinGuns")]
    public bool PreventOperatingGuns;
}

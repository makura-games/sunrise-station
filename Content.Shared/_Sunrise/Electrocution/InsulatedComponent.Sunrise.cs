#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Shared.Electrocution;

public sealed partial class InsulatedComponent
{
    /// <summary>
    /// Запрещает владельцу использовать оружие без увеличенной спусковой скобы.
    /// </summary>
    [DataField("preventOpperatinGuns")]
    public bool PreventOperatingGuns;
}

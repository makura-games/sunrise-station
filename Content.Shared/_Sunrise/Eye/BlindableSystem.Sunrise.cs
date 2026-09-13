using Content.Shared.Body;
using Content.Shared.Starlight.Medical.Surgery.Steps.Parts;

#pragma warning disable IDE0130 // Пространство имён не соответствует структуре папок
namespace Content.Shared.Eye.Blinding.Systems;

public sealed partial class BlindableSystem
{
    [Dependency] private BodySystem _body = default!;

    /// <summary>
    /// Проверяет отсутствие глаз только у рас, чьи органы задаются через <see cref="InitialBodyComponent"/>.
    /// У простых мобов органы глаз не моделируются отдельными сущностями.
    /// </summary>
    private bool IsMissingRequiredEyes(EntityUid uid)
    {
        return HasComp<InitialBodyComponent>(uid) &&
               HasComp<BodyComponent>(uid) &&
               !_body.TryGetOrganWithComponent<OrganEyesComponent>(uid, out _);
    }
}

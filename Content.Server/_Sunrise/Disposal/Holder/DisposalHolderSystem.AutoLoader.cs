using Content.Server.Disposal.Unit;

#pragma warning disable IDE0130 // Namespace не соответствует структуре директорий
namespace Content.Server.Disposal.Holder;

public sealed partial class DisposalHolderSystem
{
    [Dependency] private AutoLoaderSystem _autoLoader = default!;
    [Dependency] private EntityQuery<AutoLoaderComponent> _autoLoaderQuery = default!;

    private bool IsSunriseAutoLoader(EntityUid uid)
    {
        return _autoLoaderQuery.HasComp(uid);
    }

    private bool TryCycleSunriseAutoLoader(EntityUid entity, EntityUid autoLoaderUid, EntityUid currentTube)
    {
        if (!_autoLoaderQuery.TryComp(autoLoaderUid, out var autoLoader) ||
            !_container.TryGetContainer(autoLoaderUid, autoLoader.Container, out var autoLoaderContainer))
        {
            return false;
        }

        _autoLoader.Cycle(entity, (autoLoaderUid, autoLoader), autoLoaderContainer, currentTube);
        return true;
    }
}

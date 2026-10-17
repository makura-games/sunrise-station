using System.Linq;
using Content.Shared._Sunrise.Smell.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.Smell;

/// <summary>
/// Shared cache of status-scent prototypes (statusScent), built once and iterated
/// when applying condition-based scents (drugs, stimulants, alcohol).
/// Also holds the reference to the shared smellSystemConfig tuning prototype.
/// </summary>
public sealed partial class SmellPrototypeCacheSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypes = default!;

    [ValidatePrototypeId<SmellSystemConfigPrototype>]
    private const string ConfigId = "SunriseDefault";

    /// <summary>
    /// Status-effect-to-scent mapping list from YAML (statusScent).
    /// </summary>
    private List<StatusScentPrototype> _statusScentProtos = new();

    /// <summary>
    /// Shared config of temporary scent thresholds and durations (smellSystemConfig).
    /// </summary>
    private SmellSystemConfigPrototype _config = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);

        RebuildProtoCache();
    }

    /// <summary>
    /// Hot reload handler (reloadprototypes). Rebuilds the cache only when
    /// StatusScentPrototype or SmellSystemConfigPrototype types were touched,
    /// so balance edits apply without a server restart.
    /// </summary>
    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (!args.ByType.ContainsKey(typeof(StatusScentPrototype))
            && !args.ByType.ContainsKey(typeof(SmellSystemConfigPrototype)))
            return;

        RebuildProtoCache();
    }

    /// <summary>
    /// Up-to-date list of all status-effect-to-scent mappings (every statusScent prototype).
    /// Rebuilt on system initialization and on hot reload of the related prototypes,
    /// so consumers can rely on it without manual refresh.
    /// </summary>
    public IReadOnlyList<StatusScentPrototype> StatusScentProtos => _statusScentProtos;

    /// <summary>
    /// Current scent system config.
    /// </summary>
    public SmellSystemConfigPrototype Config => _config;

    /// <summary>
    /// Rebuilds the status-scent prototype cache and the system config.
    /// </summary>
    private void RebuildProtoCache()
    {
        _statusScentProtos = _prototypes.EnumeratePrototypes<StatusScentPrototype>().ToList();
        _config = _prototypes.Index<SmellSystemConfigPrototype>(ConfigId);
    }
}

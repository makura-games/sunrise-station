using Content.Client.CombatMode;
using Content.Shared.CCVar;
using Content.Shared.CombatMode;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Client._Sunrise.CombatMode;

public sealed class SunriseCombatModeIndicatorSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly CombatModeSystem _combatMode = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly IOverlayManager _overlay = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IResourceCache _resources = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly SoundSpecifier ToggleSound =
        new SoundPathSpecifier("/Audio/Machines/twobeep.ogg", AudioParams.Default.WithVolume(-4f));

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CombatModeComponent, CombatModeChangedEvent>(OnCombatModeChanged);
        Subs.CVar(_cfg, CCVars.CombatModeIndicatorsPointShow, OnShowIndicatorChanged, true);
    }

    public override void Shutdown()
    {
        _overlay.RemoveOverlay<SunriseCombatModeIndicatorOverlay>();

        base.Shutdown();
    }

    private void OnCombatModeChanged(Entity<CombatModeComponent> ent, ref CombatModeChangedEvent args)
    {
        if (!_timing.IsFirstTimePredicted || ent.Owner != _player.LocalEntity)
            return;

        _audio.PlayLocal(ToggleSound, ent, ent);
    }

    private void OnShowIndicatorChanged(bool show)
    {
        if (show)
        {
            if (_overlay.HasOverlay<SunriseCombatModeIndicatorOverlay>())
                return;

            _overlay.AddOverlay(new SunriseCombatModeIndicatorOverlay(
                EntityManager,
                _eye,
                _player,
                _combatMode,
                _resources));
            return;
        }

        if (_overlay.HasOverlay<SunriseCombatModeIndicatorOverlay>())
            _overlay.RemoveOverlay<SunriseCombatModeIndicatorOverlay>();
    }
}

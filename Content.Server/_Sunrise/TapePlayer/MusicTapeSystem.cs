using Content.Shared._Sunrise.TapePlayer;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server._Sunrise.TapePlayer;

public sealed partial class MusicTapeSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audioSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MusicTapeComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(EntityUid uid, MusicTapeComponent comp, MapInitEvent args)
    {
        var resolved = _audioSystem.ResolveSound(comp.Sound);
        var length = _audioSystem.GetAudioLength(resolved);
        comp.SongLengthSeconds = (float) length.TotalSeconds;
        Dirty(uid, comp);
    }
}

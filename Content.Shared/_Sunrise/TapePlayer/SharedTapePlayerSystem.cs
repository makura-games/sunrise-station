using Robust.Shared.Audio.Systems;

namespace Content.Shared._Sunrise.TapePlayer;

public abstract partial class SharedTapePlayerSystem : EntitySystem
{
    [Dependency] protected SharedAudioSystem Audio = default!;
}

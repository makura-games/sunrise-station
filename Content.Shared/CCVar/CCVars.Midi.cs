using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<int> MaxMidiEventsPerSecond =
        CVarDef.Create("midi.max_events_per_second", 2048, CVar.REPLICATED | CVar.SERVER); // Sunrise edit

    public static readonly CVarDef<int> MaxMidiEventsPerBatch =
        CVarDef.Create("midi.max_events_per_batch", 1024, CVar.REPLICATED | CVar.SERVER); // Sunrise edit

    public static readonly CVarDef<int> MaxMidiBatchesPerSecond =
        CVarDef.Create("midi.max_batches_per_second", 60, CVar.REPLICATED | CVar.SERVER); // Sunrise edit

    public static readonly CVarDef<int> MaxMidiBatchesDropped =
        CVarDef.Create("midi.max_batches_dropped", 2, CVar.SERVERONLY); // Sunrise edit

    /// <summary>
    /// Defines the max amount of characters to allow in the "Midi channel selector".
    /// </summary>
    public static readonly CVarDef<int> MidiMaxChannelNameLength =
        CVarDef.Create("midi.max_channel_name_length", 64, CVar.SERVERONLY);
}

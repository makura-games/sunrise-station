using Content.Shared.CCVar;

namespace Content.Server.Instruments;

public sealed partial class InstrumentSystem
{
    public int MaxMidiEventsPerSecond { get; private set; }
    public int MaxMidiEventsPerBatch { get; private set; }
    public int MaxMidiBatchesPerSecond { get; private set; } // Sunrise-Edit
    public int MaxMidiBatchesDropped { get; private set; }

    private void InitializeCVars()
    {
        Subs.CVar(_cfg, CCVars.MaxMidiEventsPerSecond, obj => MaxMidiEventsPerSecond = obj, true);
        Subs.CVar(_cfg, CCVars.MaxMidiEventsPerBatch, obj => MaxMidiEventsPerBatch = obj, true);
        Subs.CVar(_cfg, CCVars.MaxMidiBatchesPerSecond, obj => MaxMidiBatchesPerSecond = obj, true); // Sunrise-Edit
        Subs.CVar(_cfg, CCVars.MaxMidiBatchesDropped, obj => MaxMidiBatchesDropped = obj, true);
    }
}

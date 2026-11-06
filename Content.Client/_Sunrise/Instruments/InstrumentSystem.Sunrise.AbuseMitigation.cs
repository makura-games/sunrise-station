using Content.Shared.CCVar;

#pragma warning disable IDE0130
namespace Content.Client.Instruments;

public sealed partial class InstrumentSystem
{
    // Ограничивает частоту отправки MIDI-пакетов с клиента.
    public int MaxMidiBatchesPerSecond { get; private set; }

    private void InitializeMidiAbuseCVars()
    {
        Subs.CVar(_cfg, CCVars.MaxMidiBatchesPerSecond, obj => MaxMidiBatchesPerSecond = obj, true);
    }

    private bool TryConsumeMidiBatch(InstrumentComponent instrument, TimeSpan now)
    {
        if (MaxMidiBatchesPerSecond <= 0 || now < instrument.NextMidiBatch)
            return false;

        // Оставляем запас на границе секундного окна сервера.
        var batches = Math.Max(MaxMidiBatchesPerSecond - 1, 1);
        instrument.NextMidiBatch = now.Add(TimeSpan.FromSeconds(1d / batches));
        return true;
    }
}

// ReSharper disable once CheckNamespace
namespace Content.Client.Instruments;

public sealed partial class InstrumentComponent
{
    // Время следующей допустимой отправки MIDI-пакета.
    public TimeSpan NextMidiBatch;
}

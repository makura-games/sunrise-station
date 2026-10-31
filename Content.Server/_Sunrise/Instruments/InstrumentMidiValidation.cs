using System;
using Robust.Shared.Audio.Midi;

#pragma warning disable IDE0130
namespace Content.Server.Instruments;

internal static class InstrumentMidiValidation
{
    public static bool IsValidChannel(int channel)
    {
        return channel >= 0 && channel < RobustMidiEvent.MaxChannels;
    }

    public static bool TryFilterBatch(RobustMidiEvent[] midiEvents, out RobustMidiEvent[] validEvents)
    {
        validEvents = midiEvents;

        if (midiEvents.Length == 0)
            return false;

        var validCount = 0;
        foreach (var midiEvent in midiEvents)
        {
            if (IsValidEvent(midiEvent))
                validCount++;
        }

        if (validCount == midiEvents.Length)
            return true;

        if (validCount == 0)
            return false;

        validEvents = new RobustMidiEvent[validCount];
        var index = 0;
        foreach (var midiEvent in midiEvents)
        {
            if (IsValidEvent(midiEvent))
                validEvents[index++] = midiEvent;
        }

        return true;
    }

    public static bool IsValidEvent(RobustMidiEvent midiEvent)
    {
        return midiEvent.MidiCommand switch
        {
            RobustMidiCommand.NoteOff => IsSevenBit(midiEvent.Key) && IsSevenBit(midiEvent.Velocity),
            RobustMidiCommand.NoteOn => IsSevenBit(midiEvent.Key) && IsSevenBit(midiEvent.Velocity),
            RobustMidiCommand.AfterTouch => IsSevenBit(midiEvent.Key) && IsSevenBit(midiEvent.Value),
            RobustMidiCommand.ControlChange => IsSevenBit(midiEvent.Control) && IsSevenBit(midiEvent.Value),
            RobustMidiCommand.ProgramChange => IsSevenBit(midiEvent.Program),
            RobustMidiCommand.ChannelPressure => IsSevenBit(midiEvent.Pressure),
            RobustMidiCommand.PitchBend => midiEvent.Pitch <= 0x3FFF,
            RobustMidiCommand.SystemMessage => IsValidSystemMessage(midiEvent),
            _ => false
        };
    }

    private static bool IsValidSystemMessage(RobustMidiEvent midiEvent)
    {
        if (midiEvent.Data2 != 0)
            return false;

        return midiEvent.Data1 switch
        {
            0x0B => true,
            0x00 => midiEvent.Status == 0xFF,
            _ => false
        };
    }

    private static bool IsSevenBit(int value)
    {
        return value is >= 0 and <= 0x7F;
    }
}

using System.Linq;
using Content.Shared._Sunrise.TTS;
using Robust.Shared.Prototypes;

namespace Content.Client.VoiceMask;

public sealed partial class VoiceMaskNameChangeWindow
{
    public Action<string>? OnVoiceChange;

    private List<TTSVoicePrototype> _voices = new();

    public void ReloadVoices(IPrototypeManager proto)
    {
        VoiceSelector.OnItemSelected += args =>
        {
            VoiceSelector.SelectId(args.Id);
            if (VoiceSelector.SelectedMetadata is string voice)
                OnVoiceChange?.Invoke(voice);
        };

        _voices = proto
            .EnumeratePrototypes<TTSVoicePrototype>()
            .Where(voice => voice.RoundStart)
            .OrderBy(voice => Loc.GetString(voice.Name))
            .ToList();

        for (var i = 0; i < _voices.Count; i++)
        {
            VoiceSelector.AddItem(Loc.GetString(_voices[i].Name));
            VoiceSelector.SetItemMetadata(i, _voices[i].ID);
        }

        TTSContainer.Visible = _voices.Count > 0;
    }

    public void UpdateSunriseVoice(string voice)
    {
        var voiceIndex = _voices.FindIndex(prototype => prototype.ID == voice);
        if (voiceIndex != -1)
            VoiceSelector.Select(voiceIndex);
    }
}

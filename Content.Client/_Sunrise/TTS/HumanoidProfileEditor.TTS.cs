using System.Linq;
using Content.Client._Sunrise.TTS;
using Content.Shared._Sunrise.TTS;
using Content.Shared.Preferences;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private List<TTSVoicePrototype> _voiceList = new();

    private void InitializeVoice()
    {
        _voiceList = _prototypeManager
            .EnumeratePrototypes<TTSVoicePrototype>()
            .Where(o => o.RoundStart)
            .OrderBy(o => Loc.GetString(o.Name))
            .ToList();

        TtsVoiceButton.OnItemSelected += args =>
        {
            TtsVoiceButton.SelectId(args.Id);
            SetTtsVoice(_voiceList[args.Id].ID);
        };

        VoicePlayButton.OnPressed += _ => PlayPreviewTts();
    }

    private void UpdateTtsVoicesControls()
    {
        if (Profile is null)
            return;

        TtsVoiceButton.Clear();

        var firstVoiceChoiceId = -1;
        for (var i = 0; i < _voiceList.Count; i++)
        {
            var voice = _voiceList[i];
            if (!HumanoidCharacterProfile.CanHaveVoice(voice, Profile.Sex))
                continue;

            var name = Loc.GetString(voice.Name);
            TtsVoiceButton.AddItem(name, i);

            if (firstVoiceChoiceId == -1)
                firstVoiceChoiceId = i;

            if (_sponsorsMgr is null)
                continue;
            if (!voice.SponsorOnly || _sponsorsMgr == null ||
                _sponsorsMgr.GetClientPrototypes().Contains(voice.ID))
                continue;

            TtsVoiceButton.SetItemDisabled(TtsVoiceButton.GetIdx(i), true);
            TtsVoiceButton.SetItemText(TtsVoiceButton.GetIdx(i), Loc.GetString("sponsor-marking", ("name", name))); // Sunrise-edit
        }

        var voiceChoiceId = _voiceList.FindIndex(x => x.ID == Profile.TtsVoice);
        if (firstVoiceChoiceId >= 0 &&
            !TtsVoiceButton.TrySelectId(voiceChoiceId) &&
            TtsVoiceButton.TrySelectId(firstVoiceChoiceId))
        {
            SetTtsVoice(_voiceList[firstVoiceChoiceId].ID);
        }
    }

    private void PlayPreviewTts()
    {
        if (Profile is null)
            return;

        _entManager.System<TTSSystem>().RequestPreviewTts(Profile.TtsVoice);
    }
}

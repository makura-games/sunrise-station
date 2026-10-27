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

        VoiceButton.OnItemSelected += args =>
        {
            VoiceButton.SelectId(args.Id);
            SetVoice(_voiceList[args.Id].ID);
        };

        VoicePlayButton.OnPressed += _ => PlayPreviewTts();
    }

    private void UpdateTtsVoicesControls()
    {
        if (Profile is null)
            return;

        VoiceButton.Clear();

        var firstVoiceChoiceId = -1;
        for (var i = 0; i < _voiceList.Count; i++)
        {
            var voice = _voiceList[i];
            if (!HumanoidCharacterProfile.CanHaveVoice(voice, Profile.Sex))
                continue;

            var name = Loc.GetString(voice.Name);
            VoiceButton.AddItem(name, i);

            if (firstVoiceChoiceId == -1)
                firstVoiceChoiceId = i;

            if (_sponsorsMgr is null)
                continue;
            if (!voice.SponsorOnly || _sponsorsMgr == null ||
                _sponsorsMgr.GetClientPrototypes().Contains(voice.ID))
                continue;

            VoiceButton.SetItemDisabled(VoiceButton.GetIdx(i), true);
            VoiceButton.SetItemText(VoiceButton.GetIdx(i), Loc.GetString("sponsor-marking", ("name", name))); // Sunrise-edit
        }

        var voiceChoiceId = _voiceList.FindIndex(x => x.ID == Profile.Voice);
        if (firstVoiceChoiceId >= 0 &&
            !VoiceButton.TrySelectId(voiceChoiceId) &&
            VoiceButton.TrySelectId(firstVoiceChoiceId))
        {
            SetVoice(_voiceList[firstVoiceChoiceId].ID);
        }
    }

    private void PlayPreviewTts()
    {
        if (Profile is null)
            return;

        _entManager.System<TTSSystem>().RequestPreviewTts(Profile.Voice);
    }
}

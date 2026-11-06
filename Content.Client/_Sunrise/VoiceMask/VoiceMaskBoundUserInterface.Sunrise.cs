using Content.Shared._Sunrise.SunriseCCVars;
using Content.Shared._Sunrise.TTS;
using Content.Shared.VoiceMask;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

namespace Content.Client.VoiceMask;

public sealed partial class VoiceMaskBoundUserInterface
{
    private void InitializeSunriseVoiceMaskWindow(VoiceMaskNameChangeWindow window)
    {
        if (IoCManager.Resolve<IConfigurationManager>().GetCVar(SunriseCCVars.TTSEnabled))
            window.ReloadVoices(IoCManager.Resolve<IPrototypeManager>());

        window.OnVoiceChange += voice => SendMessage(new VoiceMaskChangeVoiceMessage(voice));
    }

    private static void UpdateSunriseVoiceMaskState(VoiceMaskNameChangeWindow window, VoiceMaskBuiState state)
    {
        window.UpdateSunriseVoice(state.Voice);
    }
}

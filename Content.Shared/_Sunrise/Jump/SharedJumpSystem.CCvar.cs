namespace Content.Shared._Sunrise.Jump;

public abstract partial class SharedJumpSystem
{
    private static bool _enabled;
    private static bool _bunnyHopEnabled;
    private static TimeSpan _bunnyHopSpeedBoostWindow;
    private static float _bunnyHopSpeedUpPerJump;
    private static float _bunnyHopSpeedLimit;
    private static float _bunnyHopMinSpeedThreshold;

    private void OnClientOptionJumpSound(ClientOptionDisableJumpSoundEvent ev, EntitySessionEventArgs args)
    {
        if (ev.Disable)
            _ignoredRecipients.Add(args.SenderSession);
        else
            _ignoredRecipients.Remove(args.SenderSession);
    }

    private static void OnJumpEnableChanged(bool enable)
    {
        _enabled = enable;
    }

    private static void OnBunnyHopEnableChanged(bool enable)
    {
        _bunnyHopEnabled = enable;
    }

    private static void OnBunnyHopMinSpeedThresholdChanged(float value)
    {
        _bunnyHopMinSpeedThreshold = value;
    }

    private static void OnBunnyHopSpeedBoostWindowChanged(float value)
    {
        _bunnyHopSpeedBoostWindow = TimeSpan.FromSeconds(value);
    }

    private static void OnBunnyHopSpeedUpPerJumpChanged(float value)
    {
        _bunnyHopSpeedUpPerJump = value;
    }

    private static void OnBunnyHopSpeedLimitChanged(float value)
    {
        _bunnyHopSpeedLimit = value;
    }
}

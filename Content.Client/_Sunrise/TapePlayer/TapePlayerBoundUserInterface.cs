using Content.Shared._Sunrise.TapePlayer;
using Robust.Shared.Audio.Components;

namespace Content.Client._Sunrise.TapePlayer;

public sealed partial class TapePlayerBoundUserInterface : BoundUserInterface
{
    [Dependency] private IEntityManager _entityManager = default!;

    [ViewVariables]
    private TapePlayerMenu? _menu;

    public TapePlayerBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Open()
    {
        base.Open();

        _menu = new TapePlayerMenu();
        _menu.OnClose += Close;
        _menu.OpenCentered();

        _menu.OnPlayPressed += args =>
        {
            if (args)
            {
                SendMessage(new TapePlayerPlayingMessage());
            }
            else
            {
                SendMessage(new TapePlayerPauseMessage());
            }
        };

        _menu.OnStopPressed += () =>
        {
            SendMessage(new TapePlayerStopMessage());
        };

        _menu.SetTime += SetTime;
        _menu.SetVolume += SetVolume;
        Reload();
    }

    /// <summary>
    /// Reloads the attached menu if it exists.
    /// </summary>
    public void Reload()
    {
        if (_menu == null || !EntMan.TryGetComponent(Owner, out TapePlayerComponent? tapePlayer))
            return;

        _menu.SetAudioStream(tapePlayer.AudioStream);
        _menu.SetVolumeSlider(tapePlayer.Volume * 100f);

        if (_entityManager.TryGetComponent<MusicTapeComponent>(tapePlayer.InsertedTape, out var musicTapeComponent))
        {
            _menu.SetSelectedSong(musicTapeComponent.SongName, musicTapeComponent.SongLengthSeconds);
        }
        else
        {
            _menu.SetSelectedSong(string.Empty, 0f);
        }
    }

    public void SetTime(float time)
    {
        var sentTime = time;

        // You may be wondering, what the fuck is this
        // Well we want to be able to predict the playback slider change, of which there are many ways to do it
        // We can't just use SendPredictedMessage because it will reset every tick and audio updates every frame
        // so it will go BRRRRT
        // Using ping gets us close enough that it SHOULD, MOST OF THE TIME, fall within the 0.1 second tolerance
        // that's still on engine so our playback position never gets corrected.
        if (EntMan.TryGetComponent(Owner, out TapePlayerComponent? tapePlayer) &&
            EntMan.TryGetComponent(tapePlayer.AudioStream, out AudioComponent? audioComp))
        {
            audioComp.PlaybackPosition = time;
        }

        SendMessage(new TapePlayerSetTimeMessage(sentTime));
    }

    public void SetVolume(float volume)
    {
        SendMessage(new TapePlayerSetVolumeMessage(volume));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;

        if (_menu == null)
            return;

        _menu.OnClose -= Close;
        _menu.Orphan();
        _menu = null;
    }
}


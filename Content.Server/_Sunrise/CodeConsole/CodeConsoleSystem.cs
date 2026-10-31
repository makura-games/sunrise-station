using Content.Server.DeviceLinking.Systems;
using Content.Shared._Sunrise.CodeConsole;
using Content.Shared.Audio;
using Content.Shared.DeviceNetwork.Components;
using Content.Shared.Interaction;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server._Sunrise.CodeConsole;

public sealed partial class CodeConsoleSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private DeviceLinkSystem _deviceLink = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CodeConsoleComponent, MapInitEvent>(OnMapInit);

        SubscribeLocalEvent<CodeConsoleComponent, CodeConsoleActivateButtonMessage>(OnActivateButtonPressed);
        SubscribeLocalEvent<CodeConsoleComponent, CodeConsoleLockButtonMessage>(OnLockButtonPressed);

        SubscribeLocalEvent<CodeConsoleComponent, CodeConsoleKeypadMessage>(OnKeypadButtonPressed);
        SubscribeLocalEvent<CodeConsoleComponent, CodeConsoleKeypadClearMessage>(OnClearButtonPressed);
        SubscribeLocalEvent<CodeConsoleComponent, CodeConsoleKeypadEnterMessage>(OnEnterButtonPressed);

        SubscribeLocalEvent<CodeConsoleComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<CodeConsoleComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
    }

    private void OnMapInit(Entity<CodeConsoleComponent> ent, ref MapInitEvent args)
    {
        if (string.IsNullOrWhiteSpace(ent.Comp.Code))
        {
            if (ent.Comp.CodeLength <= 0 || ent.Comp.CodeLength > 16)
                ent.Comp.CodeLength = 16;

            ent.Comp.Code = GetRandomCode(ent.Comp.CodeLength);
        }

        UpdateUserInterface(ent);
    }

    private void OnActivateButtonPressed(Entity<CodeConsoleComponent> ent, ref CodeConsoleActivateButtonMessage args)
    {
        _audio.PlayPvs(ent.Comp.KeypadPressSound, ent.Owner);

        if (ent.Comp.IsLocked)
            return;

        _deviceLink.InvokePort(ent.Owner, ent.Comp.ActivatePort);
    }

    private void OnLockButtonPressed(Entity<CodeConsoleComponent> ent, ref CodeConsoleLockButtonMessage args)
    {
        if (ent.Comp.IsLocked)
            return;

        UpdateStatus(ent);
        UpdateUserInterface(ent);
    }

    private void OnKeypadButtonPressed(Entity<CodeConsoleComponent> ent, ref CodeConsoleKeypadMessage args)
    {
        if (args.Value < 0 || args.Value > 9)
            return;

        PlayKeypadSound(ent, args.Value);

        if (!ent.Comp.IsLocked)
            return;

        if (ent.Comp.EnteredCode.Length >= ent.Comp.CodeLength)
            return;

        ent.Comp.EnteredCode += args.Value.ToString();
        UpdateUserInterface(ent);
    }

    private void OnClearButtonPressed(Entity<CodeConsoleComponent> ent, ref CodeConsoleKeypadClearMessage args)
    {
        _audio.PlayPvs(ent.Comp.KeypadPressSound, ent.Owner);

        if (!ent.Comp.IsLocked)
            return;

        ent.Comp.EnteredCode = "";
        UpdateUserInterface(ent);
    }

    private void OnEnterButtonPressed(Entity<CodeConsoleComponent> ent, ref CodeConsoleKeypadEnterMessage args)
    {
        if (!ent.Comp.IsLocked)
        {
            _audio.PlayPvs(ent.Comp.KeypadPressSound, ent.Owner);
            return;
        }

        UpdateStatus(ent);
        UpdateUserInterface(ent);
    }

    private void OnInteractUsing(Entity<CodeConsoleComponent> ent, ref InteractUsingEvent args)
    {
        if (TryComp<NetworkConfiguratorComponent>(args.Used, out _) && ent.Comp.IsSealed)
        {
            args.Handled = true;
        }
    }

    private void OnGetVerbs(Entity<CodeConsoleComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (ent.Comp.IsSealed)
            return;

        if (!args.CanInteract || !args.CanAccess)
            return;

        var verb = new AlternativeVerb()
        {
            Text = Loc.GetString("code-console-verb-seal-name"),
            Message = Loc.GetString("code-console-verb-seal-desc"),
            Icon = new SpriteSpecifier.Texture(
                new("/Textures/Interface/VerbIcons/lock.svg.192dpi.png")),
            Act = () => { ent.Comp.IsSealed = true; }
        };
        args.Verbs.Add(verb);
    }

    private void UpdateStatus(Entity<CodeConsoleComponent> ent)
    {

        if (ent.Comp.IsLocked)
        {
            if (ent.Comp.EnteredCode == ent.Comp.Code)
            {
                ent.Comp.IsLocked = false;
                _audio.PlayPvs(ent.Comp.AccessGrantedSound, ent.Owner);
            }
            else
            {
                if (ent.Comp.EnteredCode.Length == ent.Comp.CodeLength)
                    _deviceLink.InvokePort(ent.Owner, ent.Comp.WrongCodePort);

                ent.Comp.EnteredCode = "";
                _audio.PlayPvs(ent.Comp.AccessDeniedSound, ent.Owner);
            }
        }
        else
        {
            ent.Comp.IsLocked = true;
            ent.Comp.EnteredCode = "";
            _audio.PlayPvs(ent.Comp.KeypadPressSound, ent.Owner);
        }
    }

    private void PlayKeypadSound(Entity<CodeConsoleComponent> ent, int number)
    {
        // This is a C mixolydian blues scale.
        // 1 2 3    C D Eb
        // 4 5 6    E F F#
        // 7 8 9    G A Bb
        var semitoneShift = number switch
        {
            1 => 0,
            2 => 2,
            3 => 3,
            4 => 4,
            5 => 5,
            6 => 6,
            7 => 7,
            8 => 9,
            9 => 10,
            0 => 8,
            _ => 0
        };

        var opts = ent.Comp.KeypadPressSound.Params;
        opts = AudioHelpers.ShiftSemitone(opts, semitoneShift).AddVolume(-5f);
        _audio.PlayPvs(ent.Comp.KeypadPressSound, ent.Owner, opts);
    }

    private void UpdateUserInterface(Entity<CodeConsoleComponent> ent)
    {
        if (!_ui.HasUi(ent.Owner, CodeConsoleUiKey.Key))
            return;

        var state = new CodeConsoleUiState
        {
            IsLocked = ent.Comp.IsLocked,
            EnteredCodeLength = ent.Comp.EnteredCode.Length,
            MaxCodeLength = ent.Comp.CodeLength
        };

        _ui.SetUiState(ent.Owner, CodeConsoleUiKey.Key, state);
    }

    private string GetRandomCode(int codeLength)
    {
        if (codeLength < 0)
            codeLength = 6;

        var symbols = "1234567890".ToCharArray();
        var code = new char[codeLength];

        for (int i = 0; i < codeLength; i++)
        {
            code[i] = _random.Pick(symbols);
        }

        return new string(code);
    }
}

using Content.Shared._Sunrise.Silicons.StationAi;
using Content.Server.Silicons.Borgs;
using Robust.Server.Containers;

namespace Content.Server._Sunrise.Silicons.StationAi;

/// <summary>
/// Server-side authority for station AI body gameplay.
/// <para>
/// The system prepares borg chassis as <see cref="StationAiBodyComponent"/> entities when an AI communication board is inserted,
/// then lets a station AI brain use <see cref="TryEnterBody(EntityUid, EntityUid)"/> and <see cref="TryExitBody"/>
/// to transfer its mind between the AI core and the selected body.
/// </para>
/// <para>
/// Body state is stored on <see cref="StationAiBodyComponent"/>, the brain-side controller state is stored on
/// <see cref="StationAiBodyControllerComponent"/>, and the server UI/action flow is wired from <see cref="Initialize"/>.
/// The implementation is split into state transitions, player interface, and body feature partial files.
/// </para>
/// </summary>
public sealed partial class StationAiBodySystem : EntitySystem
{
    [Dependency] private ContainerSystem _container = default!;
    [Dependency] private SiliconVoiceSystem _siliconVoice = default!;

    public override void Initialize()
    {
        base.Initialize();

        InitializeBodyState();
        InitializeBodyInterface();
        InitializeBodyFeatures();
    }
}

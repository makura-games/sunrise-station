using Content.Shared._Sunrise.HardsuitInjection.EntitySystems;
using Content.Shared.Inventory;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using System.Threading;

namespace Content.Shared._Sunrise.HardsuitInjection.Components;


[Access(typeof(InjectSystem))]
[RegisterComponent]
public sealed partial class InjectComponent : Component
{
    [DataField]
    public EntProtoId ToggleInjectionAction = "ActionToggleInjection";

    [DataField]
    public EntProtoId InjectionAction = "ActionInjection";


    [DataField("requiredSlot")]
    public SlotFlags RequiredFlags = SlotFlags.OUTERCLOTHING;

    [DataField("containerId")]
    public string ContainerId = "beakerSlot";


    [DataField("verbText")]
    public string VerbText = "hardsuitinjection-toggle";


    [DataField("delay")]
    public TimeSpan? Delay = TimeSpan.FromSeconds(30);

    [DataField("stripDelay")]
    public TimeSpan? StripDelay = TimeSpan.FromSeconds(10);


    [DataField("injectSound")]
    public SoundSpecifier InjectSound = new SoundPathSpecifier("/Audio/Items/hypospray.ogg");


    [ViewVariables(VVAccess.ReadWrite)]
    public EntityUid? ToggleInjectionActionEntity;

    [ViewVariables(VVAccess.ReadWrite)]
    public EntityUid? InjectionActionEntity;

    [ViewVariables(VVAccess.ReadWrite)]
    public ContainerSlot? Container;

    [ViewVariables(VVAccess.ReadWrite)]
    public bool Locked = true;

    [DataField("openCloseDelay")]
    public TimeSpan OpenCloseDelay = TimeSpan.FromSeconds(3);

    [DataField("canBeOpened")]
    public bool CanBeOpened = true;

    [DataField("alwaysOpen")]
    public bool AlwaysOpen = false;

    [DataField("autoClose")]
    public bool AutoClose = true;

    [DataField("autoCloseDelay")]
    public TimeSpan AutoCloseDelay = TimeSpan.FromSeconds(10);

    [ViewVariables]
    public TimeSpan LastOpenTime;

    [ViewVariables]
    public CancellationTokenSource? AutoCloseCancelToken;
}

using Content.Shared._Sunrise.CollectiveMind;
using Content.Shared.Actions;
using Content.Shared.Roles.Components;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Antags.Abductor;

[RegisterComponent, NetworkedComponent, Access(typeof(SharedAbductorSystem)), AutoGenerateComponentState]
public sealed partial class AbductorHumanObservationConsoleComponent : Component
{
    [DataField(readOnly: true)]
    public EntProtoId? RemoteEntityProto = "AbductorHumanObservationConsoleEye";

    [DataField, AutoNetworkedField]
    public NetEntity? RemoteEntity;

    [DataField, AutoNetworkedField]
    public EntityUid? Opener;
}
[RegisterComponent, NetworkedComponent, Access(typeof(SharedAbductorSystem)), AutoGenerateComponentState]
public sealed partial class AbductorConsoleComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public int Balance = 0;

    [DataField, AutoNetworkedField]
    public NetEntity? Target;

    [DataField, AutoNetworkedField]
    public NetEntity? AlienPod;

    [DataField, AutoNetworkedField]
    public NetEntity? Experimentator;

    [DataField, AutoNetworkedField]
    public NetEntity? Dispencer;

    [DataField, AutoNetworkedField]
    public NetEntity? Armor;

    [DataField, AutoNetworkedField]
    public EntityUid? Agent;

    [DataField, AutoNetworkedField]
    public EntityUid? Scientist;
}
[RegisterComponent, NetworkedComponent, Access(typeof(SharedAbductorSystem))]
public sealed partial class AbductorAlienPadComponent : Component
{
}

[RegisterComponent, NetworkedComponent, Access(typeof(SharedAbductorSystem))]
public sealed partial class AbductorVictimRoleComponent : BaseMindRoleComponent
{
}

[RegisterComponent, NetworkedComponent, Access(typeof(SharedAbductorSystem))]
public sealed partial class AbductorVictimRuleComponent : Component
{
}

[RegisterComponent, NetworkedComponent, Access(typeof(SharedAbductorSystem))]
public sealed partial class AbductorOnAlienPadComponent : Component
{
}
[RegisterComponent, NetworkedComponent, Access(typeof(SharedAbductorSystem)), AutoGenerateComponentState]
public sealed partial class AbductorExperimentatorComponent : Component
{
    [DataField, AutoNetworkedField]
    public NetEntity? Console;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public string ContainerId = "storage";
}
[RegisterComponent, NetworkedComponent, Access(typeof(SharedAbductorSystem))]
public sealed partial class AbductorDispencerComponent : Component
{
}

[RegisterComponent, NetworkedComponent, Access(typeof(SharedAbductorSystem))]
public sealed partial class AbductorComponent : Component
{
    public ProtoId<CollectiveMindPrototype> AbductorCollectiveMindProto = "Abductor";
}

[RegisterComponent, NetworkedComponent, Access(typeof(SharedAbductorSystem)), AutoGenerateComponentState]
public sealed partial class RemoteEyeSourceContainerComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Actor;
}

[RegisterComponent, NetworkedComponent, Access(typeof(SharedAbductorSystem)), AutoGenerateComponentState]
public sealed partial class AbductorsAbilitiesComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? ExitConsole;

    [DataField, AutoNetworkedField]
    public EntityUid? SendYourself;

    [DataField, AutoNetworkedField]
    public EntityUid? GizmoMark;

    [DataField, AutoNetworkedField]
    public EntityUid? SendAgent;


    [DataField]
    public EntityUid[] HiddenActions = [];
}

[RegisterComponent, Access(typeof(SharedAbductorSystem))]
public sealed partial class AbductConditionComponent : Component
{
    [DataField("abducted"), ViewVariables(VVAccess.ReadWrite)]
    public int Abducted;
    [DataField("hashset"), ViewVariables(VVAccess.ReadWrite)]
    public HashSet<NetEntity> AbductedHashs = [];
}

#region Events

public sealed partial class ExitConsoleEvent : InstantActionEvent
{

}
public sealed partial class SendYourselfEvent : WorldTargetActionEvent
{

}

public sealed partial class SendAgentEvent : WorldTargetActionEvent
{

}
public sealed partial class GizmoMarkEvent : EntityTargetActionEvent
{

}
public sealed partial class AbductorReturnToShipEvent : InstantActionEvent
{

}

#endregion

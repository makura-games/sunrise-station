using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Content.Shared.NPC.Prototypes;
﻿using Content.Shared.Actions;


namespace Content.Shared._Sunrise.PersonalBiocode;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PersonalBiocodeComponent : Component // Пока только для модсьюитов
{
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("breakAble"), AutoNetworkedField]
    public bool BreakAble  = true;

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("actionEntity"), AutoNetworkedField]
    public EntityUid? ActionEntity;

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("DNA"), AutoNetworkedField]
    public string DNA = "";

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("isAuthorized"), AutoNetworkedField]
    public bool IsAuthorized = false;
}

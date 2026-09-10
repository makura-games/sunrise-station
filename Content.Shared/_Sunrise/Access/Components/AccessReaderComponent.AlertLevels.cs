using Robust.Shared.Prototypes;

#pragma warning disable IDE0130 // Пространство имён соответствует расширяемому upstream-компоненту.
namespace Content.Shared.Access.Components;

public sealed partial class AccessReaderComponent
{
    /// <summary>
    /// Additional access groups granted by simultaneously active alert levels.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public HashSet<ProtoId<AccessGroupPrototype>> AdditionalGroups = [];
}

public sealed partial class AccessReaderComponentState
{
    /// <summary>
    /// Additional temporary access groups synchronized for alert-level access checks.
    /// </summary>
    public HashSet<ProtoId<AccessGroupPrototype>> AdditionalGroups;
}

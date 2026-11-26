using Robust.Shared.Serialization;

namespace Content.Shared._Sunrise.Silicons.StationAi;

[Serializable, NetSerializable]
public enum StationAiBodyUiKey : byte
{
    /// <summary>
    /// Body selector interface on the station AI brain entity.
    /// </summary>
    Key,
}

/// <summary>
/// One station AI body entry displayed in the body selector.
/// </summary>
[Serializable, NetSerializable]
public sealed class StationAiBodyEntry : IRobustCloneable<StationAiBodyEntry>
{
    public NetEntity Body;
    public int BodyNumber;
    public string Name = string.Empty;
    public NetEntity? LinkedAi;
    public bool IsCurrent;

    public StationAiBodyEntry()
    {
    }

    public StationAiBodyEntry(
        NetEntity body,
        int bodyNumber,
        string name,
        NetEntity? linkedAi,
        bool isCurrent)
    {
        Body = body;
        BodyNumber = bodyNumber;
        Name = name;
        LinkedAi = linkedAi;
        IsCurrent = isCurrent;
    }

    public StationAiBodyEntry Clone()
    {
        return new StationAiBodyEntry(Body, BodyNumber, Name, LinkedAi, IsCurrent);
    }
}

/// <summary>
/// Requests station AI transfer into the selected free body.
/// </summary>
[Serializable, NetSerializable]
public sealed class StationAiBodyEnterMessage(NetEntity body) : BoundUserInterfaceMessage
{
    public NetEntity Body { get; } = body;
}

/// <summary>
/// Requests manual return from the current body to the station AI brain.
/// </summary>
[Serializable, NetSerializable]
public sealed class StationAiBodyExitMessage : BoundUserInterfaceMessage;

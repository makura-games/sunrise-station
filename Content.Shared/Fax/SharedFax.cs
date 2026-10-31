using System.Numerics;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Fax;

[Serializable, NetSerializable]
public enum FaxUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class FaxUiState : BoundUserInterfaceState
{
    public string DeviceName { get; }
    public Dictionary<string, string> AvailablePeers { get; }
    public string? DestinationAddress { get; }
    public bool IsPaperInserted { get; }
    public bool CanSend { get; }
    public bool CanCopy { get; }

    public FaxUiState(string deviceName,
        Dictionary<string, string> peers,
        bool canSend,
        bool canCopy,
        bool isPaperInserted,
        string? destAddress)
    {
        DeviceName = deviceName;
        AvailablePeers = peers;
        IsPaperInserted = isPaperInserted;
        CanSend = canSend;
        CanCopy = canCopy;
        DestinationAddress = destAddress;
    }
}

[Serializable, NetSerializable]
public sealed class FaxFileMessage : BoundUserInterfaceMessage
{
    public string? Label;
    public string Content;
    public bool OfficePaper;
    // Sunrise-Start
    public SpriteSpecifier? ImageContent { get; set; }
    public Vector2 ImageScale { get; set; }
    // Sunrise-End

    public FaxFileMessage(string? label, string content, bool officePaper, SpriteSpecifier? imageContent = null, Vector2 imageScale = default)
    {
        Label = label;
        Content = content;
        OfficePaper = officePaper;
        // Sunrise-Start
        ImageContent = imageContent;
        ImageScale = imageScale;
        // Sunrise-End
    }
}

public static class FaxFileMessageValidation
{
    public const int MaxLabelSize = 50; // parity with Content.Server.Labels.Components.HandLabelerComponent.MaxLabelChars
    public const int MaxContentSize = 10000;
}

[Serializable, NetSerializable]
public sealed class FaxCopyMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class FaxSendMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class FaxRefreshMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class FaxDestinationMessage : BoundUserInterfaceMessage
{
    public string Address { get; }
    public FaxDestinationMessage(string address)
    {
        Address = address;
    }
}

using Robust.Shared.Serialization;

namespace Content.Shared._Sunrise.Paint;

[Serializable, NetSerializable]
public enum PaintUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class PaintSetColorMessage(Color color) : BoundUserInterfaceMessage
{
    public readonly Color Color = color;
}

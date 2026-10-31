using System.Numerics;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Paper;

public sealed partial class PaperComponent
{
    [DataField, AutoNetworkedField]
    public Color DefaultColor = new(25, 25, 25);

    [DataField, AutoNetworkedField]
    public SpriteSpecifier? ImageContent { get; set; }

    [DataField, AutoNetworkedField]
    public Vector2? ImageScale { get; set; }

    [Serializable, NetSerializable]
    public sealed class PaperBoundUserInterfaceState : BoundUserInterfaceState
    {
        public readonly string Text;
        public readonly Color DefaultColor;
        public readonly List<StampDisplayInfo> StampedBy;
        public readonly PaperAction Mode;
        public readonly SpriteSpecifier? ImageContent;
        public readonly Vector2? ImageScale;
        public readonly bool TemplateFieldsEnabled;

        public PaperBoundUserInterfaceState(
            string text,
            Color defaultColor,
            List<StampDisplayInfo> stampedBy,
            PaperAction mode = PaperAction.Read,
            SpriteSpecifier? imageContent = null,
            Vector2? imageScale = null,
            bool templateFieldsEnabled = false)
        {
            Text = text;
            DefaultColor = defaultColor;
            StampedBy = stampedBy;
            Mode = mode;
            ImageContent = imageContent;
            ImageScale = imageScale;
            TemplateFieldsEnabled = templateFieldsEnabled;
        }
    }

    [Serializable, NetSerializable]
    public enum PaperTemplateRequestType : byte
    {
        Signature,
        Job
    }

    [Serializable, NetSerializable]
    public sealed class PaperTemplateRequestMessage(PaperTemplateRequestType type, int index) : BoundUserInterfaceMessage
    {
        public PaperTemplateRequestType Type { get; } = type;
        public int Index { get; } = index;
    }
}

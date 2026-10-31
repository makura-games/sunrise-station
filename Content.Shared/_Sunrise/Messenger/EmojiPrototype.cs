using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array;

namespace Content.Shared._Sunrise.Messenger;

/// <summary>
/// Prototype of an emoji for the messenger.
/// </summary>
[Prototype]
public sealed partial class EmojiPrototype : IPrototype, IInheritingPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [ParentDataField(typeof(AbstractPrototypeIdArraySerializer<EmojiPrototype>))]
    public string[]? Parents { get; private set; }

    [NeverPushInheritance]
    [AbstractDataField]
    public bool Abstract { get; private set; }

    /// <summary>
    /// Short emoji code (e.g., ":smile:").
    /// </summary>
    [DataField(required: true)]
    public string Code { get; private set; } = default!;

    /// <summary>
    /// Path to the emoji sprite.
    /// </summary>
    [DataField(required: true)]
    public string SpritePath { get; private set; } = default!;

    /// <summary>
    /// Emoji sprite state.
    /// </summary>
    [DataField(required: true)]
    public string SpriteState { get; private set; } = default!;
}

using System.Numerics;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.RichText;
using Robust.Shared.Utility;

namespace Content.Client._Sunrise.UserInterface.Controls
{
    /// <summary>
    /// A rich text label control that supports custom markup tags (like sponsor gradients/emojis).
    /// </summary>
    [Virtual]
    public partial class SunriseRichTextLabel : Control
    {
        [Dependency] private MarkupTagManager _tagManager = default!;

        private FormattedMessage? _message;
        private SunriseRichTextEntry _entry;
        private float _lineHeightScale = 1;
        private bool _lineHeightOverride;
        private readonly MarkupDrawingContext _drawingContext = new();

        /// <summary>
        /// Gets or sets the scale factor for the line height of the text.
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)]
        public float LineHeightScale
        {
            get
            {
                if (!_lineHeightOverride && TryGetStyleProperty(nameof(LineHeightScale), out float value))
                    return value;

                return _lineHeightScale;
            }
            set
            {
                _lineHeightScale = value;
                _lineHeightOverride = true;
                InvalidateMeasure();
            }
        }

        /// <summary>
        /// Gets or sets the markup text shown by this label. Setting this to null clears the label.
        /// </summary>
        public string? Text
        {
            get => _message?.ToMarkup();
            set
            {
                // Sunrise-Edit start - Очищаем сообщение и инвалидируем размер при null значении
                if (value == null)
                {
                    _message = null;
                    InvalidateMeasure();
                    return;
                }
                // Sunrise-Edit end

                SetMessage(FormattedMessage.FromMarkupPermissive(value));
            }
        }

        public SunriseRichTextLabel()
        {
            IoCManager.InjectDependencies(this);
            VerticalAlignment = VAlignment.Center;
        }

        /// <summary>
        /// Sets the formatted message content with optional allowed tags and default color.
        /// </summary>
        public void SetMessage(FormattedMessage message, Type[]? tagsAllowed = null, Color? defaultColor = null)
        {
            // Sunrise-Edit start - Удаляем старые inline-контролы перед заменой
            _entry.RemoveControls();
            _message = message;
            _entry = new SunriseRichTextEntry(_message, this, _tagManager, tagsAllowed, defaultColor);
            InvalidateMeasure();
            // Sunrise-Edit end
        }

        /// <summary>
        /// Sets a plain text message content with optional allowed tags and default color.
        /// </summary>
        public void SetMessage(string message, Type[]? tagsAllowed = null, Color? defaultColor = null)
        {
            var msg = new FormattedMessage();
            msg.AddText(message);
            SetMessage(msg, tagsAllowed, defaultColor);
        }

        /// <summary>
        /// Retrieves the markup representation of the current message.
        /// </summary>
        public string? GetMessage() => _message?.ToMarkup();

        protected override Vector2 MeasureOverride(Vector2 availableSize)
        {
            if (_message == null)
            {
                return Vector2.Zero;
            }

            var font = _getFont();
            _entry.Update(_tagManager, font, availableSize.X * UIScale, UIScale, LineHeightScale);

            return new Vector2(_entry.Width / UIScale, _entry.Height / UIScale);
        }

        protected override void Draw(DrawingHandleScreen handle)
        {
            base.Draw(handle);

            if (_message == null)
            {
                return;
            }

            // Sunrise-Edit start - Очищаем кэшированный контекст рисования и переиспользуем его
            _drawingContext.Clear();
            _entry.Draw(_tagManager, handle, _getFont(), SizeBox, 0, _drawingContext, UIScale, LineHeightScale);
            // Sunrise-Edit end
        }

        [Pure]
        private Font _getFont()
        {
            if (TryGetStyleProperty<Font>("font", out var font))
            {
                return font;
            }

            return UserInterfaceManager.ThemeDefaults.DefaultFont;
        }
    }
}

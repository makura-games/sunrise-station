using Content.Client.Stylesheets;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client._Sunrise.Disease.Microscope;

/// <summary>
/// Подсказка микроскопа: заголовок и текст в рамке ограниченной ширины.
/// </summary>
/// <remarks>
/// Стандартная подсказка не переносит строки и растягивается на весь экран, поэтому длинные
/// правила стадии читать невозможно. Здесь ширина фиксирована, а текст переносится.
/// </remarks>
public sealed class DiseaseTooltip : Control
{
    /// <summary>Предельная ширина подсказки в виртуальных пикселях.</summary>
    private const float TooltipWidth = 320f;

    public DiseaseTooltip(string title, string? text = null)
    {
        MaxWidth = TooltipWidth;

        var contents = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(6, 4),
            SeparationOverride = 2,
        };

        contents.AddChild(new RichTextLabel
        {
            HorizontalExpand = true,
            StyleClasses = { StyleClass.TooltipTitle },
            Text = title,
        });

        if (!string.IsNullOrWhiteSpace(text))
        {
            contents.AddChild(new RichTextLabel
            {
                HorizontalExpand = true,
                StyleClasses = { StyleClass.TooltipDesc },
                Text = text,
            });
        }

        AddChild(new PanelContainer
        {
            StyleClasses = { StyleClass.TooltipPanel },
            Children = { contents },
        });
    }

    /// <summary>Вешает подсказку на элемент; текст берётся в момент показа.</summary>
    public static void Attach(Control control, Func<string> title, Func<string>? text = null)
    {
        control.MouseFilter = MouseFilterMode.Stop;
        control.TooltipSupplier = _ => new DiseaseTooltip(title(), text?.Invoke());
    }

    /// <summary>Вешает постоянную подсказку на элемент.</summary>
    public static void Attach(Control control, string title, string? text = null)
    {
        Attach(control, () => title, text == null ? null : () => text);
    }
}

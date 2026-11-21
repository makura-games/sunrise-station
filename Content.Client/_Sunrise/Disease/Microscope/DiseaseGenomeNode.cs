using System.Numerics;
using Content.Client.Stylesheets.Palette;
using Content.Shared._Sunrise.Disease.Microscope;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Timing;

namespace Content.Client._Sunrise.Disease.Microscope;

/// <summary>
/// Узел одной стадии на ленте генома: отрезок двойной спирали и метка стадии.
/// </summary>
/// <remarks>
/// Каждая стадия — отдельный элемент интерфейса, а не кусок общего рисунка: движок считает
/// подсказку привязанной к элементу под курсором, поэтому на общей ленте она не сменялась
/// при переходе между стадиями и ждала, пока курсор замрёт.
/// </remarks>
public sealed class DiseaseGenomeNode : Control
{
    /// <summary>Подсказка показывается почти сразу: по ленте водят курсором, а не замирают на ней.</summary>
    private const float HoverDelay = 0.05f;

    private bool _done;
    private bool _current;
    private bool _hovered;
    private int _index;
    private float _time;

    public DiseaseGenomeNode()
    {
        MouseFilter = MouseFilterMode.Stop;
        HorizontalExpand = true;
        TooltipDelay = HoverDelay;
        TooltipSupplier = _ => new DiseaseTooltip(Description);
    }

    /// <summary>Текст подсказки этой стадии.</summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>Обновляет узел под текущее состояние стадии.</summary>
    public void SetStage(int index, bool done, bool current, string description)
    {
        _index = index;
        _done = done;
        _current = current;
        Description = description;
    }

    protected override void MouseEntered()
    {
        base.MouseEntered();
        _hovered = true;
    }

    protected override void MouseExited()
    {
        base.MouseExited();
        _hovered = false;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        _time += args.DeltaSeconds;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        if (PixelWidth <= 0 || PixelHeight <= 0)
            return;

        var middle = PixelHeight * 0.5f;
        var amplitude = PixelHeight * 0.26f;
        var strand = DiseaseMicroscopePalette.Marking.WithAlpha(0.22f);
        var step = Math.Max(1f, PixelWidth / 12f);

        // Фаза считается от номера стадии, поэтому нити соседних узлов сходятся на стыке.
        for (var x = 0f; x < PixelWidth; x += step)
        {
            var next = Math.Min(x + step, PixelWidth);
            var from = (_index + x / PixelWidth) * MathF.PI;
            var to = (_index + next / PixelWidth) * MathF.PI;

            handle.DrawLine(
                new Vector2(x, middle + MathF.Sin(from) * amplitude),
                new Vector2(next, middle + MathF.Sin(to) * amplitude),
                strand);
            handle.DrawLine(
                new Vector2(x, middle - MathF.Sin(from) * amplitude),
                new Vector2(next, middle - MathF.Sin(to) * amplitude),
                strand);
        }

        var center = new Vector2(PixelWidth * 0.5f, middle);
        var color = _done
            ? DiseaseMicroscopePalette.Colony(DiseaseCultureBoard.Target)
            : _current
                ? Palettes.Cyan.Text
                : Palettes.Slate.Text;

        // Перемычка между нитями показывает, что стадия закрыта.
        if (_done)
            handle.DrawLine(center + new Vector2(0, -amplitude), center + new Vector2(0, amplitude), color.WithAlpha(0.5f));

        var radius = PixelHeight * 0.17f;
        if (_current && !_done)
        {
            var pulse = 0.5f + 0.5f * MathF.Sin(_time * 3f);
            handle.DrawCircle(center, radius * (1f + 0.25f * pulse), color.WithAlpha(0.25f + 0.35f * pulse), false);
        }

        if (_hovered)
            handle.DrawCircle(center, radius * 1.45f, color.WithAlpha(0.35f), false);

        handle.DrawCircle(center, radius, color.WithAlpha(_done ? 0.85f : 0.25f));
        handle.DrawCircle(center, radius, color.WithAlpha(0.9f), false);
    }
}

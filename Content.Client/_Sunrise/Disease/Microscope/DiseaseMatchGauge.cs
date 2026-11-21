using Content.Client.Stylesheets.Palette;
using Content.Shared._Sunrise.Disease.Microscope;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client._Sunrise.Disease.Microscope;

/// <summary>
/// Положение сторон в партии: ваша колония тянет дорожку слева, конкурент — справа,
/// а метка стоит на числе клеток, которого стадии не хватает.
/// </summary>
/// <remarks>
/// Оба условия партии сведены к одной метке: она учитывает и норму стадии, и нужную долю поля,
/// поэтому с ростом конкурента уезжает вправо. Числа объясняет подсказка, на шкале их нет.
/// Стороны заданы местом и цветом, а не подписями: левый край и зелёный — всегда игрок,
/// правый край и красный — всегда конкурент.
/// </remarks>
public sealed class DiseaseMatchGauge : Control
{
    private int _target;
    private int _rival;
    private int _required = 1;

    public DiseaseMatchGauge()
    {
        MinHeight = 22;
        HorizontalExpand = true;
    }

    /// <summary>Обновляет счёт партии.</summary>
    /// <param name="target">Клетки вашей колонии.</param>
    /// <param name="rival">Клетки конкурента.</param>
    /// <param name="required">Клетки, которых требует стадия при таком конкуренте.</param>
    public void SetScore(int target, int rival, int required)
    {
        _target = Math.Max(0, target);
        _rival = Math.Max(0, rival);
        _required = Math.Max(1, required);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        if (PixelWidth <= 0 || PixelHeight <= 0)
            return;

        // Метка выходит за дорожку сверху и снизу, поэтому дорожке оставлена не вся высота.
        var margin = PixelHeight * 0.18f;
        var top = margin;
        var bottom = PixelHeight - margin;
        handle.DrawRect(new UIBox2(0, top, PixelWidth, bottom), Palettes.Navy.Background);

        // Дорожка вмещает требование с запасом, поэтому метка не уезжает за край даже при перевесе.
        var scale = Math.Max(_required * 1.35f, _target + _rival);
        var targetColor = DiseaseMicroscopePalette.Colony(DiseaseCultureBoard.Target);
        var rivalColor = DiseaseMicroscopePalette.Colony(DiseaseCultureBoard.Opponent);
        var targetWidth = PixelWidth * (_target / scale);
        var rivalWidth = PixelWidth * (_rival / scale);

        handle.DrawRect(new UIBox2(0, top, targetWidth, bottom), targetColor);
        handle.DrawRect(new UIBox2(PixelWidth - rivalWidth, top, PixelWidth, bottom), rivalColor);

        // Края дорожки подкрашены всегда: по ним видно, чья сторона где, даже на пустом поле.
        var edge = Math.Max(2f, PixelWidth * 0.008f);
        handle.DrawRect(new UIBox2(0, top, edge, bottom), targetColor);
        handle.DrawRect(new UIBox2(PixelWidth - edge, top, PixelWidth, bottom), rivalColor);

        DrawMark(handle, PixelWidth * (_required / scale), top, bottom);
    }

    /// <summary>
    /// Рисует метку требования: пока колония её не прошла, она заметна на фоне дорожки,
    /// а после — загорается цветом колонии, и стадию можно закрывать.
    /// </summary>
    private void DrawMark(DrawingHandleScreen handle, float position, float top, float bottom)
    {
        var reached = _target >= _required;
        var color = reached
            ? DiseaseMicroscopePalette.Colony(DiseaseCultureBoard.Target)
            : Palettes.Amber.Base;

        var width = Math.Max(2f, PixelWidth * 0.004f);
        var cap = Math.Max(3f, PixelHeight * 0.16f);

        // Тёмная подложка держит метку читаемой и поверх своей колонии, и поверх чужой.
        handle.DrawRect(new UIBox2(position - width * 2f, top, position + width * 2f, bottom),
            Palettes.Navy.Background.WithAlpha(0.75f));
        handle.DrawRect(new UIBox2(position - width, top - cap, position + width, bottom + cap), color);

        // Навершия-перекладины: по ним метка находится взглядом и не сливается с краями дорожки.
        var reach = width * 3f;
        handle.DrawRect(new UIBox2(position - reach, top - cap, position + reach, top - cap + width), color);
        handle.DrawRect(new UIBox2(position - reach, bottom + cap - width, position + reach, bottom + cap), color);
    }
}

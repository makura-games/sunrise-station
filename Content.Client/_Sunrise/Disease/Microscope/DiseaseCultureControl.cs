using System.Numerics;
using Content.Client.Stylesheets.Palette;
using Content.Shared._Sunrise.Disease.Microscope;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Input;
using Robust.Shared.Timing;

namespace Content.Client._Sunrise.Disease.Microscope;

/// <summary>
/// Оценка текущего наведения: окрашивает рамку инструмента до того, как игрок потратит каплю.
/// </summary>
public enum DiseaseCulturePreview : byte
{
    /// <summary>Нейтральное наведение: курсор вне поля или партия не идёт.</summary>
    Neutral,

    /// <summary>Ход даст нужный результат.</summary>
    Allowed,

    /// <summary>Ход сработает, но с побочным эффектом: ростом конкурента или потерей цели.</summary>
    Caution,

    /// <summary>Ход невозможен или бесполезен.</summary>
    Blocked,
}

/// <summary>
/// Интерактивный окуляр партии: показывает поле, область инструмента, предсказанный ход игрока
/// и последний ответ конкурента. Поле приходит с сервера и этим контролом никогда не меняется.
/// </summary>
/// <remarks>
/// Под оплаченным прогнозом окуляр дополнительно разыгрывает ответ конкурента на наведённую каплю:
/// ответ детерминирован, поэтому совпадает с тем, что затем сделает сервер.
/// </remarks>
public sealed class DiseaseCultureControl : Control
{
    public const string StylePropertyOcular = "microscope-ocular";

    private byte[] _cells = [];
    private byte[] _previousCells = [];
    private byte[] _preview = [];
    private byte[] _forecast = [];
    private List<int> _forecastMoves = new();
    private bool _forecastActive;
    private int _turn;
    private List<int> _area = new();
    private List<int> _opponentMoves = new();
    private DiseaseMicroscopeTool _tool = DiseaseMicroscopeTool.Nutrient;
    private DiseaseCultureProfile _profile;
    private float _transition = 1f;
    private float _pulse = 1f;
    private float _reply = 1f;
    private float _time;
    private Vector2i _pulseCell;
    private DiseaseMicroscopeTool _pulseTool;

    /// <summary>Вызывается после щелчка инструментом по допустимой координате поля.</summary>
    public event Action<DiseaseMicroscopeTool, int, int>? OnApply;

    /// <summary>Вызывается при смене места наведения или предсказанного результата.</summary>
    public event Action? OnPreviewChanged;

    /// <summary>Принимает ли окуляр ходы; осмотр наведением доступен всегда.</summary>
    public bool InteractionEnabled { get; set; }

    /// <summary>Оценка текущего наведения, выставляемая окном после пересчёта предпросмотра.</summary>
    public DiseaseCulturePreview PreviewState { get; set; }

    /// <summary>Текущая координата поля под курсором.</summary>
    public Vector2i? HoveredCell { get; private set; }

    /// <summary>Текущее авторитетное поле.</summary>
    public IReadOnlyList<byte> Cells => _cells;

    /// <summary>Точный результат выбранного инструмента в месте наведения.</summary>
    public IReadOnlyList<byte> Preview => _preview;

    /// <summary>Условия партии: от них зависят области инструментов и правила роста.</summary>
    public DiseaseCultureProfile Profile
    {
        get => _profile;
        set
        {
            if (_profile == value)
                return;
            _profile = value;
            RebuildPreview();
        }
    }

    /// <summary>Выбранный инструмент, независимый от выбора других пользователей.</summary>
    public DiseaseMicroscopeTool Tool
    {
        get => _tool;
        set
        {
            if (_tool == value)
                return;
            _tool = value;
            RebuildPreview();
        }
    }

    public DiseaseCultureControl()
    {
        MouseFilter = MouseFilterMode.Stop;
        MinSize = new Vector2(460, 460);
    }

    /// <summary>Заменяет отображаемое поле, анимируя только действительно изменившиеся клетки.</summary>
    public void SetCells(byte[] cells)
    {
        var changed = cells.Length != _cells.Length;
        for (var i = 0; !changed && i < cells.Length; i++)
            changed = cells[i] != _cells[i];

        if (!changed)
            return;

        _previousCells = _cells;
        _cells = (byte[]) cells.Clone();
        _transition = 0f;
        RebuildPreview();
    }

    /// <summary>Включает прогноз ответа конкурента для наведённой капли.</summary>
    /// <param name="active">Оплачен ли прогноз на текущий ход.</param>
    /// <param name="turn">Сколько ходов уже сделано: от номера хода зависит ответ конкурента.</param>
    public void SetForecast(bool active, int turn)
    {
        if (_forecastActive == active && _turn == turn)
            return;

        _forecastActive = active;
        _turn = turn;
        RebuildPreview();
    }

    /// <summary>Отмечает клетки, занятые последним ответом конкурента.</summary>
    public void SetOpponentMoves(List<int> moves)
    {
        var changed = moves.Count != _opponentMoves.Count;
        for (var i = 0; !changed && i < moves.Count; i++)
            changed = moves[i] != _opponentMoves[i];

        if (!changed)
            return;

        _opponentMoves = new List<int>(moves);
        _reply = moves.Count > 0 ? 0f : 1f;
    }

    /// <summary>Выполнены ли условия стадии: норма клеток и доля поля.</summary>
    public bool IsTargetReady =>
        _cells.Length == DiseaseCultureBoard.CellCount &&
        _profile.IsReached(
            DiseaseCultureBoard.CountColony(_cells, DiseaseCultureBoard.Target),
            DiseaseCultureBoard.CountColony(_cells, DiseaseCultureBoard.Opponent));

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);
        SetHover(args.RelativePixelPosition);
    }

    protected override void MouseExited()
    {
        base.MouseExited();
        HoveredCell = null;
        RebuildPreview();
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        args.Handle();
        SetHover(args.RelativePixelPosition);
        if (!InteractionEnabled || HoveredCell is not { } cell || _cells.Length != DiseaseCultureBoard.CellCount)
            return;

        _pulseCell = cell;
        _pulseTool = Tool;
        _pulse = 0f;
        OnApply?.Invoke(Tool, cell.X, cell.Y);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        _time += args.DeltaSeconds;
        _transition = Math.Min(1f, _transition + args.DeltaSeconds * 5f);
        _pulse = Math.Min(1f, _pulse + args.DeltaSeconds * 3f);
        _reply = Math.Min(1f, _reply + args.DeltaSeconds * 0.8f);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        var side = Math.Min(PixelWidth, PixelHeight);
        if (side <= 0)
            return;

        var origin = (PixelSize - new Vector2(side)) * 0.5f;
        if (TryGetStyleProperty<Texture>(StylePropertyOcular, out var ocular))
            handle.DrawTextureRect(ocular, UIBox2.FromDimensions(origin, new Vector2(side)));
        else
            handle.DrawCircle(origin + new Vector2(side * 0.5f), side * 0.46f, Palettes.Cyan.BackgroundDark);

        GetField(out var start, out var step);
        DrawDish(handle, origin, side);
        DrawCulture(handle, start, step);
        DrawForecast(handle, start, step);
        DrawOpponentReply(handle, start, step);
        DrawFootprint(handle, start, step);
        DrawPulse(handle, start, step);
    }

    private void DrawDish(DrawingHandleScreen handle, Vector2 origin, float side)
    {
        var center = origin + new Vector2(side * 0.5f);

        // Ободок чашки подсказывает готовность цели раньше, чем игрок посмотрит на счётчик клеток.
        var ready = IsTargetReady;
        var rim = ready ? Palettes.Green.Base : DiseaseMicroscopePalette.Marking;
        var breath = ready ? 0.3f + 0.2f * MathF.Sin(_time * 2f) : 0.14f;
        handle.DrawCircle(center, side * 0.455f, rim.WithAlpha(breath), false);
        handle.DrawCircle(center, side * 0.44f, DiseaseMicroscopePalette.Marking.WithAlpha(0.07f), false);
    }

    private void DrawCulture(DrawingHandleScreen handle, Vector2 start, float step)
    {
        for (var y = 0; y < DiseaseCultureBoard.Size; y++)
        {
            for (var x = 0; x < DiseaseCultureBoard.Size; x++)
            {
                if (!DiseaseCultureBoard.IsInside(x, y))
                    continue;

                var center = start + new Vector2(x + 0.5f, y + 0.5f) * step;
                handle.DrawCircle(center, Math.Max(1f, step * 0.025f), DiseaseMicroscopePalette.Marking.WithAlpha(0.12f));

                var index = DiseaseCultureBoard.GetIndex(x, y);
                var colony = index < _cells.Length ? _cells[index] : (byte)0;
                var previous = index < _previousCells.Length ? _previousCells[index] : (byte)0;

                if (colony == DiseaseCultureBoard.Sterile)
                {
                    // Выжженная клетка ещё догорает прежней колонией, чтобы потеря материала читалась.
                    if (DiseaseCultureBoard.IsColony(previous) && _transition < 1f)
                        DrawColony(handle, center, step * 0.38f * (1f - _transition), previous, 1f - _transition);

                    DrawSterile(handle, center, step, 1f);
                    continue;
                }

                if (colony != 0)
                {
                    var scale = previous == colony ? 1f : 0.2f + 0.8f * _transition;
                    DrawColony(handle, center, step * 0.38f * scale, colony, 1f);
                    DrawConnections(handle, index, center, step, colony);
                }
                else if (DiseaseCultureBoard.IsColony(previous) && _transition < 1f)
                {
                    DrawColony(handle, center, step * 0.38f * (1f - _transition), previous, 1f - _transition);
                }

                if (HoveredCell == null || _preview.Length != _cells.Length)
                    continue;

                DrawPreviewCell(handle, center, step, index, colony);
            }
        }
    }

    /// <summary>Обводит клетки, которые конкурент занял последним ответом.</summary>
    private void DrawOpponentReply(DrawingHandleScreen handle, Vector2 start, float step)
    {
        if (_reply >= 1f || _opponentMoves.Count == 0)
            return;

        var color = DiseaseMicroscopePalette.Colony(DiseaseCultureBoard.Opponent);
        foreach (var index in _opponentMoves)
        {
            if (index < 0 || index >= DiseaseCultureBoard.CellCount)
                continue;

            var center = start + new Vector2(index % DiseaseCultureBoard.Size + 0.5f, index / DiseaseCultureBoard.Size + 0.5f) * step;
            handle.DrawCircle(center, step * (0.45f + 0.25f * (1f - _reply)), color.WithAlpha(0.8f * (1f - _reply)), false);
        }
    }

    /// <summary>
    /// Рисует предсказанный ответ конкурента на наведённую каплю: где он прорастёт,
    /// какую клетку цели вытеснит и где отравит среду.
    /// </summary>
    private void DrawForecast(DrawingHandleScreen handle, Vector2 start, float step)
    {
        if (HoveredCell == null || _forecastMoves.Count == 0 || _forecast.Length != _preview.Length)
            return;

        var rival = DiseaseMicroscopePalette.Colony(DiseaseCultureBoard.Opponent);
        var blink = 0.55f + 0.25f * MathF.Sin(_time * 4f);
        foreach (var index in _forecastMoves)
        {
            if (index < 0 || index >= _forecast.Length)
                continue;

            var center = start + new Vector2(index % DiseaseCultureBoard.Size + 0.5f, index / DiseaseCultureBoard.Size + 0.5f) * step;
            var before = _preview[index];
            var after = _forecast[index];

            if (after == DiseaseCultureBoard.Sterile)
                DrawSterile(handle, center, step, blink);
            else if (after == DiseaseCultureBoard.Opponent && before != DiseaseCultureBoard.Opponent)
                DrawColony(handle, center, step * 0.3f, DiseaseCultureBoard.Opponent, blink * 0.6f);

            // Пунктир отличает прогноз от собственного хода: это то, что сделает конкурент, а не вы.
            DrawDashedRing(handle, center, step * 0.44f, rival.WithAlpha(blink));

            // Потерю вашей клетки выделяем отдельно: ради неё прогноз обычно и берут.
            if (before == DiseaseCultureBoard.Target)
                handle.DrawCircle(center, step * 0.48f, DiseaseMicroscopePalette.Removed.WithAlpha(blink), false);
        }
    }

    private static void DrawDashedRing(DrawingHandleScreen handle, Vector2 center, float radius, Color color)
    {
        const int dashes = 8;
        for (var dash = 0; dash < dashes; dash++)
        {
            var from = MathF.Tau * dash / dashes;
            var to = from + MathF.Tau / dashes * 0.5f;
            handle.DrawLine(
                center + new Vector2(MathF.Cos(from), MathF.Sin(from)) * radius,
                center + new Vector2(MathF.Cos(to), MathF.Sin(to)) * radius,
                color);
        }
    }

    private void DrawPreviewCell(DrawingHandleScreen handle, Vector2 center, float step, int index, byte colony)
    {
        var preview = _preview[index];

        // Призрак новой клетки окрашен цветом своей колонии, поэтому рост конкурента виден сразу.
        if (colony == 0 && DiseaseCultureBoard.IsColony(preview))
        {
            DrawColony(handle, center, step * 0.34f, preview, 0.4f);
            handle.DrawCircle(center, step * 0.38f, DiseaseMicroscopePalette.Colony(preview).WithAlpha(0.35f), false);
            return;
        }

        // Выжигание показывается заранее: по нему видно, какую часть среды хлор заберёт навсегда.
        if (preview == DiseaseCultureBoard.Sterile)
        {
            DrawSterile(handle, center, step, 0.55f);
            if (DiseaseCultureBoard.IsColony(colony))
                handle.DrawCircle(center, step * 0.42f, DiseaseMicroscopePalette.Removed, false);
        }
    }

    private static void DrawSterile(DrawingHandleScreen handle, Vector2 center, float step, float alpha)
    {
        var color = DiseaseMicroscopePalette.Sterile.WithAlpha(alpha);
        var arm = step * 0.26f;
        handle.DrawRect(UIBox2.FromDimensions(center - new Vector2(step * 0.4f), new Vector2(step * 0.8f)), color.WithAlpha(alpha * 0.18f));
        handle.DrawLine(center - new Vector2(arm), center + new Vector2(arm), color);
        handle.DrawLine(center + new Vector2(-arm, arm), center + new Vector2(arm, -arm), color);
    }

    private void SetHover(Vector2 position)
    {
        GetField(out var start, out var step);
        if (step <= 0f)
            return;

        var grid = (position - start) / step;
        var cell = new Vector2i((int)MathF.Floor(grid.X), (int)MathF.Floor(grid.Y));

        // Пустая чашка не реагирует на курсор: без партии показывать и применять нечего.
        Vector2i? hovered = _cells.Length == DiseaseCultureBoard.CellCount && DiseaseCultureBoard.IsInside(cell.X, cell.Y)
            ? cell
            : null;
        if (HoveredCell == hovered)
            return;

        HoveredCell = hovered;
        RebuildPreview();
    }

    private void RebuildPreview()
    {
        _area.Clear();
        _preview = [];
        _forecast = [];
        _forecastMoves = new();
        if (HoveredCell is { } cell)
        {
            _area = DiseaseCultureBoard.GetArea(Tool, cell.X, cell.Y, _profile);
            _preview = DiseaseCultureBoard.Apply(_cells, Tool, cell.X, cell.Y, _profile);

            // Сервер отвечает с номером уже сделанного хода, поэтому прогноз берёт следующий.
            // Бесполезную каплю сервер не примет, и отвечать на неё конкуренту не придётся.
            if (_forecastActive && _preview.Length == _cells.Length && !SameCells(_preview, _cells))
                _forecast = DiseaseCultureOpponent.Respond(_preview, _profile, unchecked(_turn + 1), out _forecastMoves);
        }

        OnPreviewChanged?.Invoke();
    }

    private static bool SameCells(byte[] first, byte[] second)
    {
        if (first.Length != second.Length)
            return false;

        for (var i = 0; i < first.Length; i++)
        {
            if (first[i] != second[i])
                return false;
        }

        return true;
    }

    private void GetField(out Vector2 start, out float step)
    {
        var side = Math.Min(PixelWidth, PixelHeight);
        var origin = (PixelSize - new Vector2(side)) * 0.5f;
        start = origin + new Vector2(side * 0.09f);
        step = side * 0.82f / DiseaseCultureBoard.Size;
    }

    private Color GetStateColor()
    {
        return PreviewState switch
        {
            DiseaseCulturePreview.Allowed => Palettes.Green.Base,
            DiseaseCulturePreview.Caution => Palettes.Amber.Base,
            DiseaseCulturePreview.Blocked => Palettes.Red.Base,
            _ => DiseaseMicroscopePalette.Marking,
        };
    }

    private void DrawFootprint(DrawingHandleScreen handle, Vector2 start, float step)
    {
        var color = GetStateColor();
        foreach (var index in _area)
        {
            var position = start + new Vector2(index % DiseaseCultureBoard.Size, index / DiseaseCultureBoard.Size) * step;
            var box = UIBox2.FromDimensions(position + new Vector2(1f), new Vector2(step - 2f));
            handle.DrawRect(box, color.WithAlpha(0.05f));
            handle.DrawRect(box, color.WithAlpha(InteractionEnabled ? 0.5f : 0.18f), false);
        }

        if (HoveredCell is not { } cell)
            return;

        var tip = start + new Vector2(cell.X + 0.5f, cell.Y + 0.5f) * step;
        var end = tip + new Vector2(step * 0.65f, -step * 1.1f);
        handle.DrawLine(tip, end, color);
        handle.DrawCircle(end, step * 0.12f, color);
    }

    private void DrawPulse(DrawingHandleScreen handle, Vector2 start, float step)
    {
        if (_pulse >= 1f)
            return;

        var center = start + new Vector2(_pulseCell.X + 0.5f, _pulseCell.Y + 0.5f) * step;
        var color = _pulseTool == DiseaseMicroscopeTool.Inhibitor
            ? DiseaseMicroscopePalette.Removed
            : DiseaseMicroscopePalette.Colony(DiseaseCultureBoard.Target);
        handle.DrawCircle(center, step * (0.2f + _pulse * 1.7f), color.WithAlpha(1f - _pulse), false);
        handle.DrawCircle(center, step * 0.12f * (1f - _pulse), color.WithAlpha(1f - _pulse));
    }

    private static void DrawColony(DrawingHandleScreen handle, Vector2 center, float radius, byte colony, float alpha)
    {
        // Колонии различаются только цветом: зелёная ваша, красная конкурента.
        var color = DiseaseMicroscopePalette.Colony(colony);
        handle.DrawCircle(center, radius * 1.28f, color.WithAlpha(alpha * 0.08f));
        handle.DrawCircle(center, radius, color.WithAlpha(alpha * 0.75f));
        handle.DrawCircle(center, radius * 0.45f, color.WithAlpha(alpha * 0.95f));
    }

    private void DrawConnections(DrawingHandleScreen handle, int index, Vector2 center, float step, byte colony)
    {
        var color = DiseaseMicroscopePalette.Colony(colony).WithAlpha(0.3f);
        if (index % DiseaseCultureBoard.Size < DiseaseCultureBoard.Size - 1 && index + 1 < _cells.Length && _cells[index + 1] == colony)
            handle.DrawRect(UIBox2.FromDimensions(center - new Vector2(0, step * 0.12f), new Vector2(step, step * 0.24f)), color);
        if (index + DiseaseCultureBoard.Size < _cells.Length && _cells[index + DiseaseCultureBoard.Size] == colony)
            handle.DrawRect(UIBox2.FromDimensions(center - new Vector2(step * 0.12f, 0), new Vector2(step * 0.24f, step)), color);
    }
}

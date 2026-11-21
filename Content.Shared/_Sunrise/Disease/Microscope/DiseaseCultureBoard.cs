namespace Content.Shared._Sunrise.Disease.Microscope;

/// <summary>
/// Детерминированные операции над культурой, общие для серверной авторитетной логики и предпросмотра на клиенте.
/// На поле ровно две колонии: целевая и колония конкурента, которую развить нельзя.
/// </summary>
public static class DiseaseCultureBoard
{
    /// <summary>Сторона квадрата, в который вписано круглое поле.</summary>
    public const int Size = 12;

    /// <summary>Число клеток поля вместе с углами за пределами круга.</summary>
    public const int CellCount = Size * Size;

    /// <summary>Клетка развиваемой колонии.</summary>
    public const byte Target = 1;

    /// <summary>Клетка колонии конкурента.</summary>
    public const byte Opponent = 2;

    /// <summary>Значение выжженной клетки: на ней уже ничего не вырастет.</summary>
    public const byte Sterile = byte.MaxValue;

    private const int Middle = (Size - 1) / 2;

    private static readonly (int X, int Y)[] Neighbors = [(0, -1), (-1, 0), (1, 0), (0, 1)];

    // Посев берёт одну из форм, поэтому поле не повторяется от партии к партии.
    private static readonly (int X, int Y)[][] Shapes =
    [
        [(0, 0), (-1, 0), (1, 0), (0, 1)],
        [(0, 0), (1, 0), (0, 1), (1, 1)],
        [(0, 0), (1, 0), (2, 0), (1, 1)],
        [(0, 0), (0, 1), (1, 1), (1, 2)],
        [(0, 0), (1, 0), (1, 1), (2, 1)],
        [(0, 0), (-1, 0), (1, 0), (1, 1)],
    ];

    /// <summary>Живая ли клетка колонии: выжженная и пустая среда колониями не считаются.</summary>
    public static bool IsColony(byte cell)
    {
        return cell == Target || cell == Opponent;
    }

    /// <summary>Лежит ли координата внутри круглой культуры.</summary>
    public static bool IsInside(int x, int y)
    {
        if (x < 0 || y < 0 || x >= Size || y >= Size)
            return false;

        var dx = x * 2 - (Size - 1);
        var dy = y * 2 - (Size - 1);
        return dx * dx + dy * dy <= (Size - 1) * (Size - 1);
    }

    /// <summary>Индекс клетки по координатам.</summary>
    public static int GetIndex(int x, int y)
    {
        return y * Size + x;
    }

    /// <summary>
    /// Засевает поле по номеру посева: цель садится у середины, конкурент — на отдалении,
    /// которое сокращается с ростом сложности.
    /// </summary>
    public static byte[] Create(int seed, DiseaseCultureProfile profile)
    {
        var cells = new byte[CellCount];
        if (!profile.IsValid)
            return cells;

        var random = new System.Random(seed);
        var free = new List<int>(CellCount);
        for (var index = 0; index < CellCount; index++)
        {
            if (IsInside(index % Size, index / Size))
                free.Add(index);
        }

        for (var i = free.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (free[i], free[j]) = (free[j], free[i]);
        }

        var target = PickCenter(free, cells, null, 0);
        PlaceColony(cells, random, target, Target, 3);

        // Чем выше стадия, тем ближе садится конкурент и тем меньше места у цели.
        var distance = Math.Max(2, 7 - profile.Tier);

        // На поздних стадиях у конкурента два раздельных очага: один щелчок ингибитора его уже не снимает.
        var patches = profile.Tier >= 3 ? 2 : 1;
        var rivals = new List<(int X, int Y)>(patches);
        for (var patch = 0; patch < patches; patch++)
        {
            var rival = PickRival(free, cells, target, rivals, distance);
            rivals.Add(rival);
            PlaceColony(cells, random, rival, Opponent, 4);
        }

        return cells;
    }

    /// <summary>Клетки под выбранным инструментом.</summary>
    public static List<int> GetArea(DiseaseMicroscopeTool tool, int x, int y, DiseaseCultureProfile profile)
    {
        var area = new List<int>();
        if (!IsInside(x, y) || !Enum.IsDefined(tool))
            return area;

        switch (tool)
        {
            // Точечный ингибитор высокой стадии бьёт крестом: широкая зачистка недоступна.
            case DiseaseMicroscopeTool.Inhibitor when profile.NarrowInhibitor:
                AddCross(area, x, y);
                break;
            default:
                AddBox(area, x, y);
                break;
        }

        return area;
    }

    /// <summary>
    /// Возвращает новое поле после одного хода игрока. Рост читает исходное поле,
    /// поэтому один щелчок продвигает ровно одно поколение и никогда не разрешает спор соседей случайно.
    /// </summary>
    public static byte[] Apply(
        IReadOnlyList<byte> cells,
        DiseaseMicroscopeTool tool,
        int x,
        int y,
        DiseaseCultureProfile profile)
    {
        if (cells.Count != CellCount)
            return [];

        var result = new byte[CellCount];
        for (var i = 0; i < CellCount; i++)
            result[i] = cells[i];

        if (!IsInside(x, y) || !Enum.IsDefined(tool))
            return result;

        switch (tool)
        {
            case DiseaseMicroscopeTool.Nutrient:
                Grow(cells, result, x, y, profile);
                break;
            case DiseaseMicroscopeTool.Inhibitor:
                Inhibit(cells, result, x, y, profile);
                break;
        }

        return result;
    }

    /// <summary>Считает занятые клетки конкретной колонии.</summary>
    public static int CountColony(IReadOnlyList<byte> cells, byte colony)
    {
        var count = 0;
        foreach (var cell in cells)
        {
            if (cell == colony)
                count++;
        }

        return count;
    }

    /// <summary>Колония, которая может занять пустую клетку: на границе двух колоний рост останавливается.</summary>
    public static byte GetGrowth(IReadOnlyList<byte> cells, int x, int y)
    {
        byte candidate = 0;
        foreach (var neighbor in Neighbors)
        {
            var nx = x + neighbor.X;
            var ny = y + neighbor.Y;
            if (!IsInside(nx, ny) || !IsColony(cells[GetIndex(nx, ny)]))
                continue;

            var colony = cells[GetIndex(nx, ny)];
            if (candidate != 0 && candidate != colony)
                return 0;

            candidate = colony;
        }

        return candidate;
    }

    /// <summary>Соседние клетки по сторонам света, лежащие внутри чашки.</summary>
    public static IEnumerable<int> GetNeighbors(int index)
    {
        var x = index % Size;
        var y = index / Size;
        foreach (var neighbor in Neighbors)
        {
            var nx = x + neighbor.X;
            var ny = y + neighbor.Y;
            if (IsInside(nx, ny))
                yield return GetIndex(nx, ny);
        }
    }

    /// <summary>Расстояние между клетками по сторонам света.</summary>
    public static int GetDistance(int first, int second)
    {
        return Math.Abs(first % Size - second % Size) + Math.Abs(first / Size - second / Size);
    }

    private static void Grow(IReadOnlyList<byte> cells, byte[] result, int x, int y, DiseaseCultureProfile profile)
    {
        for (var dy = -1; dy <= 1; dy++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                var cellX = x + dx;
                var cellY = y + dy;
                if (!IsInside(cellX, cellY) || cells[GetIndex(cellX, cellY)] != 0)
                    continue;

                var colony = GetGrowth(cells, cellX, cellY);
                if (colony == 0)
                    continue;

                // На поздних стадиях цель тянется только по кресту капли, а конкурент — по всей области.
                if (colony == Target && profile.TargetCross && Math.Abs(dx) + Math.Abs(dy) > 1)
                    continue;

                result[GetIndex(cellX, cellY)] = colony;
            }
        }
    }

    /// <summary>
    /// Применяет ингибитор: обработанная среда выжигается, а плотное ядро конкурента держится
    /// за клетки вне участка и переживает обработку.
    /// </summary>
    private static void Inhibit(IReadOnlyList<byte> cells, byte[] result, int x, int y, DiseaseCultureProfile profile)
    {
        var area = GetArea(DiseaseMicroscopeTool.Inhibitor, x, y, profile);
        foreach (var index in area)
        {
            // Целевая колония защиты не имеет: хлор рядом с ней всегда опасен.
            if (profile.ResilientOpponent && cells[index] == Opponent && CountAnchors(cells, area, index) >= 2)
                continue;

            // Хлор не просто убирает клетки: обработанная среда мертва и больше ничего не вырастит.
            result[index] = Sterile;
        }
    }

    private static int CountAnchors(IReadOnlyList<byte> cells, List<int> area, int index)
    {
        var count = 0;
        foreach (var neighbor in GetNeighbors(index))
        {
            if (cells[neighbor] == Opponent && !area.Contains(neighbor))
                count++;
        }

        return count;
    }

    /// <summary>
    /// Выбирает место очага конкурента: на нужном отдалении от цели и не вплотную к другим очагам,
    /// чтобы второй очаг нельзя было накрыть тем же щелчком ингибитора.
    /// </summary>
    private static (int X, int Y) PickRival(
        List<int> free,
        byte[] cells,
        (int X, int Y) target,
        List<(int X, int Y)> rivals,
        int distance)
    {
        const int apart = 5;
        for (var slack = 0; slack <= Math.Max(distance, apart); slack++)
        {
            foreach (var index in free)
            {
                var x = index % Size;
                var y = index / Size;
                if (cells[index] != 0)
                    continue;

                var toTarget = Math.Abs(x - target.X) + Math.Abs(y - target.Y);
                if (toTarget < distance - slack || toTarget > distance + slack + 2)
                    continue;

                // Отдаление очагов друг от друга не ослабляется: иначе они срастутся в один участок.
                var fits = true;
                foreach (var other in rivals)
                {
                    if (Math.Abs(x - other.X) + Math.Abs(y - other.Y) >= apart)
                        continue;

                    fits = false;
                    break;
                }

                if (fits)
                    return (x, y);
            }
        }

        return PickCenter(free, cells, target, distance);
    }

    private static (int X, int Y) PickCenter(List<int> free, byte[] cells, (int X, int Y)? anchor, int distance)
    {
        for (var slack = 0; slack <= Math.Max(distance, 3); slack++)
        {
            foreach (var index in free)
            {
                var x = index % Size;
                var y = index / Size;
                if (cells[index] != 0)
                    continue;

                if (anchor is not { } other)
                {
                    // Цель садится ближе к середине чашки, чтобы ей было куда расти.
                    if (Math.Max(Math.Abs(x - Middle), Math.Abs(y - Middle)) <= 2 + slack)
                        return (x, y);

                    continue;
                }

                var apart = Math.Abs(x - other.X) + Math.Abs(y - other.Y);
                if (apart >= distance - slack && apart <= distance + slack + 2)
                    return (x, y);
            }
        }

        foreach (var index in free)
        {
            if (cells[index] == 0)
                return (index % Size, index / Size);
        }

        return (Middle, Middle);
    }

    private static void PlaceColony(byte[] cells, System.Random random, (int X, int Y) center, byte colony, int limit)
    {
        var shape = Shapes[random.Next(Shapes.Length)];
        var turns = random.Next(4);
        var mirror = random.Next(2) == 0;
        var placed = 0;
        foreach (var offset in shape)
        {
            if (placed >= limit)
                break;

            var dx = mirror ? -offset.X : offset.X;
            var dy = offset.Y;
            for (var turn = 0; turn < turns; turn++)
                (dx, dy) = (-dy, dx);

            var x = center.X + dx;
            var y = center.Y + dy;

            // Соседние посевы не затирают друг друга, иначе колония могла бы исчезнуть с поля целиком.
            if (!IsInside(x, y) || cells[GetIndex(x, y)] != 0)
                continue;

            cells[GetIndex(x, y)] = colony;
            placed++;
        }
    }

    private static void AddBox(List<int> area, int x, int y)
    {
        for (var dy = -1; dy <= 1; dy++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                if (IsInside(x + dx, y + dy))
                    area.Add(GetIndex(x + dx, y + dy));
            }
        }
    }

    private static void AddCross(List<int> area, int x, int y)
    {
        area.Add(GetIndex(x, y));
        foreach (var neighbor in GetNeighbors(GetIndex(x, y)))
            area.Add(neighbor);
    }
}

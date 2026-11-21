namespace Content.Shared._Sunrise.Disease.Microscope;

/// <summary>
/// Ходы конкурирующей колонии. Конкурент отвечает на каждое действие игрока: занимает среду
/// по направлению к цели, вытесняет её клетки на границе и травит путь роста.
/// </summary>
/// <remarks>
/// Ответ детерминирован по состоянию поля и номеру хода, поэтому сервер и тесты получают
/// одинаковый результат, а игрок не может переиграть один и тот же ход с другим исходом.
/// </remarks>
public static class DiseaseCultureOpponent
{
    /// <summary>Выполняет ответные ходы конкурента и возвращает новое поле.</summary>
    /// <param name="cells">Поле после хода игрока.</param>
    /// <param name="profile">Условия партии текущей стадии.</param>
    /// <param name="turn">Номер хода, чтобы одинаковые позиции не разыгрывались одинаково.</param>
    /// <param name="moves">Клетки, которые конкурент изменил: по ним окно подсвечивает его ответ.</param>
    public static byte[] Respond(
        IReadOnlyList<byte> cells,
        DiseaseCultureProfile profile,
        int turn,
        out List<int> moves)
    {
        moves = new List<int>();
        var result = new byte[cells.Count];
        for (var i = 0; i < cells.Count; i++)
            result[i] = cells[i];

        if (cells.Count != DiseaseCultureBoard.CellCount || !profile.IsValid)
            return result;

        for (var move = 0; move < profile.OpponentMoves; move++)
        {
            if (TryMove(result, profile, turn, move, out var index))
                moves.Add(index);
            else
                break;
        }

        return result;
    }

    private static bool TryMove(byte[] cells, DiseaseCultureProfile profile, int turn, int move, out int index)
    {
        index = 0;
        var target = Collect(cells, DiseaseCultureBoard.Target);
        var opponent = Collect(cells, DiseaseCultureBoard.Opponent);
        if (target.Count == 0)
            return false;

        // В культуре остаются спящие споры: выжечь конкурента целиком одним щелчком не выйдет,
        // он заново прорастает подальше от цели, а среда уже испорчена.
        if (opponent.Count == 0)
        {
            if (!TrySpore(cells, target, turn, move, out index))
                return false;

            cells[index] = DiseaseCultureBoard.Opponent;
            return true;
        }

        // Наступление: клетка цели на стыке колоний переходит конкуренту.
        // Вытеснение идёт не чаще раза за ответ, иначе цель теряла бы клетки быстрее, чем растёт.
        if (profile.OpponentDisplace && move == 0 && TryDisplace(cells, target, turn, move, out index))
        {
            cells[index] = DiseaseCultureBoard.Opponent;
            return true;
        }

        // Токсин идёт в ход на финишной прямой, когда цель почти добрала норму.
        if (profile.OpponentToxin && target.Count * 5 >= profile.RequiredGrowth * 4 &&
            TryPoison(cells, target, opponent, turn, move, out index))
        {
            cells[index] = DiseaseCultureBoard.Sterile;
            return true;
        }

        if (TryGrow(cells, target, opponent, turn, move, out index))
        {
            cells[index] = DiseaseCultureBoard.Opponent;
            return true;
        }

        return false;
    }

    /// <summary>Проращивает спору в свободной среде подальше от цели.</summary>
    private static bool TrySpore(byte[] cells, List<int> target, int turn, int move, out int index)
    {
        index = 0;
        var candidates = new List<int>();
        var best = 0;
        for (var cell = 0; cell < cells.Length; cell++)
        {
            if (cells[cell] != 0 ||
                !DiseaseCultureBoard.IsInside(cell % DiseaseCultureBoard.Size, cell / DiseaseCultureBoard.Size))
            {
                continue;
            }

            var distance = GetDistance(cell, target);
            if (distance < best)
                continue;

            if (distance > best)
            {
                best = distance;
                candidates.Clear();
            }

            candidates.Add(cell);
        }

        return TryPick(cells, candidates, turn, move, out index);
    }

    private static bool TryDisplace(byte[] cells, List<int> target, int turn, int move, out int index)
    {
        index = 0;
        var candidates = new List<int>();
        var best = 0;
        foreach (var cell in target)
        {
            var pressure = 0;
            foreach (var neighbor in DiseaseCultureBoard.GetNeighbors(cell))
            {
                if (cells[neighbor] == DiseaseCultureBoard.Opponent)
                    pressure++;
            }

            if (pressure == 0 || pressure < best)
                continue;

            // Конкурент давит там, где у него больше клеток на стыке, а не в случайном месте.
            if (pressure > best)
            {
                best = pressure;
                candidates.Clear();
            }

            candidates.Add(cell);
        }

        return TryPick(cells, candidates, turn, move, out index);
    }

    private static bool TryPoison(byte[] cells, List<int> target, List<int> opponent, int turn, int move, out int index)
    {
        index = 0;
        var candidates = new List<int>();
        var best = int.MaxValue;
        foreach (var cell in target)
        {
            foreach (var neighbor in DiseaseCultureBoard.GetNeighbors(cell))
            {
                if (cells[neighbor] != 0)
                    continue;

                var distance = GetDistance(neighbor, opponent);
                if (distance > best)
                    continue;

                if (distance < best)
                {
                    best = distance;
                    candidates.Clear();
                }

                if (!candidates.Contains(neighbor))
                    candidates.Add(neighbor);
            }
        }

        return TryPick(cells, candidates, turn, move, out index);
    }

    private static bool TryGrow(byte[] cells, List<int> target, List<int> opponent, int turn, int move, out int index)
    {
        index = 0;
        var candidates = new List<int>();
        var best = int.MaxValue;
        foreach (var cell in opponent)
        {
            foreach (var neighbor in DiseaseCultureBoard.GetNeighbors(cell))
            {
                // Спорные клетки конкурент занять не может: на стыке колоний рост стоит.
                if (cells[neighbor] != 0 ||
                    DiseaseCultureBoard.GetGrowth(cells, neighbor % DiseaseCultureBoard.Size, neighbor / DiseaseCultureBoard.Size)
                    != DiseaseCultureBoard.Opponent)
                {
                    continue;
                }

                var distance = GetDistance(neighbor, target);
                if (distance > best)
                    continue;

                if (distance < best)
                {
                    best = distance;
                    candidates.Clear();
                }

                if (!candidates.Contains(neighbor))
                    candidates.Add(neighbor);
            }
        }

        return TryPick(cells, candidates, turn, move, out index);
    }

    private static bool TryPick(IReadOnlyList<byte> cells, List<int> candidates, int turn, int move, out int index)
    {
        index = 0;
        if (candidates.Count == 0)
            return false;

        candidates.Sort();
        index = candidates[GetPick(cells, turn, move) % candidates.Count];
        return true;
    }

    private static int GetPick(IReadOnlyList<byte> cells, int turn, int move)
    {
        // Выбор считается от состояния поля: перезаход в окно не даёт разыграть ход заново.
        var hash = 17;
        for (var i = 0; i < cells.Count; i++)
            hash = unchecked(hash * 31 + cells[i]);

        hash = unchecked(hash * 31 + turn);
        hash = unchecked(hash * 31 + move);
        return hash & int.MaxValue;
    }

    private static int GetDistance(int index, List<int> cells)
    {
        var best = int.MaxValue;
        foreach (var cell in cells)
            best = Math.Min(best, DiseaseCultureBoard.GetDistance(index, cell));

        return best;
    }

    private static List<int> Collect(IReadOnlyList<byte> cells, byte colony)
    {
        var found = new List<int>();
        for (var index = 0; index < cells.Count; index++)
        {
            if (cells[index] == colony)
                found.Add(index);
        }

        return found;
    }
}

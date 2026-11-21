using Robust.Shared.Serialization;

namespace Content.Shared._Sunrise.Disease.Microscope;

/// <summary>Дополнительные правила партии, включаемые стадией развития штамма.</summary>
[Flags, Serializable, NetSerializable]
public enum DiseaseCultureRules : byte
{
    None = 0,

    /// <summary>Целевая колония растёт только по кресту капли, конкурент — по всей области.</summary>
    TargetCross = 1 << 0,

    /// <summary>Ингибитор бьёт крестом из пяти клеток вместо квадрата.</summary>
    NarrowInhibitor = 1 << 1,

    /// <summary>Клетка конкурента переживает ингибитор, если держится за две свои клетки вне участка.</summary>
    ResilientOpponent = 1 << 2,

    /// <summary>Конкурент вытесняет клетки цели на границе, а не только занимает пустую среду.</summary>
    OpponentDisplace = 1 << 3,

    /// <summary>Конкурент выжигает среду перед фронтом цели, закрывая ей путь.</summary>
    OpponentToxin = 1 << 4,
}

/// <summary>
/// Условия партии на одной стадии развития штамма. Сервер и клиент читают один профиль,
/// поэтому предпросмотр хода в окне совпадает с тем, что сделает машина.
/// </summary>
/// <param name="RequiredShare">
/// Доля целевой колонии среди всех живых клеток поля в процентах, без которой стадию не закрыть.
/// </param>
[Serializable, NetSerializable]
public readonly record struct DiseaseCultureProfile(
    byte Tier,
    byte RequiredGrowth,
    byte OpponentMoves,
    byte NutrientCost,
    byte InhibitorCost,
    DiseaseCultureRules Rules,
    byte RequiredShare = 0)
{
    /// <summary>Число уровней сложности: по одному на стадию развития плюс надбавка за устойчивость.</summary>
    public const byte MaximumTier = 5;

    /// <summary>Профиль без образца: партия не идёт.</summary>
    public static readonly DiseaseCultureProfile Empty = default;

    /// <inheritdoc cref="DiseaseCultureRules.TargetCross"/>
    public bool TargetCross => (Rules & DiseaseCultureRules.TargetCross) != 0;

    /// <inheritdoc cref="DiseaseCultureRules.NarrowInhibitor"/>
    public bool NarrowInhibitor => (Rules & DiseaseCultureRules.NarrowInhibitor) != 0;

    /// <inheritdoc cref="DiseaseCultureRules.ResilientOpponent"/>
    public bool ResilientOpponent => (Rules & DiseaseCultureRules.ResilientOpponent) != 0;

    /// <inheritdoc cref="DiseaseCultureRules.OpponentDisplace"/>
    public bool OpponentDisplace => (Rules & DiseaseCultureRules.OpponentDisplace) != 0;

    /// <inheritdoc cref="DiseaseCultureRules.OpponentToxin"/>
    public bool OpponentToxin => (Rules & DiseaseCultureRules.OpponentToxin) != 0;

    /// <summary>Заполнен ли профиль и укладываются ли его требования в размеры поля.</summary>
    public bool IsValid =>
        Tier >= 1 && Tier <= MaximumTier &&
        RequiredGrowth >= 1 && RequiredGrowth <= DiseaseCultureBoard.CellCount &&
        OpponentMoves >= 1 && OpponentMoves <= 4 &&
        NutrientCost >= 1 && InhibitorCost >= 1 &&
        RequiredShare <= 100;

    /// <summary>
    /// Условия партии для стадии развития: чем дальше геном, тем больше нужно вырастить
    /// и тем чаще отвечает конкурент. Устойчивый штамм добавляет ступень сложности.
    /// </summary>
    /// <param name="stage">Номер развиваемой стадии, считая с нуля.</param>
    /// <param name="resistance">Устойчивость заготовки от нуля до единицы.</param>
    public static DiseaseCultureProfile FromStage(int stage, float resistance)
    {
        if (stage < 0)
            return Empty;

        var stability = float.IsFinite(resistance) ? Math.Clamp(resistance, 0f, 1f) : 0f;
        var level = Math.Clamp(stage + (stability >= 0.35f ? 1 : 0), 0, MaximumTier - 1);

        // Требуемая доля растёт вместе со стадией: на поздних партиях конкурента приходится держать в узде,
        // а не просто обгонять.
        return level switch
        {
            // Первая стадия уже требует внимания: конкурент сразу теснит цель на стыке колоний.
            0 => new DiseaseCultureProfile(1, 9, 1, 1, 1, DiseaseCultureRules.OpponentDisplace, 60),
            1 => new DiseaseCultureProfile(2, 11, 1, 1, 1,
                DiseaseCultureRules.OpponentDisplace | DiseaseCultureRules.ResilientOpponent, 60),
            2 => new DiseaseCultureProfile(3, 13, 2, 1, 2,
                DiseaseCultureRules.OpponentDisplace | DiseaseCultureRules.ResilientOpponent |
                DiseaseCultureRules.TargetCross, 65),
            3 => new DiseaseCultureProfile(4, 15, 2, 2, 2,
                DiseaseCultureRules.OpponentDisplace | DiseaseCultureRules.ResilientOpponent |
                DiseaseCultureRules.TargetCross | DiseaseCultureRules.NarrowInhibitor, 70),
            // Последняя стадия добавляет токсин, но отвечает не чаще предыдущей: иначе цель не растёт.
            _ => new DiseaseCultureProfile(5, 17, 2, 2, 2,
                DiseaseCultureRules.OpponentDisplace | DiseaseCultureRules.ResilientOpponent |
                DiseaseCultureRules.TargetCross | DiseaseCultureRules.NarrowInhibitor |
                DiseaseCultureRules.OpponentToxin, 70),
        };
    }

    /// <summary>Доля целевой колонии среди живых клеток поля в процентах, с округлением вниз.</summary>
    /// <param name="target">Клетки целевой колонии.</param>
    /// <param name="opponent">Клетки конкурента.</param>
    public static int GetShare(int target, int opponent)
    {
        var total = Math.Max(0, target) + Math.Max(0, opponent);
        return total <= 0 ? 0 : Math.Max(0, target) * 100 / total;
    }

    /// <summary>Набрала ли целевая колония норму клеток стадии.</summary>
    public bool IsGrowthReached(int target)
    {
        return IsValid && target >= RequiredGrowth;
    }

    /// <summary>
    /// Сколько клеток нужно целевой колонии при таком конкуренте, чтобы стадия закрылась.
    /// Оба условия партии сведены к одному числу: это и норма стадии, и нужная доля поля.
    /// </summary>
    /// <param name="opponent">Клетки конкурента на поле.</param>
    public int GetRequiredTarget(int opponent)
    {
        if (!IsValid)
            return DiseaseCultureBoard.CellCount + 1;

        var rival = Math.Max(0, opponent);
        if (RequiredShare >= 100)
            return rival > 0 ? DiseaseCultureBoard.CellCount + 1 : RequiredGrowth;

        // Наименьшее t, при котором t * 100 >= RequiredShare * (t + rival): деление с округлением вверх.
        var byShare = (RequiredShare * rival + (100 - RequiredShare) - 1) / (100 - RequiredShare);
        return Math.Max(RequiredGrowth, byShare);
    }

    /// <summary>Держит ли целевая колония нужную долю поля против конкурента.</summary>
    public bool IsShareReached(int target, int opponent)
    {
        // Сравнение в целых числах: округление процента не должно пропускать партию на волосок ниже порога.
        return IsValid && target > 0 && target * 100 >= RequiredShare * (target + Math.Max(0, opponent));
    }

    /// <summary>
    /// Пройдена ли партия: целевая колония набрала норму стадии и держит нужную долю поля.
    /// </summary>
    /// <param name="target">Клетки целевой колонии на поле.</param>
    /// <param name="opponent">Клетки конкурента на поле.</param>
    public bool IsReached(int target, int opponent)
    {
        return IsGrowthReached(target) && IsShareReached(target, opponent);
    }
}

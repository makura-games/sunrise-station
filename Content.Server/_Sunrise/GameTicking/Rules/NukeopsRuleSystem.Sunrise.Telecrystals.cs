using Content.Shared.FixedPoint;

// ReSharper disable once CheckNamespace
namespace Content.Server.GameTicking.Rules;

public sealed partial class NukeopsRuleSystem
{
    private const int CommanderBaseTc = 40;
    private const int CommanderTcPerFighter = 5;
    private const int CommanderWarTcPerOperative = 40;

    private static FixedPoint2 GetCommanderTcPerFighter()
    {
        return CommanderTcPerFighter;
    }

    private static FixedPoint2 GetCommanderStartupTc(int fightersCount)
    {
        return CommanderBaseTc + CommanderTcPerFighter * fightersCount;
    }

    private static FixedPoint2 GetCommanderWarBonusTc(int operativesCount)
    {
        return CommanderWarTcPerOperative * operativesCount;
    }
}

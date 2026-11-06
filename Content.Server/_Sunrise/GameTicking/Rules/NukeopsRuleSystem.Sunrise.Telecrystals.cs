using Content.Shared.FixedPoint;

#pragma warning disable IDE0130
namespace Content.Server.GameTicking.Rules;

public sealed partial class NukeopsRuleSystem
{
    private const int CommanderBaseTc = 50;
    private const int CommanderTcPerFighter = 30;

    private static FixedPoint2 GetCommanderTcPerFighter()
    {
        return CommanderTcPerFighter;
    }

    private static FixedPoint2 GetCommanderStartupTc(int fightersCount)
    {
        return CommanderBaseTc + CommanderTcPerFighter * fightersCount;
    }

    private static FixedPoint2 GetCommanderWarBonusTc(int fightersCount)
    {
        return CommanderTcPerFighter * fightersCount;
    }
}

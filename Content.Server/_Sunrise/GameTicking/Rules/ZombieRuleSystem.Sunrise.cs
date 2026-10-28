using Content.Server._Sunrise.AssaultOps.Icarus;
using Content.Server.GameTicking.Rules.Components;

namespace Content.Server.GameTicking.Rules;

public sealed partial class ZombieRuleSystem
{
     // Sunrise-часть системы обрабатывает пороги заражения и запускает CBURN и Icarus.
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private IcarusTerminalSystem _icarus = default!;

    private static readonly TimeSpan ZombieIcarusBeamDelay = TimeSpan.FromSeconds(25);

    private void CheckSunriseInfectedThresholds(ZombieRuleComponent zombieRuleComponent, float infectedFraction)
    {
        TryCallCburn(zombieRuleComponent, infectedFraction);
        TryCallIcarusBeam(zombieRuleComponent, infectedFraction);
    }

    private bool TryCallCburn(ZombieRuleComponent zombieRuleComponent, float infectedFraction)
    {
        if (!CanCallCburn(zombieRuleComponent, infectedFraction))
            return false;

        DoCallCburn(zombieRuleComponent);
        return true;
    }

    private bool CanCallCburn(ZombieRuleComponent zombieRuleComponent, float infectedFraction)
    {
        return infectedFraction >= zombieRuleComponent.ZombieCburnCallPercentage && !zombieRuleComponent.CburnCalled;
    }

    private void DoCallCburn(ZombieRuleComponent zombieRuleComponent)
    {
        _gameTicker.StartGameRule(zombieRuleComponent.ZombieCburnGameRule);
        zombieRuleComponent.CburnCalled = true;
    }

    private bool TryCallIcarusBeam(ZombieRuleComponent zombieRuleComponent, float infectedFraction)
    {
        if (!CanCallIcarusBeam(zombieRuleComponent, infectedFraction))
            return false;

        DoCallIcarusBeam(zombieRuleComponent);
        return true;
    }

    private bool CanCallIcarusBeam(ZombieRuleComponent zombieRuleComponent, float infectedFraction)
    {
        return infectedFraction >= zombieRuleComponent.ZombieIcarusBeamPercentage && !zombieRuleComponent.IcarusBeamCalled;
    }

    private void DoCallIcarusBeam(ZombieRuleComponent zombieRuleComponent)
    {
        _icarus.FireBeamOnStationDelayed(ZombieIcarusBeamDelay);
        zombieRuleComponent.IcarusBeamCalled = true;
    }
}

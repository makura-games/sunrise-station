using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Timing;
using Content.Shared.Whitelist;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Sunrise.Reagents.Convert;

/// <summary>
/// Конвертирует реагенты внутри сущности по клику на нее
/// </summary>
public sealed partial class ByClickReagentConvertSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private UseDelaySystem _useDelay = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ByClickReagentConvertComponent, AfterInteractEvent>(OnInteract);
    }

    private void OnInteract(Entity<ByClickReagentConvertComponent> ent, ref AfterInteractEvent args)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        if (args.Target is not { } target || !IsPossible(ent, target, args.User))
            return;

        foreach (var (_, solution) in _solutionContainer.EnumerateSolutions((target, null)))
        {
            if (!TryConvertReagent(solution, ent.Comp.Target, ent.Comp.Result))
                continue;

            _useDelay.TryResetDelay(ent.Owner);
            _popup.PopupClient(ent.Comp.PopupMessage, target, args.User);
            _audio.PlayPvs(ent.Comp.Sound, target);
            args.Handled = true;
            return;
        }
    }

    private bool TryConvertReagent(
        Entity<SolutionComponent> solution,
        ProtoId<ReagentPrototype> sourceReagent,
        ProtoId<ReagentPrototype> resultReagent)
    {
        var contents = solution.Comp.Solution.Contents;

        // RemoveReagent использует RemoveSwap, поэтому берём найденный реагент с конца списка.
        for (var i = contents.Count - 1; i >= 0; i--)
        {
            var reagent = contents[i];
            if (reagent.Reagent.Prototype != sourceReagent)
                continue;

            var removed = _solutionContainer.RemoveReagent(solution, reagent.Reagent, reagent.Quantity);
            if (removed <= 0)
                continue;

            _solutionContainer.TryAddReagent(solution, resultReagent, removed, out var added);
            if (added < removed)
                _solutionContainer.TryAddReagent(solution, reagent.Reagent, removed - added, out _);

            return added > 0;
        }

        return false;
    }

    /// <summary>
    /// Проверяет, возможна ли конвертация?
    /// </summary>
    /// <returns>Да/Нет</returns>
    private bool IsPossible(Entity<ByClickReagentConvertComponent> ent, EntityUid target, EntityUid user)
    {
        if (!_whitelist.CheckBoth(user, ent.Comp.BlacklistUser, ent.Comp.WhitelistUser))
            return false;

        if (!_whitelist.CheckBoth(target, ent.Comp.BlacklistTarget, ent.Comp.WhitelistTarget))
            return false;

        if (_useDelay.IsDelayed(ent.Owner))
            return false;

        return true;
    }
}

using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Construction.Components;
using Content.Shared.Lathe;
using Content.Shared.UserInterface;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server._Sunrise.Access;

/// <summary>
/// Добавляет настройку доступов производственным машинам и защищает их BUI от команд без доступа.
/// </summary>
public sealed class FabricatorAccessSystem : EntitySystem
{
    [Dependency] private readonly AccessReaderSystem _access = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;

    private static readonly SoundSpecifier AccessDeniedSound =
        new SoundPathSpecifier("/Audio/Machines/custom_deny.ogg");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LatheComponent, ActivatableUIOpenAttemptEvent>(OnLatheUiOpenAttempt);
        SubscribeLocalEvent<LatheComponent, BoundUserInterfaceMessageAttempt>(OnLatheUiMessageAttempt);

        SubscribeLocalEvent<FlatpackCreatorComponent, ActivatableUIOpenAttemptEvent>(OnFlatpackerUiOpenAttempt);
        SubscribeLocalEvent<FlatpackCreatorComponent, BoundUserInterfaceMessageAttempt>(OnFlatpackerUiMessageAttempt);
    }

    private void OnLatheUiOpenAttempt(Entity<LatheComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (!TryUseFabricator((ent.Owner, null), args.User, args.Silent))
            args.Cancel();
    }

    private void OnFlatpackerUiOpenAttempt(Entity<FlatpackCreatorComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (!TryUseFabricator((ent.Owner, null), args.User, args.Silent))
            args.Cancel();
    }

    private void OnLatheUiMessageAttempt(Entity<LatheComponent> ent, ref BoundUserInterfaceMessageAttempt args)
    {
        if (!Equals(args.UiKey, LatheUiKey.Key) ||
            TryUseFabricator((ent.Owner, null), args.Actor, quiet: true))
            return;

        DenyUiMessage(ent, args);
    }

    private void OnFlatpackerUiMessageAttempt(
        Entity<FlatpackCreatorComponent> ent,
        ref BoundUserInterfaceMessageAttempt args)
    {
        if (!Equals(args.UiKey, FlatpackCreatorUIKey.Key) ||
            TryUseFabricator((ent.Owner, null), args.Actor, quiet: true))
            return;

        DenyUiMessage(ent, args);
    }

    /// <summary>
    /// Пытается разрешить пользователю работу с производственным оборудованием.
    /// </summary>
    public bool TryUseFabricator(
        Entity<AccessReaderComponent?> fabricator,
        EntityUid user,
        bool quiet = false)
    {
        return CanUseFabricator(fabricator, user, quiet);
    }

    /// <summary>
    /// Проверяет, позволяет ли настроенный доступ использовать производственное оборудование.
    /// </summary>
    public bool CanUseFabricator(
        Entity<AccessReaderComponent?> fabricator,
        EntityUid user,
        bool quiet = false)
    {
        if (!Resolve(fabricator, ref fabricator.Comp, false))
            return true;

        if (_access.IsAllowed(user, fabricator, fabricator.Comp))
            return true;

        if (!quiet)
            PlayAccessDeniedSound(fabricator, user);

        return false;
    }

    private void DenyUiMessage(EntityUid fabricator, BoundUserInterfaceMessageAttempt args)
    {
        _ui.CloseUi((fabricator, null), args.UiKey, args.Actor);
        PlayAccessDeniedSound(fabricator, args.Actor);
        args.Cancel();
    }

    private void PlayAccessDeniedSound(EntityUid fabricator, EntityUid user)
    {
        _audio.PlayEntity(AccessDeniedSound, user, fabricator);
    }
}

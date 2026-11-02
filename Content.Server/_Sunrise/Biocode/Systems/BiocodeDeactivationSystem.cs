using Content.Shared._Sunrise.Biocode.Systems;
using Content.Shared.Pinpointer;
using Content.Server.Popups;
using Content.Server.Pinpointer;

namespace Content.Server._Sunrise.Biocode.Systems;

/// <summary>
/// Server-side implementation of biocode deactivation system.
/// </summary>
public sealed partial class ServerBiocodeDeactivationSystem : BiocodeDeactivationSystem
{
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private PinpointerSystem _pinpointerSystem = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;

    private static readonly ISawmill Sawmill = Logger.GetSawmill("biocode.deactivation");

    protected override void ShowAlert(EntityUid user, string alertText)
    {
        _popup.PopupEntity(Loc.GetString(alertText), user, user);
    }

    protected override void DeactivateItem(EntityUid uid)
    {
        // Handle pinpointer deactivation using the proper system method
        if (TryComp<PinpointerComponent>(uid, out var pinpointer))
        {
            // Always ensure the pinpointer is deactivated
            _pinpointerSystem.SetActive((uid, pinpointer), false);

            // Force update appearance to ensure visual state is correct
            if (TryComp<AppearanceComponent>(uid, out var appearance))
            {
                _appearance.SetData(uid, PinpointerVisuals.IsActive, false, appearance);
                _appearance.SetData(uid, PinpointerVisuals.TargetDistance, Distance.Unknown, appearance);
            }
        }

        // Add other item types here as needed
        // Example: if (TryComp<SomeOtherComponent>(uid, out var otherComponent))
        // {
        //     _someOtherSystem.Deactivate(uid, otherComponent);
        // }
    }
}

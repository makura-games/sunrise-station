using Robust.Shared.GameStates;

namespace Content.Shared._Sunrise.Movement.Standing;

[RegisterComponent, NetworkedComponent, Access(typeof(ProneCrawlSystem))]
public sealed partial class ActiveProneCrawlVisualsComponent : Component;

using Robust.Shared.GameStates;

namespace Content.Shared.Roles.Components;

/// <summary>
/// Роль вампира.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class VampireRoleComponent : BaseMindRoleComponent;

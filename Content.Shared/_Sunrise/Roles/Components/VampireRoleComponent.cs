using Robust.Shared.GameStates;

namespace Content.Shared.Roles.Components;

/// <summary>
/// Роль разума вампира.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class VampireRoleComponent : BaseMindRoleComponent;

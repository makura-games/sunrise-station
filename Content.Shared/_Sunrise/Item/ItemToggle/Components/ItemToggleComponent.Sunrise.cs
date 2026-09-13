namespace Content.Shared.Item.ItemToggle.Components;

public sealed partial class ItemToggleComponent
{
    /// <summary>
    /// Можно ли активировать предмет, когда он находится в руке.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool CanActivateInhand = true;

    /// <summary>
    /// Нужно ли деактивировать предмет при извлечении из функциональной руки.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField]
    public bool DeactivateUnequippedHand;
}

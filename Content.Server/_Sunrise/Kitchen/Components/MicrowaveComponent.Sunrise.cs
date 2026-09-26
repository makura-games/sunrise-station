using Content.Shared.Kitchen;
using Content.Shared.Kitchen.Components;
using Robust.Shared.Audio;

#pragma warning disable IDE0130 // Namespace не соответствует расположению файла
namespace Content.Server.Kitchen.Components;

public sealed partial class MicrowaveComponent
{
    /// <summary>
    /// Типы рецептов, поддерживаемые устройством.
    /// </summary>
    [DataField]
    public MicrowaveRecipeType ValidRecipeTypes = MicrowaveRecipeType.Microwave;

    /// <summary>
    /// Указывает, что события микроволновки должны считать предмет нагреваемым.
    /// </summary>
    [DataField]
    public bool CanHeat = true;

    /// <summary>
    /// Указывает, что события микроволновки должны считать предмет облучаемым.
    /// </summary>
    [DataField]
    public bool CanIrradiate = true;

    /// <summary>
    /// Звук при отсутствии подходящего рецепта.
    /// </summary>
    [DataField]
    public SoundSpecifier NoRecipeSound = new SoundPathSpecifier("/Audio/Effects/Cargo/buzz_sigh.ogg");

    /// <summary>
    /// Ключ интерфейса, открываемого устройством.
    /// </summary>
    [DataField]
    public MicrowaveUiKey Key = MicrowaveUiKey.Key;
}

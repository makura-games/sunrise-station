using Robust.Shared.Serialization;

#pragma warning disable IDE0130 // Namespace не соответствует расположению файла
namespace Content.Shared.Kitchen;

public sealed partial class FoodRecipePrototype
{
    /// <summary>
    /// Типы устройств, в которых можно приготовить рецепт.
    /// </summary>
    [DataField]
    public MicrowaveRecipeType RecipeType = MicrowaveRecipeType.Microwave;
}

[Flags]
[Serializable, NetSerializable]
public enum MicrowaveRecipeType : byte
{
    /// <summary>
    /// Рецепт нельзя приготовить ни в одном устройстве.
    /// </summary>
    None = 0,

    /// <summary>
    /// Обычная микроволновка.
    /// </summary>
    Microwave = 1 << 0,

    /// <summary>
    /// Электрическая плита.
    /// </summary>
    ElectricRangeKey = 1 << 1,

    /// <summary>
    /// Медицинский ассемблер.
    /// </summary>
    MedicalAssembler = 1 << 2,
}

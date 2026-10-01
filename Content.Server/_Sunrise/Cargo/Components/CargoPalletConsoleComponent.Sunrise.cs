using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

#pragma warning disable IDE0130 // Namespace не соответствует расположению файла
namespace Content.Server.Cargo.Components;

[AutoGenerateComponentPause]
public sealed partial class CargoPalletConsoleComponent
{
    [DataField]
    public SoundSpecifier ErrorSound = new SoundCollectionSpecifier("CargoError");

    /// <summary>
    /// Время, после которого консоль снова сможет проиграть звук отказа.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextDenySoundTime;

    /// <summary>
    /// Минимальный интервал между звуками отказа.
    /// </summary>
    [DataField]
    public TimeSpan DenySoundDelay = TimeSpan.FromSeconds(2);
}

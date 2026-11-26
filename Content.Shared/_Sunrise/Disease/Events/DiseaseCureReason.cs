namespace Content.Shared._Sunrise.Disease.Events;

/// <summary>
/// Почему болезнь убрали с носителя.
/// </summary>
public enum DiseaseCureReason : byte
{
    /// <summary>
    /// Лечение добрало порог излечения, и началось выздоровление.
    /// </summary>
    Treatment,

    /// <summary>
    /// Болезнь отыграла последнюю стадию и прошла сама.
    /// </summary>
    Natural,

    /// <summary>
    /// Болезнь снял админ.
    /// </summary>
    Admin,

    /// <summary>
    /// Носителя возродили командой.
    /// </summary>
    Rejuvenate,
}

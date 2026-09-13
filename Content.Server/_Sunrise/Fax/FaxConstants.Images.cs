#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Server.Fax;

public static partial class FaxConstants
{
    public const string FaxPaperImageData = "fax_data_image";

    // Опечатка в сетевом ключе сохранена для совместимости с существующими пакетами факса.
    public const string FaxPaperImageScaleData = "fax_data_imgage_scale";
}

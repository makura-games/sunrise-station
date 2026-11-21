using Content.Client.Stylesheets;
using Content.Client.Stylesheets.Stylesheets;
using Robust.Client.UserInterface;
using Robust.Shared.Utility;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client._Sunrise.Disease.Microscope;

/// <summary>Отдаёт текстуру окуляра микроскопа через активную таблицу стилей.</summary>
[CommonSheetlet]
public sealed class DiseaseMicroscopeSheetlet : Sheetlet<NanotrasenStylesheet>
{
    public override StyleRule[] GetRules(NanotrasenStylesheet sheet, object config)
    {
        return
        [
            E<DiseaseCultureControl>().Prop(DiseaseCultureControl.StylePropertyOcular,
                sheet.GetTextureOr(new ResPath("_Sunrise/Disease/Microscope/ocular.png"), new ResPath("/Textures"))),
        ];
    }
}

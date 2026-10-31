using Robust.Client.GameObjects;
using Robust.Client.Graphics;

namespace Content.Client.DisplacementMap;

public sealed partial class DisplacementMapSystem
{
    private static bool TryApplySunriseShaderOverride(
        Entity<SpriteComponent> sprite,
        int index,
        ShaderInstance? shaderOverride)
    {
        if (shaderOverride is null)
            return false;

        shaderOverride.SetParameter("useDisplacement", true);
        shaderOverride.SetParameter("displacementSize", 127f);
        sprite.Comp.LayerSetShader(index, shaderOverride);
        return true;
    }
}

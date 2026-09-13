using Content.Shared.Roles;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Sunrise.Helpers;

public sealed partial class ChatIconsHelpersSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototype = default!;

    public const string NoIdIconPath = "/Textures/Interface/Misc/job_icons.rsi/NoId.png";

    /// <summary>
    /// Собирает и возвращает иконку для переданной работы
    /// </summary>
    [PublicAPI]
    public string GetJobIcon(ProtoId<JobPrototype>? job, int scale = 1)
    {
        var iconPath = _prototype.TryIndex(job, out var jobPrototype)
            ? GetJobIconPath(jobPrototype)
            : NoIdIconPath;

        var jobIcon = Loc.GetString("texture-tag",
            ("path", iconPath),
            ("scale", scale)
        );

        return jobIcon;
    }

    /// <summary>
    /// Возвращает путь к иконке работы, используя переданный прототип работы
    /// </summary>
    [PublicAPI]
    public string GetJobIconPath(JobPrototype job)
    {
        var icon = _prototype.Index(job.Icon);

        var sprite = icon.Icon switch
        {
            SpriteSpecifier.Texture tex => tex.TexturePath.CanonPath,
            SpriteSpecifier.Rsi rsi => rsi.RsiPath.CanonPath + '/' + rsi.RsiState + ".png",
            _ => NoIdIconPath,
        };

        return sprite;
    }
}

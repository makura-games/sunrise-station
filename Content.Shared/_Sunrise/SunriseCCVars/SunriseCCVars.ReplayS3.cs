using Robust.Shared.Configuration;

namespace Content.Shared._Sunrise.SunriseCCVars;

public sealed partial class SunriseCCVars
{
    /// <summary>
    ///     Whether automatic upload of replays to Wasabi S3 storage is enabled.
    /// </summary>
    public static readonly CVarDef<bool> ReplayS3UploadEnabled =
        CVarDef.Create("replay.s3_upload_enabled", false, CVar.SERVERONLY);

    /// <summary>
    ///     Wasabi S3 endpoint.
    /// </summary>
    public static readonly CVarDef<string> ReplayS3Endpoint =
        CVarDef.Create("replay.s3_endpoint", "https://s3.eu-central-1.wasabisys.com", CVar.SERVERONLY);

    /// <summary>
    ///     S3 bucket name for storing replays.
    /// </summary>
    public static readonly CVarDef<string> ReplayS3Bucket =
        CVarDef.Create("replay.s3_bucket", "makuragames-stellar-stories-replays", CVar.SERVERONLY);

    /// <summary>
    ///     Access key for authentication in Wasabi S3.
    /// </summary>
    public static readonly CVarDef<string> ReplayS3AccessKey =
        CVarDef.Create("replay.s3_access_key", "", CVar.SERVERONLY);

    /// <summary>
    ///     Secret access key for authentication in Wasabi S3.
    /// </summary>
    public static readonly CVarDef<string> ReplayS3SecretKey =
        CVarDef.Create("replay.s3_secret_key", "", CVar.SERVERONLY | CVar.CONFIDENTIAL);
}

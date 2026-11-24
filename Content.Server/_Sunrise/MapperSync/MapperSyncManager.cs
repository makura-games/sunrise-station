using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Content.Shared._Sunrise.SunriseCCVars;
using Robust.Server.ServerStatus;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._Sunrise.MapperSync;

public sealed partial class MapperSyncManager
{
    [Dependency] private IStatusHost _statusHost = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IResourceManager _res = default!;
    [Dependency] private ILogManager _logManager = default!;
    [Dependency] private IGameTiming _timing = default!;

    // Client state
    private readonly HttpClient _httpClient = new();
    private List<string> _cachedRemoteMaps = new();
    private DateTime _lastFetchTime = DateTime.MinValue;
    private bool _isFetching = false;
    private ISawmill _sawmill = default!;

    private TimeSpan _nextUpdate = TimeSpan.Zero;
    private readonly TimeSpan _updateRate = TimeSpan.FromMinutes(1);

    public void Initialize()
    {
        _sawmill = _logManager.GetSawmill("mappersync");

        _statusHost.AddHandler(HandleMapRequest);

        // Initial fetch on start
        _ = FetchMapsAsync();
    }

    public void Update()
    {
        if (_nextUpdate > _timing.CurTime)
            return;

        _nextUpdate = _timing.CurTime + _updateRate;
        _ = FetchMapsAsync();
    }

    public IReadOnlyList<string> CachedRemoteMaps => _cachedRemoteMaps;
    public DateTime LastFetchTime => _lastFetchTime;
    public bool IsFetching => _isFetching;

    /// <summary>
    /// Returns true if MapperSync (pullmap) is configured/connected.
    /// </summary>
    public bool IsEnabled => !string.IsNullOrWhiteSpace(_cfg.GetCVar(SunriseCCVars.MapperSyncServerUrl));

    /// <summary>
    /// Gets the list of available remote maps.
    /// </summary>
    public IReadOnlyList<string> GetRemoteMaps()
    {
        return _cachedRemoteMaps;
    }

    /// <summary>
    /// Manually refresh the remote maps list.
    /// </summary>
    public async Task<bool> FetchMapsAsync()
    {
        if (_isFetching) return false;
        _isFetching = true;

        var url = _cfg.GetCVar(SunriseCCVars.MapperSyncServerUrl);
        var token = _cfg.GetCVar(SunriseCCVars.MapperSyncApiToken);

        if (string.IsNullOrWhiteSpace(url))
        {
            _isFetching = false;
            return false;
        }

        try
        {
            var req = new HttpRequestMessage(HttpMethod.Get, $"{url.TrimEnd('/')}/mappersync/list");
            if (!string.IsNullOrWhiteSpace(token))
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var res = await _httpClient.SendAsync(req);
            
            if (res.IsSuccessStatusCode)
            {
                var json = await res.Content.ReadAsStringAsync();
                var list = JsonSerializer.Deserialize<List<string>>(json);
                if (list != null)
                {
                    _cachedRemoteMaps = list;
                    _lastFetchTime = DateTime.UtcNow;
                    _isFetching = false;
                    _sawmill.Debug($"MapperSync: Background fetch successful. Found {list.Count} maps.");
                    return true;
                }
            }
            else
            {
                _sawmill.Warning($"Failed to fetch remote maps in background. URL: {url}, StatusCode: {res.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            _sawmill.Error($"Exception while fetching remote maps in background from {url}: {ex}");
        }

        _isFetching = false;
        return false;
    }

    /// <summary>
    /// Downloads a remote map and saves it locally.
    /// </summary>
    public async Task<(bool Success, string Error)> DownloadMapAsync(string mapPath)
    {
        var url = _cfg.GetCVar(SunriseCCVars.MapperSyncServerUrl);
        var token = _cfg.GetCVar(SunriseCCVars.MapperSyncApiToken);

        if (string.IsNullOrWhiteSpace(url))
            return (false, "Mapper Sync Server URL is not configured (sunrise.mapper_sync.server_url).");

        try
        {
            var queryPath = Uri.EscapeDataString(mapPath);
            var req = new HttpRequestMessage(HttpMethod.Get, $"{url.TrimEnd('/')}/mappersync/download?path={queryPath}");
            if (!string.IsNullOrWhiteSpace(token))
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var res = await _httpClient.SendAsync(req);
            
            if (res.IsSuccessStatusCode)
            {
                var contentBytes = await res.Content.ReadAsByteArrayAsync();
                
                // Ensure it ends with .yml
                if (!mapPath.EndsWith(".yml", StringComparison.OrdinalIgnoreCase))
                    mapPath += ".yml";
                
                // Construct target respath
                var targetPath = new ResPath(mapPath);
                
                // Needs to be rooted to /Maps/
                if (!targetPath.ToString().StartsWith("/Maps/", StringComparison.OrdinalIgnoreCase))
                {
                    targetPath = new ResPath("/Maps") / targetPath;
                }
                
                // Ensure directory exists
                var dir = targetPath.Directory;
                _res.UserData.CreateDir(dir);
                
                // Write file (Overwrite)
                using var stream = _res.UserData.OpenWrite(targetPath);
                stream.SetLength(0); // Ensure clean overwrite
                await stream.WriteAsync(contentBytes, 0, contentBytes.Length);
                await stream.FlushAsync();
                
                return (true, string.Empty);
            }
            
            return (false, $"HTTP Error: {res.StatusCode}");
        }
        catch (Exception ex)
        {
            _sawmill.Error($"Exception while downloading remote map: {ex}");
            return (false, $"Exception: {ex.Message}");
        }
    }

    // --- SERVER SIDE HANDLERS ---
    
    private async Task<bool> HandleMapRequest(IStatusHandlerContext context)
    {
        if (!context.Url.AbsolutePath.StartsWith("/mappersync/"))
            return false;

        // Ensure expose is enabled
        if (!_cfg.GetCVar(SunriseCCVars.MapperSyncExposeMaps))
        {
            await context.RespondErrorAsync(HttpStatusCode.Forbidden);
            return true;
        }

        // Authenticate
        var token = _cfg.GetCVar(SunriseCCVars.MapperSyncApiToken);
        if (string.IsNullOrWhiteSpace(token) ||
            !context.RequestHeaders.TryGetValue("Authorization", out var authValues) ||
            authValues.Count == 0 ||
            authValues[0] != $"Bearer {token}")
        {
            _sawmill.Warning($"Unauthorized mappersync request from {context.RemoteEndPoint}");
            await context.RespondErrorAsync(HttpStatusCode.Unauthorized);
            return true;
        }

        if (context.Url.AbsolutePath == "/mappersync/list" && context.IsGetLike)
        {
            await HandleList(context);
            return true;
        }
        
        if (context.Url.AbsolutePath == "/mappersync/download" && context.IsGetLike)
        {
            await HandleDownload(context);
            return true;
        }

        return false;
    }

    private async Task HandleList(IStatusHandlerContext context)
    {
        var maps = new List<string>();

        _sawmill.Debug("MapperSync: Starting scan for maps in UserData root (ResPath.Root)...");
        FindMaps(_res.UserData, ResPath.Root, maps);
        
        _sawmill.Info($"MapperSync: Scanned UserData, found {maps.Count} maps.");

        await context.RespondJsonAsync(maps);
    }

    private static readonly string[] IgnoredDirectories = { "logs", "bin", "replays", "config", "preferences", "crash_reports", "SQLite" };

    private void FindMaps(IWritableDirProvider dirProvider, ResPath currentPath, List<string> maps)
    {
        if (!dirProvider.Exists(currentPath))
        {
            _sawmill.Debug($"MapperSync: Path does not exist: {currentPath}");
            return;
        }

        foreach (var filename in dirProvider.DirectoryEntries(currentPath))
        {
            _sawmill.Debug($"MapperSync: Found entry '{filename}' in {currentPath}");

            // Skip common large non-map directories
            if (IgnoredDirectories.Contains(filename))
                continue;

            var path = currentPath / filename;
            try
            {
                if (dirProvider.IsDir(path))
                {
                    // Recursive scan
                    FindMaps(dirProvider, path, maps);
                }
                else if (filename.EndsWith(".yml", StringComparison.OrdinalIgnoreCase))
                {
                    _sawmill.Debug($"MapperSync: Adding map: {path}");
                    maps.Add(path.ToRootedPath().ToString());
                }
            }
            catch (Exception ex)
            {
                _sawmill.Error($"MapperSync: Error processing path {path}: {ex.Message}");
            }
        }
    }

    private async Task HandleDownload(IStatusHandlerContext context)
    {
        var mapPathStr = "";
        var query = context.Url.Query;
        if (query.StartsWith("?")) query = query.Substring(1);
        var args = query.Split('&');
        foreach (var arg in args)
        {
            if (arg.StartsWith("path="))
                mapPathStr = Uri.UnescapeDataString(arg.Substring(5));
        }

        if (string.IsNullOrWhiteSpace(mapPathStr))
        {
            await context.RespondErrorAsync(HttpStatusCode.BadRequest);
            return;
        }

        // Validate that it ends with .yml
        if (!mapPathStr.EndsWith(".yml", StringComparison.OrdinalIgnoreCase))
        {
            await context.RespondErrorAsync(HttpStatusCode.Forbidden);
            return;
        }

        // Security check for directory traversal
        if (mapPathStr.Contains(".."))
        {
            await context.RespondErrorAsync(HttpStatusCode.Forbidden);
            return;
        }

        var resPath = new ResPath(mapPathStr);
        if (!resPath.IsRooted)
            resPath = new ResPath("/") / resPath;

        // Final safety check: must be a .yml and no directory traversal
        var finalPath = resPath.Clean().ToRootedPath();
        if (!finalPath.ToString().EndsWith(".yml", StringComparison.OrdinalIgnoreCase) || finalPath.ToString().Contains(".."))
        {
            await context.RespondErrorAsync(HttpStatusCode.Forbidden);
            return;
        }

        if (!_res.UserData.Exists(finalPath))
        {
            _sawmill.Warning($"Requested map not found in UserData: {finalPath}");
            await context.RespondErrorAsync(HttpStatusCode.NotFound);
            return;
        }

        try
        {
            using var fileStream = _res.UserData.OpenRead(finalPath);
            _sawmill.Debug($"MapperSync: Sending file {finalPath} ({fileStream.Length} bytes)...");

            using (var responseStream = await context.RespondStreamAsync(HttpStatusCode.OK))
            {
                await fileStream.CopyToAsync(responseStream);
                await responseStream.FlushAsync();
            }

            _sawmill.Debug($"MapperSync: Finished sending {finalPath}.");
        }
        catch (Exception ex)
        {
            _sawmill.Error($"Error downloading map {finalPath}: {ex}");
            // Context might already be closed/responded if we fail after RespondStreamAsync
            try { await context.RespondErrorAsync(HttpStatusCode.InternalServerError); } catch { /* ignore */ }
        }
    }
}

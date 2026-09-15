namespace RomM.Client;

/// <summary>
/// Maps OpenAPI paths to typed client surfaces or documents intentional allowlist coverage
/// via <see cref="IRomMTransport"/> generic send.
/// </summary>
public static class OpenApiPathCoverage
{
    /// <summary>Paths implemented by typed façades (method documentation string).</summary>
    public static IReadOnlyDictionary<string, string> TypedSurfaces { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["/api/heartbeat"] = nameof(IRomMSystemClient) + "." + nameof(IRomMSystemClient.GetHeartbeatAsync),
        ["/api/token"] = "RomMAuthHandler.OAuthPasswordGrant",
        ["/api/platforms"] = nameof(IRomMPlatformsClient) + "." + nameof(IRomMPlatformsClient.ListAsync),
        ["/api/platforms/{id}"] = nameof(IRomMPlatformsClient) + "." + nameof(IRomMPlatformsClient.GetAsync),
        ["/api/roms"] = nameof(IRomMRomsClient) + "." + nameof(IRomMRomsClient.ListAsync),
        ["/api/roms/{id}"] = nameof(IRomMRomsClient) + "." + nameof(IRomMRomsClient.GetAsync),
        ["/api/roms/{id}/content/{file_name}"] = nameof(IRomMRomsClient) + "." + nameof(IRomMRomsClient.DownloadContentAsync),
        ["/api/tasks"] = nameof(IRomMTasksClient) + "." + nameof(IRomMTasksClient.ListAsync),
        ["/api/tasks/{task_id}"] = nameof(IRomMTasksClient) + "." + nameof(IRomMTasksClient.GetStatusAsync),
        ["/api/tasks/run/{task_name}"] = nameof(IRomMTasksClient) + "." + nameof(IRomMTasksClient.RunAsync),
        ["/api/tasks/status"] = nameof(IRomMTasksClient) + "." + nameof(IRomMTasksClient.ListAsync),
    };

    /// <summary>
    /// Remaining OpenAPI paths intentionally reachable only via <see cref="IRomMTransport.SendAsync"/>
    /// until dedicated typed clients land. Core auth/platforms/roms/tasks are never listed here.
    /// </summary>
    public static string TransportAllowlistReason { get; } =
        "Covered by IRomMTransport.SendAsync for full REST reachability; typed façade deferred (P1/P2).";

    /// <summary>
    /// Snapshot paths not yet given a typed façade. New OpenAPI paths must be added here
    /// or to <see cref="TypedSurfaces"/>; a bare /api/ prefix is not coverage.
    /// </summary>
    public static IReadOnlySet<string> DeferredTransportPaths { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "/api/activity",
        "/api/activity/heartbeat",
        "/api/activity/rom/{rom_id}",
        "/api/auth/device/approve",
        "/api/auth/device/deny",
        "/api/auth/device/init",
        "/api/auth/device/pending/{user_code}",
        "/api/auth/device/token",
        "/api/client-tokens",
        "/api/client-tokens/{token_id}",
        "/api/client-tokens/{token_id}/admin",
        "/api/client-tokens/{token_id}/pair",
        "/api/client-tokens/{token_id}/regenerate",
        "/api/client-tokens/all",
        "/api/client-tokens/exchange",
        "/api/client-tokens/pair/{code}/status",
        "/api/collections",
        "/api/collections/{id}",
        "/api/collections/{id}/roms",
        "/api/collections/identifiers",
        "/api/collections/smart",
        "/api/collections/smart/{id}",
        "/api/collections/smart/identifiers",
        "/api/collections/virtual",
        "/api/collections/virtual/{id}",
        "/api/collections/virtual/identifiers",
        "/api/config",
        "/api/config/exclude",
        "/api/config/exclude/{exclusion_type}/{exclusion_value}",
        "/api/config/system/platforms",
        "/api/config/system/platforms/{fs_slug}",
        "/api/config/system/versions",
        "/api/config/system/versions/{fs_slug}",
        "/api/devices",
        "/api/devices/{device_id}",
        "/api/export/gamelist-xml",
        "/api/export/pegasus",
        "/api/feeds/fpkgi/{platform_slug}",
        "/api/feeds/kekatsu/{platform_slug}",
        "/api/feeds/pkgi/ps3/{content_type}",
        "/api/feeds/pkgi/psp/{content_type}",
        "/api/feeds/pkgi/psvita/{content_type}",
        "/api/feeds/pkgj/psp/dlc",
        "/api/feeds/pkgj/psp/games",
        "/api/feeds/pkgj/psvita/dlc",
        "/api/feeds/pkgj/psvita/games",
        "/api/feeds/pkgj/psx/games",
        "/api/feeds/tinfoil",
        "/api/feeds/webrcade",
        "/api/firmware",
        "/api/firmware/{id}",
        "/api/firmware/{id}/content/{file_name}",
        "/api/firmware/delete",
        "/api/firmware/identifiers",
        "/api/forgot-password",
        "/api/heartbeat/metadata/{source}",
        "/api/login",
        "/api/login/openid",
        "/api/logout",
        "/api/logs",
        "/api/music/albums",
        "/api/music/artists",
        "/api/music/genres",
        "/api/music/tracks",
        "/api/music/years",
        "/api/netplay/list",
        "/api/oauth/openid",
        "/api/permissions/catalog",
        "/api/permissions/groups",
        "/api/permissions/groups/{id}",
        "/api/permissions/hidden",
        "/api/permissions/me",
        "/api/permissions/users/{user_id}",
        "/api/platforms/identifiers",
        "/api/platforms/supported",
        "/api/play-sessions",
        "/api/play-sessions/{session_id}",
        "/api/reset-password",
        "/api/roms/{id}/convert-to-folder",
        "/api/roms/{id}/files",
        "/api/roms/{id}/files/content/{file_name}",
        "/api/roms/{id}/manuals",
        "/api/roms/{id}/manuals/files",
        "/api/roms/{id}/manuals/files/{file_id}",
        "/api/roms/{id}/manuals/redownload",
        "/api/roms/{id}/notes",
        "/api/roms/{id}/notes/{note_id}",
        "/api/roms/{id}/notes/identifiers",
        "/api/roms/{id}/patch",
        "/api/roms/{id}/props",
        "/api/roms/{id}/screenshots",
        "/api/roms/{id}/screenshots/{file_id}",
        "/api/roms/{id}/simple",
        "/api/roms/{id}/soundtracks",
        "/api/roms/{id}/soundtracks/{file_id}",
        "/api/roms/{id}/soundtracks/metadata",
        "/api/roms/by-hash",
        "/api/roms/by-metadata-provider",
        "/api/roms/delete",
        "/api/roms/download",
        "/api/roms/filters",
        "/api/roms/identifiers",
        "/api/roms/upload/{upload_id}",
        "/api/roms/upload/{upload_id}/cancel",
        "/api/roms/upload/{upload_id}/complete",
        "/api/roms/upload/start",
        "/api/saves",
        "/api/saves/{id}",
        "/api/saves/{id}/content",
        "/api/saves/{id}/downloaded",
        "/api/saves/{id}/track",
        "/api/saves/{id}/untrack",
        "/api/saves/{id}/visibility",
        "/api/saves/delete",
        "/api/saves/identifiers",
        "/api/saves/summary",
        "/api/screenshots",
        "/api/screenshots/{id}",
        "/api/screenshots/{id}/content",
        "/api/search/cover",
        "/api/search/roms",
        "/api/setup/library",
        "/api/setup/platforms",
        "/api/states",
        "/api/states/{id}",
        "/api/states/{id}/content",
        "/api/states/{id}/visibility",
        "/api/states/delete",
        "/api/states/identifiers",
        "/api/stats",
        "/api/sync/devices/{device_id}/push-pull",
        "/api/sync/negotiate",
        "/api/sync/sessions",
        "/api/sync/sessions/{session_id}",
        "/api/sync/sessions/{session_id}/complete",
        "/api/users",
        "/api/users/{id}",
        "/api/users/{id}/avatar",
        "/api/users/{id}/ra/refresh",
        "/api/users/identifiers",
        "/api/users/invite-link",
        "/api/users/me",
        "/api/users/register",
    };

    public static bool IsCovered(string path)
    {
        if (TypedSurfaces.ContainsKey(path))
        {
            return true;
        }

        return DeferredTransportPaths.Contains(path);
    }

    public static string DescribeCoverage(string path)
    {
        if (TypedSurfaces.TryGetValue(path, out var surface))
        {
            return "typed:" + surface;
        }

        if (DeferredTransportPaths.Contains(path))
        {
            return "allowlist:IRomMTransport.SendAsync";
        }

        return "unmapped";
    }
}

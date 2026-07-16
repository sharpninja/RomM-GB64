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

    public static bool IsCovered(string path)
    {
        if (TypedSurfaces.ContainsKey(path))
        {
            return true;
        }

        // All other /api/* paths are allowlisted via generic transport.
        return path.StartsWith("/api/", StringComparison.Ordinal);
    }

    public static string DescribeCoverage(string path)
    {
        if (TypedSurfaces.TryGetValue(path, out var surface))
        {
            return "typed:" + surface;
        }

        if (path.StartsWith("/api/", StringComparison.Ordinal))
        {
            return "allowlist:IRomMTransport.SendAsync";
        }

        return "unmapped";
    }
}

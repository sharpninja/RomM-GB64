using RomM.Client;

namespace RomM.Client.Tests;

public sealed class OpenApiCoverageTests
{
    private static string SnapshotPath =>
        Path.Combine(AppContext.BaseDirectory, "openapi", "romm-5.0.0.json");

    [Fact]
    public void Every_openapi_path_is_typed_or_transport_allowlisted()
    {
        var inventory = OpenApiPathInventory.Load(SnapshotPath);
        var unmapped = inventory.Paths
            .Where(p => !OpenApiPathCoverage.IsCovered(p))
            .OrderBy(p => p)
            .ToList();

        Assert.True(
            unmapped.Count == 0,
            "Unmapped OpenAPI paths (must be typed or allowlisted):\n" + string.Join("\n", unmapped));

        // Core paths must be typed, not only transport allowlist.
        foreach (var core in new[]
                 {
                     "/api/heartbeat",
                     "/api/platforms",
                     "/api/platforms/{id}",
                     "/api/roms",
                     "/api/roms/{id}",
                     "/api/tasks",
                     "/api/tasks/run/{task_name}",
                     "/api/token",
                 })
        {
            Assert.True(
                OpenApiPathCoverage.TypedSurfaces.ContainsKey(core),
                $"Core path {core} must have a typed surface.");
        }
    }

    [Fact]
    public void IRomMClient_exposes_core_entry_points()
    {
        var t = typeof(IRomMClient);
        Assert.NotNull(t.GetProperty(nameof(IRomMClient.System)));
        Assert.NotNull(t.GetProperty(nameof(IRomMClient.Platforms)));
        Assert.NotNull(t.GetProperty(nameof(IRomMClient.Roms)));
        Assert.NotNull(t.GetProperty(nameof(IRomMClient.Tasks)));
        Assert.NotNull(t.GetProperty(nameof(IRomMClient.Transport)));
        Assert.NotNull(typeof(IRomMSystemClient).GetMethod(nameof(IRomMSystemClient.GetHeartbeatAsync)));
        Assert.NotNull(typeof(IRomMRomsClient).GetMethod(nameof(IRomMRomsClient.ListAsync)));
        Assert.NotNull(typeof(IRomMTasksClient).GetMethod(nameof(IRomMTasksClient.ScanLibraryAsync)));
    }
}

using System.Text.Json;

namespace RomM.Client.Tests;

/// <summary>TEST-SPEC-001 scaffold: pinned OpenAPI snapshot must exist and be parseable.</summary>
public sealed class OpenApiSnapshotTests
{
    private static string SnapshotPath =>
        Path.Combine(AppContext.BaseDirectory, "openapi", "romm-5.0.0.json");

    [Fact]
    public void OpenApi_snapshot_file_exists()
    {
        Assert.True(File.Exists(SnapshotPath), $"Missing OpenAPI snapshot at {SnapshotPath}");
    }

    [Fact]
    public void OpenApi_snapshot_is_romm_api_5()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(SnapshotPath));
        var root = doc.RootElement;
        Assert.Equal("3.1.0", root.GetProperty("openapi").GetString());
        var info = root.GetProperty("info");
        Assert.Equal("RomM API", info.GetProperty("title").GetString());
        Assert.Equal("5.0.0", info.GetProperty("version").GetString());
    }

    [Fact]
    public void OpenApi_snapshot_has_expected_core_paths()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(SnapshotPath));
        var paths = doc.RootElement.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/api/heartbeat", out _), "missing /api/heartbeat");
        Assert.True(paths.TryGetProperty("/api/platforms", out _), "missing /api/platforms");
        Assert.True(paths.TryGetProperty("/api/roms", out _), "missing /api/roms");
        Assert.True(paths.TryGetProperty("/api/tasks", out _), "missing /api/tasks");
        Assert.True(paths.TryGetProperty("/api/token", out _), "missing /api/token");
    }

    [Fact]
    public void OpenApi_path_inventory_has_at_least_100_paths()
    {
        var inventory = OpenApiPathInventory.Load(SnapshotPath);
        Assert.True(inventory.Paths.Count >= 100, $"Expected >=100 paths, got {inventory.Paths.Count}");
    }
}

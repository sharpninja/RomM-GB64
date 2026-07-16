using System.Text.Json;

namespace RomM.Client;

/// <summary>Inventory of path keys from a pinned OpenAPI snapshot.</summary>
public sealed class OpenApiPathInventory
{
    public OpenApiPathInventory(IReadOnlyList<string> paths)
    {
        Paths = paths;
    }

    public IReadOnlyList<string> Paths { get; }

    /// <summary>Loads path property names from the OpenAPI document <c>paths</c> object.</summary>
    public static OpenApiPathInventory Load(string snapshotPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshotPath);

        using var stream = File.OpenRead(snapshotPath);
        using var doc = JsonDocument.Parse(stream);
        var pathsElement = doc.RootElement.GetProperty("paths");

        var paths = new List<string>();
        foreach (var property in pathsElement.EnumerateObject())
        {
            paths.Add(property.Name);
        }

        return new OpenApiPathInventory(paths);
    }
}

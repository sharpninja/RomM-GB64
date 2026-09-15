using System.Text.Json;
using System.Text.Json.Serialization;

namespace RomM.Client.Models;

public sealed class HeartbeatSystem
{
    [JsonPropertyName("VERSION")]
    public string? Version { get; set; }

    [JsonPropertyName("SHOW_SETUP_WIZARD")]
    public bool? ShowSetupWizard { get; set; }
}

public sealed class HeartbeatResponse
{
    [JsonPropertyName("SYSTEM")]
    public HeartbeatSystem? System { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }

    public string? Version => System?.Version;

    public bool? ShowSetupWizard => System?.ShowSetupWizard;
}

public sealed class PlatformSchema
{
    public int Id { get; set; }
    public string? Slug { get; set; }
    public string? FsSlug { get; set; }
    public string? Name { get; set; }
    public string? DisplayName { get; set; }
    public int? RomCount { get; set; }
    public string? CustomName { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

public class SimpleRomSchema
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? FsName { get; set; }
    public string? FsPath { get; set; }
    public int? PlatformId { get; set; }
    public string? PlatformSlug { get; set; }
    public string? PlatformFsSlug { get; set; }
    public string? PlatformDisplayName { get; set; }
    public long? FsSizeBytes { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

public sealed class DetailedRomSchema : SimpleRomSchema
{
    public string? Summary { get; set; }
    public string? Slug { get; set; }
}

public sealed class RomPage
{
    public List<SimpleRomSchema> Items { get; set; } = new();
    public int Total { get; set; }
    public int Limit { get; set; }
    public int Offset { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

public sealed class TaskInfo
{
    public string? Name { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Type { get; set; }
    public bool? ManualRun { get; set; }
    public bool? Enabled { get; set; }
    public string? CronString { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

public sealed class TaskExecutionResponse
{
    public string? TaskId { get; set; }
    public string? TaskName { get; set; }
    public string? Id { get; set; }
    public string? Status { get; set; }
    public string? Name { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }

    public string ResolveTaskId() => TaskId ?? Id ?? throw new InvalidOperationException("Task response missing id.");
}

public sealed class TaskStatusResponse
{
    public string? TaskId { get; set; }
    public string? TaskName { get; set; }
    public string? Id { get; set; }
    public string? Status { get; set; }
    public string? State { get; set; }
    public bool? Done { get; set; }
    public bool? Finished { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }

    public bool IsTerminal
    {
        get
        {
            if (Done == true || Finished == true)
            {
                return true;
            }

            var s = (Status ?? State ?? "").Trim().ToLowerInvariant();
            return s is "finished" or "completed" or "complete" or "success" or "succeeded"
                or "failed" or "error" or "stopped" or "cancelled" or "canceled";
        }
    }

    public bool IsSuccess
    {
        get
        {
            var s = (Status ?? State ?? "").Trim().ToLowerInvariant();
            return s is "finished" or "completed" or "complete" or "success" or "succeeded" || Done == true;
        }
    }
}

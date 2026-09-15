using System.Collections.Generic;
using System.Text.Json.Serialization;
using RomM.Client.Auth;
using RomM.Client.Models;

namespace RomM.Client.Json;

/// <summary>
/// Source-generated JSON metadata for every DTO the client (de)serializes. Wiring this as the
/// serializer's <c>TypeInfoResolver</c> keeps RomM.Client reflection-free, so it is safe to consume
/// from a Native-AOT / trimmed app (e.g. the ViceSharp Xbox head). Register one entry per root type
/// deserialized by <see cref="RomMJson"/> and <see cref="RomMAuthHandler"/>; referenced types (bases,
/// element types, extension-data dictionaries) are pulled in automatically.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    NumberHandling = JsonNumberHandling.AllowReadingFromString)]
[JsonSerializable(typeof(HeartbeatSystem))]
[JsonSerializable(typeof(HeartbeatResponse))]
[JsonSerializable(typeof(PlatformSchema))]
[JsonSerializable(typeof(List<PlatformSchema>))]
[JsonSerializable(typeof(SimpleRomSchema))]
[JsonSerializable(typeof(DetailedRomSchema))]
[JsonSerializable(typeof(RomPage))]
[JsonSerializable(typeof(TaskInfo))]
[JsonSerializable(typeof(List<TaskInfo>))]
[JsonSerializable(typeof(Dictionary<string, TaskInfo>))]
[JsonSerializable(typeof(TaskExecutionResponse))]
[JsonSerializable(typeof(TaskStatusResponse))]
[JsonSerializable(typeof(Dictionary<string, TaskStatusResponse>))]
[JsonSerializable(typeof(RomMAuthHandler.TokenResponseDto))]
internal sealed partial class RomMJsonContext : JsonSerializerContext;

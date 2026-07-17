using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace RomM.Client.Json;

/// <summary>
/// The client's JSON entry point. It resolves every type through the source-generated
/// <see cref="RomMJsonContext"/> and serializes/deserializes via <see cref="JsonTypeInfo{T}"/>, so the
/// client stays reflection-free and Native-AOT / trim safe.
/// </summary>
internal static class RomMJson
{
    /// <summary>The source-gen serializer options (no reflection resolver).</summary>
    public static readonly JsonSerializerOptions Options = RomMJsonContext.Default.Options;

    public static T? Deserialize<T>(string json) =>
        JsonSerializer.Deserialize(json, TypeInfo<T>());

    public static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var value = await JsonSerializer.DeserializeAsync(stream, TypeInfo<T>(), ct).ConfigureAwait(false);
        return value ?? throw new InvalidOperationException($"Failed to deserialize {typeof(T).Name}.");
    }

    private static JsonTypeInfo<T> TypeInfo<T>() =>
        (JsonTypeInfo<T>)Options.GetTypeInfo(typeof(T));
}

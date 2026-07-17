using System.Text.Json;

namespace CsdbBridge.Services;

/// <summary>
/// Mints a per-user RomM access token via the OAuth password grant (<c>POST /api/token</c>), so the bridge
/// can hand a client a token scoped to its own user instead of the shared admin token. Used after
/// <see cref="RommUserProvisioner"/> has ensured the user exists (with the shared token as its password).
/// </summary>
public sealed class RommTokenClient
{
    private readonly HttpClient _romm;

    /// <summary>Creates the client over an HttpClient whose base address is RomM (no admin bearer needed).</summary>
    public RommTokenClient(HttpClient romm) => _romm = romm;

    /// <summary>
    /// The scopes requested for the per-user token. RomM's password grant defaults to no scope (which
    /// yields 403 on every scoped endpoint), so request the read + collection-write scopes an EDITOR needs
    /// to browse, download, and manage lists.
    /// </summary>
    private const string RequestedScope =
        "me.read roms.read platforms.read assets.read collections.read collections.write roms.user.read roms.user.write";

    /// <summary>Logs in as <paramref name="username"/> and returns the RomM access token.</summary>
    public async Task<string> LoginAsync(string username, string password, CancellationToken cancellationToken)
    {
        using var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "password"),
            new KeyValuePair<string, string>("username", username),
            new KeyValuePair<string, string>("password", password),
            new KeyValuePair<string, string>("scope", RequestedScope),
        });

        using HttpResponseMessage response = await _romm.PostAsync("api/token", form, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        string json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        using JsonDocument doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind == JsonValueKind.Object
            && doc.RootElement.TryGetProperty("access_token", out JsonElement token)
            && token.ValueKind == JsonValueKind.String
            && token.GetString() is { Length: > 0 } accessToken)
        {
            return accessToken;
        }

        throw new InvalidOperationException("RomM /api/token returned no access_token.");
    }
}

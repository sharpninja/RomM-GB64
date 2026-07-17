using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace CsdbBridge.Services;

/// <summary>
/// Ensures a RomM user account exists for an Xbox user id, creating it on demand so the client can then
/// authenticate to RomM with (xbox user id, shared token-as-password) via the OAuth password grant. Uses
/// the bridge's admin <c>ROMM_API_TOKEN</c> as the bearer for the RomM users API. Idempotent: an existing
/// username is left untouched.
/// </summary>
public sealed class RommUserProvisioner
{
    private readonly HttpClient _romm;

    /// <summary>Creates the provisioner over an HttpClient whose base address is RomM and whose default
    /// Authorization is the admin bearer token.</summary>
    public RommUserProvisioner(HttpClient romm) => _romm = romm;

    /// <summary>
    /// Ensures a RomM user named <paramref name="username"/> exists; creates it with the given password
    /// (role VIEWER) when missing. A create that races another (409/400 "already exists") is treated as success.
    /// </summary>
    public async Task EnsureUserAsync(string username, string password, CancellationToken cancellationToken)
    {
        if (await UserExistsAsync(username, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        // EDITOR so the per-user token can manage lists (collections.write), not just read.
        var body = new UserCreate(username, BuildEmail(username), password, "EDITOR");
        using HttpResponseMessage response = await _romm
            .PostAsJsonAsync("api/users", body, cancellationToken)
            .ConfigureAwait(false);

        if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Conflict)
        {
            // Created, or a definite duplicate (409): idempotent success.
            return;
        }

        // A 400 is ambiguous: it can be RomM's duplicate guard racing a concurrent create, OR a genuine
        // validation error (e.g. an unsafe/oversized username). Distinguish by re-checking existence: if
        // the user is present it was a duplicate; otherwise surface the failure instead of silently
        // proceeding to a later login 401.
        if (response.StatusCode == HttpStatusCode.BadRequest
            && await UserExistsAsync(username, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        response.EnsureSuccessStatusCode();
    }

    private async Task<bool> UserExistsAsync(string username, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _romm.GetAsync("api/users", cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        string json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        using JsonDocument doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (JsonElement user in doc.RootElement.EnumerateArray())
        {
            if (user.TryGetProperty("username", out JsonElement name)
                && name.ValueKind == JsonValueKind.String
                && string.Equals(name.GetString(), username, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Synthesizes a stable placeholder email for an auto-provisioned Xbox user.</summary>
    internal static string BuildEmail(string username)
    {
        var chars = username.Where(char.IsLetterOrDigit).ToArray();
        string local = chars.Length > 0 ? new string(chars) : "user";
        return $"{local}@xbox.local";
    }

    private sealed record UserCreate(string username, string email, string password, string role);
}

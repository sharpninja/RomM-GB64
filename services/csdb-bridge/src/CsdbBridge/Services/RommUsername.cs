using System.Security.Cryptography;
using System.Text;

namespace CsdbBridge.Services;

/// <summary>
/// Maps an opaque client id (the Xbox/Windows <c>NonRoamableId</c>, which is long and can contain characters
/// RomM rejects in a username) to a stable, RomM-safe username. Deterministic: the same client id always
/// yields the same username, so provisioning (create) and login resolve to one canonical account.
/// </summary>
public static class RommUsername
{
    /// <summary>
    /// Derives a stable, RomM-valid username (<c>xbox-</c> + 16 lowercase hex) from an arbitrary client id.
    /// Lowercase <c>[a-z0-9-]</c>, 21 chars: short and character-safe for both <c>POST /api/users</c> and the
    /// OAuth password grant, where the raw NonRoamableId would fail RomM's username validation.
    /// </summary>
    public static string FromClientId(string clientId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(clientId));
        string suffix = Convert.ToHexString(hash, 0, 8).ToLowerInvariant();
        return $"xbox-{suffix}";
    }
}

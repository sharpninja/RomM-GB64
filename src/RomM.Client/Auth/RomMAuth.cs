namespace RomM.Client.Auth;

/// <summary>Supported RomM authentication modes.</summary>
public enum RomMAuthKind
{
    ClientApiToken,
    Basic,
    OAuthPassword,
}

/// <summary>Authentication configuration for RomM HTTP calls.</summary>
public sealed class RomMAuth
{
    private RomMAuth(
        RomMAuthKind kind,
        string? token,
        string? username,
        string? password,
        IReadOnlyList<string> scopes)
    {
        Kind = kind;
        Token = token;
        Username = username;
        Password = password;
        Scopes = scopes;
    }

    public RomMAuthKind Kind { get; }

    /// <summary>Client API token (typically prefixed with <c>rmm_</c>).</summary>
    public string? Token { get; }

    public string? Username { get; }

    public string? Password { get; }

    public IReadOnlyList<string> Scopes { get; }

    /// <summary>Bearer token mode using a Client API Token.</summary>
    public static RomMAuth ClientApiToken(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        return new RomMAuth(RomMAuthKind.ClientApiToken, token, null, null, Array.Empty<string>());
    }

    /// <summary>HTTP Basic authentication.</summary>
    public static RomMAuth Basic(string username, string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentNullException.ThrowIfNull(password);
        return new RomMAuth(RomMAuthKind.Basic, null, username, password, Array.Empty<string>());
    }

    /// <summary>OAuth2 password grant with optional scopes.</summary>
    public static RomMAuth OAuthPassword(string username, string password, params string[] scopes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentNullException.ThrowIfNull(password);
        scopes ??= Array.Empty<string>();
        return new RomMAuth(
            RomMAuthKind.OAuthPassword,
            null,
            username,
            password,
            scopes.ToArray());
    }
}

namespace RomM.Client.Auth;

/// <summary>OAuth access and refresh token material returned by <c>POST /api/token</c>.</summary>
public sealed class RomMToken
{
    public required string AccessToken { get; init; }

    public string TokenType { get; init; } = "bearer";

    /// <summary>UTC instant when the access token is no longer valid.</summary>
    public required DateTimeOffset ExpiresAt { get; init; }

    public string? RefreshToken { get; init; }

    public DateTimeOffset? RefreshExpiresAt { get; init; }
}

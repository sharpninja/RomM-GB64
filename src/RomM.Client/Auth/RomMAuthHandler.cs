using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using RomM.Client.Json;

namespace RomM.Client.Auth;

/// <summary>
/// <see cref="DelegatingHandler"/> that applies Client API Token, HTTP Basic, or OAuth password credentials.
/// OAuth tokens are stored in an optional <see cref="IRomMTokenStore"/> and refreshed within a 30-second skew.
/// </summary>
public sealed class RomMAuthHandler : DelegatingHandler
{
    public static readonly TimeSpan RefreshSkew = TimeSpan.FromSeconds(30);

    private readonly RomMAuth _auth;
    private readonly IRomMTokenStore _tokenStore;
    private readonly TimeProvider _timeProvider;
    private readonly Uri? _allowedBaseAddress;
    private readonly SemaphoreSlim _tokenGate = new(1, 1);

    public RomMAuthHandler(
        RomMAuth auth,
        IRomMTokenStore? tokenStore = null,
        TimeProvider? timeProvider = null,
        Uri? allowedBaseAddress = null)
    {
        _auth = auth ?? throw new ArgumentNullException(nameof(auth));
        _tokenStore = tokenStore ?? new MemoryRomMTokenStore();
        _timeProvider = timeProvider ?? TimeProvider.System;
        _allowedBaseAddress = allowedBaseAddress;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (IsAllowedOrigin(request))
        {
            await ApplyCredentialAsync(request, cancellationToken).ConfigureAwait(false);
        }

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private bool IsAllowedOrigin(HttpRequestMessage request)
    {
        if (_allowedBaseAddress is null)
        {
            return true;
        }

        var uri = request.RequestUri;
        if (uri is null)
        {
            return false;
        }

        if (!uri.IsAbsoluteUri)
        {
            return true;
        }

        return string.Equals(uri.Scheme, _allowedBaseAddress.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(uri.Host, _allowedBaseAddress.Host, StringComparison.OrdinalIgnoreCase)
            && uri.Port == _allowedBaseAddress.Port;
    }

    private async Task ApplyCredentialAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        switch (_auth.Kind)
        {
            case RomMAuthKind.ClientApiToken:
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _auth.Token);
                break;

            case RomMAuthKind.Basic:
                var raw = Convert.ToBase64String(
                    Encoding.UTF8.GetBytes($"{_auth.Username}:{_auth.Password}"));
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", raw);
                break;

            case RomMAuthKind.OAuthPassword:
                var token = await EnsureOAuthTokenAsync(request, cancellationToken).ConfigureAwait(false);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
                break;

            default:
                throw new InvalidOperationException($"Unsupported auth kind: {_auth.Kind}.");
        }
    }

    private async Task<RomMToken> EnsureOAuthTokenAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        await _tokenGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var existing = await _tokenStore.GetAsync(cancellationToken).ConfigureAwait(false);
            if (existing is not null && !IsNearExpiry(existing.ExpiresAt))
            {
                return existing;
            }

            if (existing?.RefreshToken is { Length: > 0 } refreshToken)
            {
                try
                {
                    var refreshed = await RequestTokenAsync(
                        request,
                        BuildRefreshForm(refreshToken),
                        cancellationToken).ConfigureAwait(false);
                    await _tokenStore.SetAsync(refreshed, cancellationToken).ConfigureAwait(false);
                    return refreshed;
                }
                catch (RomMAuthException)
                {
                    await _tokenStore.ClearAsync(cancellationToken).ConfigureAwait(false);
                    throw;
                }
            }

            var granted = await RequestTokenAsync(
                request,
                BuildPasswordForm(),
                cancellationToken).ConfigureAwait(false);
            await _tokenStore.SetAsync(granted, cancellationToken).ConfigureAwait(false);
            return granted;
        }
        finally
        {
            _tokenGate.Release();
        }
    }

    private bool IsNearExpiry(DateTimeOffset expiresAt)
    {
        var now = _timeProvider.GetUtcNow();
        return expiresAt <= now.Add(RefreshSkew);
    }

    private FormUrlEncodedContent BuildPasswordForm()
    {
        var fields = new List<KeyValuePair<string, string>>
        {
            new("grant_type", "password"),
            new("username", _auth.Username ?? string.Empty),
            new("password", _auth.Password ?? string.Empty),
        };

        if (_auth.Scopes.Count > 0)
        {
            fields.Add(new KeyValuePair<string, string>("scope", string.Join(' ', _auth.Scopes)));
        }

        return new FormUrlEncodedContent(fields);
    }

    private static FormUrlEncodedContent BuildRefreshForm(string refreshToken)
    {
        return new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "refresh_token"),
            new KeyValuePair<string, string>("refresh_token", refreshToken),
        });
    }

    private async Task<RomMToken> RequestTokenAsync(
        HttpRequestMessage relatedRequest,
        FormUrlEncodedContent form,
        CancellationToken cancellationToken)
    {
        var tokenUri = ResolveTokenEndpoint(relatedRequest);
        using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, tokenUri)
        {
            Content = form,
        };

        using var response = await base.SendAsync(tokenRequest, cancellationToken).ConfigureAwait(false);
        var path = tokenUri.IsAbsoluteUri ? tokenUri.AbsolutePath : tokenUri.OriginalString;
        var body = response.Content is null
            ? null
            : await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            // Never put credentials or refresh tokens into the exception message.
            throw new RomMAuthException(
                (int)response.StatusCode,
                path,
                body,
                "Authentication failed.");
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            throw new RomMAuthException(
                (int)response.StatusCode,
                path,
                body,
                "Authentication failed: empty token response.");
        }

        TokenResponseDto? dto;
        try
        {
            dto = RomMJson.Deserialize<TokenResponseDto>(body);
        }
        catch (JsonException ex)
        {
            throw new RomMAuthException(
                (int)response.StatusCode,
                path,
                body,
                "Authentication failed: invalid token response.",
                ex);
        }

        if (dto is null || string.IsNullOrWhiteSpace(dto.AccessToken))
        {
            throw new RomMAuthException(
                (int)response.StatusCode,
                path,
                body,
                "Authentication failed: missing access_token.");
        }

        // RomM returns expires / refresh_expires as relative TTLs in seconds
        // (OAUTH_ACCESS_TOKEN_EXPIRE_SECONDS / OAUTH_REFRESH_TOKEN_EXPIRE_SECONDS), not Unix timestamps.
        var now = _timeProvider.GetUtcNow();
        return new RomMToken
        {
            AccessToken = dto.AccessToken,
            TokenType = string.IsNullOrWhiteSpace(dto.TokenType) ? "bearer" : dto.TokenType,
            ExpiresAt = now.Add(TimeSpan.FromSeconds(dto.Expires)),
            RefreshToken = dto.RefreshToken,
            RefreshExpiresAt = dto.RefreshExpires is long refreshExpires
                ? now.Add(TimeSpan.FromSeconds(refreshExpires))
                : null,
        };
    }

    private Uri ResolveTokenEndpoint(HttpRequestMessage request)
    {
        if (_allowedBaseAddress is not null)
        {
            return new Uri(_allowedBaseAddress, "api/token");
        }

        var current = request.RequestUri
            ?? throw new InvalidOperationException("RequestUri is required for OAuth token acquisition.");

        if (!current.IsAbsoluteUri)
        {
            throw new InvalidOperationException(
                "Absolute RequestUri is required for OAuth token acquisition.");
        }

        var builder = new UriBuilder(current)
        {
            Path = "/api/token",
            Query = string.Empty,
            Fragment = string.Empty,
        };
        return builder.Uri;
    }

    internal sealed class TokenResponseDto
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("token_type")]
        public string? TokenType { get; set; }

        [JsonPropertyName("expires")]
        public long Expires { get; set; }

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }

        [JsonPropertyName("refresh_expires")]
        public long? RefreshExpires { get; set; }
    }
}

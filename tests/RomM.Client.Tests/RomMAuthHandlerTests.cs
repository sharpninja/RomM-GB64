using System.Net;
using System.Text;
using System.Text.Json;
using RomM.Client.Auth;

namespace RomM.Client.Tests;

/// <summary>TEST-AUTH-001 / TEST-AUTH-002: Client token, Basic, OAuth password + refresh.</summary>
public sealed class RomMAuthHandlerTests
{
    private static readonly Uri ApiUri = new("http://romm.test/api/platforms");

    [Fact]
    public async Task ClientApiToken_attaches_Bearer_Authorization()
    {
        var recording = new RecordingHttpMessageHandler();
        var auth = RomMAuth.ClientApiToken("rmm_test_token_value");
        using var http = CreateClient(auth, recording);

        using var response = await http.GetAsync(ApiUri);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(recording.Requests);
        var req = recording.Requests[0];
        Assert.Equal("Bearer", req.AuthScheme);
        Assert.Equal("rmm_test_token_value", req.AuthParameter);
    }

    [Fact]
    public async Task Basic_attaches_Basic_Authorization()
    {
        var recording = new RecordingHttpMessageHandler();
        var auth = RomMAuth.Basic("alice", "s3cret");
        using var http = CreateClient(auth, recording);

        using var response = await http.GetAsync(ApiUri);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(recording.Requests);
        var req = recording.Requests[0];
        Assert.Equal("Basic", req.AuthScheme);
        var expected = Convert.ToBase64String(Encoding.UTF8.GetBytes("alice:s3cret"));
        Assert.Equal(expected, req.AuthParameter);
    }

    [Fact]
    public async Task OAuthPassword_grants_token_stores_and_uses_Bearer()
    {
        var store = new MemoryRomMTokenStore();
        var now = DateTimeOffset.Parse("2026-07-16T12:00:00Z");
        var time = new FakeTimeProvider(now);
        var recording = new RecordingHttpMessageHandler(async (request, index, ct) =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/api/token", StringComparison.Ordinal))
            {
                var body = await request.Content!.ReadAsStringAsync(ct);
                Assert.Contains("grant_type=password", body, StringComparison.Ordinal);
                Assert.Contains("username=bob", body, StringComparison.Ordinal);
                Assert.Contains("password=p%40ss", body, StringComparison.Ordinal);
                var hasScope = body.Contains("scope=me.read+roms.read", StringComparison.Ordinal)
                    || body.Contains("scope=me.read%20roms.read", StringComparison.Ordinal)
                    || body.Contains("scope=me.read roms.read", StringComparison.Ordinal);
                Assert.True(hasScope, $"Expected me.read and roms.read scope in form body: {body}");

                // RomM returns relative TTL seconds, not absolute Unix timestamps.
                var json = JsonSerializer.Serialize(new
                {
                    access_token = "access-abc",
                    token_type = "bearer",
                    expires = 1800,
                    refresh_token = "refresh-xyz",
                    refresh_expires = 604800,
                });
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json"),
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}"),
            };
        });

        var auth = RomMAuth.OAuthPassword("bob", "p@ss", RomMScopes.MeRead, RomMScopes.RomsRead);
        using var http = CreateClient(auth, recording, store, time);

        using var response = await http.GetAsync(ApiUri);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, recording.Requests.Count);

        var tokenReq = recording.Requests[0];
        Assert.Equal(HttpMethod.Post, tokenReq.Method);
        Assert.Equal("/api/token", tokenReq.RequestUri!.AbsolutePath);
        Assert.Equal("application/x-www-form-urlencoded", tokenReq.ContentType);
        Assert.Null(tokenReq.AuthScheme);

        var apiReq = recording.Requests[1];
        Assert.Equal("Bearer", apiReq.AuthScheme);
        Assert.Equal("access-abc", apiReq.AuthParameter);

        var stored = await store.GetAsync();
        Assert.NotNull(stored);
        Assert.Equal("access-abc", stored!.AccessToken);
        Assert.Equal("refresh-xyz", stored.RefreshToken);
        Assert.Equal(now.AddMinutes(30), stored.ExpiresAt);
        Assert.Equal(now.AddDays(7), stored.RefreshExpiresAt);
    }

    [Fact]
    public async Task OAuthPassword_relative_expires_reused_on_second_request_without_token_call()
    {
        var store = new MemoryRomMTokenStore();
        var now = DateTimeOffset.Parse("2026-07-16T12:00:00Z");
        var time = new FakeTimeProvider(now);
        var tokenCalls = 0;
        var recording = new RecordingHttpMessageHandler((request, index, ct) =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/api/token", StringComparison.Ordinal))
            {
                tokenCalls++;
                // Real RomM shape: expires / refresh_expires are relative seconds.
                var json = JsonSerializer.Serialize(new
                {
                    access_token = "access-rel",
                    token_type = "bearer",
                    expires = 1800,
                    refresh_token = "refresh-rel",
                    refresh_expires = 604800,
                });
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json"),
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}"),
            });
        });

        var auth = RomMAuth.OAuthPassword("bob", "p@ss", RomMScopes.MeRead);
        using var http = CreateClient(auth, recording, store, time);

        using (await http.GetAsync(ApiUri))
        {
        }

        using (await http.GetAsync(ApiUri))
        {
        }

        Assert.Equal(1, tokenCalls);
        Assert.Equal(3, recording.Requests.Count); // 1 token + 2 API
        Assert.All(recording.Requests.Skip(1), r => Assert.Equal("access-rel", r.AuthParameter));

        var stored = await store.GetAsync();
        Assert.NotNull(stored);
        Assert.Equal(now.AddMinutes(30), stored!.ExpiresAt);
        Assert.Equal(now.AddDays(7), stored.RefreshExpiresAt);
        // Not epoch-era: must be well after 2026-01-01
        Assert.True(stored.ExpiresAt > DateTimeOffset.Parse("2026-01-01Z"));
    }

    [Fact]
    public async Task OAuthPassword_refreshes_when_access_near_expiry()
    {
        var store = new MemoryRomMTokenStore();
        var now = DateTimeOffset.Parse("2026-07-16T12:00:00Z");
        var time = new FakeTimeProvider(now);

        await store.SetAsync(new RomMToken
        {
            AccessToken = "stale-access",
            TokenType = "bearer",
            ExpiresAt = now.AddSeconds(10),
            RefreshToken = "refresh-1",
            RefreshExpiresAt = now.AddHours(1),
        });

        var recording = new RecordingHttpMessageHandler((request, index, ct) =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/api/token", StringComparison.Ordinal))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        JsonSerializer.Serialize(new
                        {
                            access_token = "fresh-access",
                            token_type = "bearer",
                            expires = 3600,
                            refresh_token = "refresh-2",
                            refresh_expires = 86400,
                        }),
                        Encoding.UTF8,
                        "application/json"),
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}"),
            });
        });

        var auth = RomMAuth.OAuthPassword("bob", "p@ss", RomMScopes.MeRead);
        using var http = CreateClient(auth, recording, store, time);

        using var response = await http.GetAsync(ApiUri);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, recording.Requests.Count);

        var refreshReq = recording.Requests[0];
        Assert.Equal(HttpMethod.Post, refreshReq.Method);
        Assert.Equal("/api/token", refreshReq.RequestUri!.AbsolutePath);
        Assert.Contains("grant_type=refresh_token", refreshReq.Body, StringComparison.Ordinal);
        Assert.Contains("refresh_token=refresh-1", refreshReq.Body, StringComparison.Ordinal);

        var apiReq = recording.Requests[1];
        Assert.Equal("Bearer", apiReq.AuthScheme);
        Assert.Equal("fresh-access", apiReq.AuthParameter);

        var stored = await store.GetAsync();
        Assert.Equal("fresh-access", stored!.AccessToken);
        Assert.Equal("refresh-2", stored.RefreshToken);
    }

    [Fact]
    public async Task OAuthPassword_refresh_failure_throws_RomMAuthException()
    {
        var store = new MemoryRomMTokenStore();
        var now = DateTimeOffset.Parse("2026-07-16T12:00:00Z");
        var time = new FakeTimeProvider(now);

        await store.SetAsync(new RomMToken
        {
            AccessToken = "stale-access",
            TokenType = "bearer",
            ExpiresAt = now.AddSeconds(5),
            RefreshToken = "bad-refresh",
            RefreshExpiresAt = now.AddHours(1),
        });

        var recording = new RecordingHttpMessageHandler((request, index, ct) =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/api/token", StringComparison.Ordinal))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
                {
                    Content = new StringContent("{\"detail\":\"invalid refresh\"}"),
                    RequestMessage = request,
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        var auth = RomMAuth.OAuthPassword("bob", "super-secret-password", RomMScopes.MeRead);
        using var http = CreateClient(auth, recording, store, time);

        var ex = await Assert.ThrowsAsync<RomMAuthException>(() => http.GetAsync(ApiUri));
        Assert.Equal(401, ex.StatusCode);
        Assert.DoesNotContain("super-secret-password", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("bad-refresh", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("super-secret-password", ex.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task OAuthPassword_reuses_valid_stored_token_without_grant()
    {
        var store = new MemoryRomMTokenStore();
        var now = DateTimeOffset.Parse("2026-07-16T12:00:00Z");
        var time = new FakeTimeProvider(now);

        await store.SetAsync(new RomMToken
        {
            AccessToken = "still-good",
            TokenType = "bearer",
            ExpiresAt = now.AddHours(1),
            RefreshToken = "refresh-keep",
            RefreshExpiresAt = now.AddDays(1),
        });

        var recording = new RecordingHttpMessageHandler();
        var auth = RomMAuth.OAuthPassword("bob", "p@ss", RomMScopes.MeRead);
        using var http = CreateClient(auth, recording, store, time);

        using var response = await http.GetAsync(ApiUri);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(recording.Requests);
        Assert.Equal("Bearer", recording.Requests[0].AuthScheme);
        Assert.Equal("still-good", recording.Requests[0].AuthParameter);
    }

    [Fact]
    public void RomMClientOptions_can_hold_Auth_and_Validate_still_requires_BaseAddress()
    {
        var options = new RomMClientOptions
        {
            Auth = RomMAuth.ClientApiToken("rmm_x"),
        };
        var ex = Assert.Throws<ArgumentException>(() => options.Validate());
        Assert.Contains("BaseAddress", ex.Message, StringComparison.OrdinalIgnoreCase);

        options.BaseAddress = new Uri("http://example.com");
        options.Validate();
        Assert.Equal("http://example.com/", options.BaseAddress!.AbsoluteUri);
        Assert.NotNull(options.Auth);
        Assert.Equal(RomMAuthKind.ClientApiToken, options.Auth!.Kind);
    }

    [Fact]
    public async Task Default_handler_inherits_HttpClient_BaseAddress_on_relative_send()
    {
        var recording = new RecordingHttpMessageHandler();
        var handler = new RomMAuthHandler(RomMAuth.ClientApiToken("rmm_secret_token"))
        {
            InnerHandler = recording,
        };
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://romm.test/") };
        await using var transport = new RomMTransport(http);

        using var response = await transport.SendAsync(HttpMethod.Get, "api/platforms");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Bearer", recording.Requests[0].AuthScheme);
        Assert.Equal("rmm_secret_token", recording.Requests[0].AuthParameter);
    }

    [Fact]
    public async Task Default_handler_does_not_attach_Bearer_to_foreign_host_via_transport()
    {
        var recording = new RecordingHttpMessageHandler();
        var handler = new RomMAuthHandler(RomMAuth.ClientApiToken("rmm_secret_token"))
        {
            InnerHandler = recording,
        };
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://romm.test/") };
        await using var transport = new RomMTransport(http);

        using var response = await transport.SendAsync(HttpMethod.Get, "https://evil.example/api/roms");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(recording.Requests);
        Assert.Null(recording.Requests[0].AuthScheme);
        Assert.Null(recording.Requests[0].AuthParameter);
    }

    [Fact]
    public async Task Default_handler_does_not_post_OAuth_secrets_to_foreign_host_via_transport()
    {
        var recording = new RecordingHttpMessageHandler();
        var handler = new RomMAuthHandler(RomMAuth.OAuthPassword("bob", "p@ss"))
        {
            InnerHandler = recording,
        };
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://romm.test/") };
        await using var transport = new RomMTransport(http);

        using var response = await transport.SendAsync(HttpMethod.Get, "https://evil.example/api/platforms");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(
            recording.Requests,
            r => r.RequestUri is not null
                 && r.RequestUri.Host.Equals("evil.example", StringComparison.OrdinalIgnoreCase)
                 && r.AuthScheme is not null);
        Assert.DoesNotContain(
            recording.Requests,
            r => r.RequestUri is not null
                 && r.RequestUri.Host.Equals("evil.example", StringComparison.OrdinalIgnoreCase)
                 && r.Method == HttpMethod.Post);
    }

    [Fact]
    public async Task ClientApiToken_does_not_attach_to_foreign_host()
    {
        var recording = new RecordingHttpMessageHandler();
        var auth = RomMAuth.ClientApiToken("rmm_secret_token");
        using var http = CreateClient(auth, recording);

        using var response = await http.GetAsync("https://evil.example/api/roms");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(recording.Requests);
        Assert.Null(recording.Requests[0].AuthScheme);
        Assert.Null(recording.Requests[0].AuthParameter);
    }

    [Fact]
    public async Task OAuth_does_not_post_secrets_to_foreign_host()
    {
        var recording = new RecordingHttpMessageHandler();
        var auth = RomMAuth.OAuthPassword("bob", "p@ss");
        using var http = CreateClient(auth, recording);

        using var response = await http.GetAsync("https://evil.example/api/platforms");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(
            recording.Requests,
            r => r.RequestUri is not null
                 && r.RequestUri.Host.Equals("evil.example", StringComparison.OrdinalIgnoreCase)
                 && r.AuthScheme is not null);
        Assert.DoesNotContain(
            recording.Requests,
            r => r.RequestUri is not null
                 && r.RequestUri.Host.Equals("evil.example", StringComparison.OrdinalIgnoreCase)
                 && r.Method == HttpMethod.Post);
    }

    private static HttpClient CreateClient(
        RomMAuth auth,
        HttpMessageHandler inner,
        IRomMTokenStore? store = null,
        TimeProvider? timeProvider = null)
    {
        var handler = new RomMAuthHandler(auth, store, timeProvider, new Uri("http://romm.test/"))
        {
            InnerHandler = inner,
        };
        return new HttpClient(handler)
        {
            BaseAddress = new Uri("http://romm.test/"),
        };
    }
}

/// <summary>Minimal TimeProvider for expiry skew tests.</summary>
internal sealed class FakeTimeProvider : TimeProvider
{
    private readonly DateTimeOffset _utcNow;

    public FakeTimeProvider(DateTimeOffset utcNow)
    {
        _utcNow = utcNow;
    }

    public override DateTimeOffset GetUtcNow() => _utcNow;
}

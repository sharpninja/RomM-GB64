using System.Net;
using System.Text;
using CsdbBridge.Services;
using Xunit;

namespace CsdbBridge.Tests;

/// <summary>
/// The share endpoint logs in as the provisioned user to mint a per-user access token (so the client
/// never receives the admin token). These cover the password-grant login + access-token extraction.
/// </summary>
public class RommTokenClientTests
{
    [Fact]
    public async Task Login_returns_access_token_and_sends_password_grant()
    {
        var handler = new StubHandler(req =>
            Json("""{"access_token":"per-user-jwt","token_type":"bearer","expires":3600}"""));
        var client = new RommTokenClient(Client(handler));

        string token = await client.LoginAsync("xbox-user-1", "rmm_admin", CancellationToken.None);

        Assert.Equal("per-user-jwt", token);
        var post = Assert.Single(handler.Requests);
        Assert.Equal("/api/token", post.Path);
        Assert.Contains("grant_type=password", post.Body);
        Assert.Contains("username=xbox-user-1", post.Body);
        Assert.Contains("password=rmm_admin", post.Body);
    }

    [Fact]
    public async Task Login_throws_when_no_access_token()
    {
        var handler = new StubHandler(_ => Json("""{"detail":"bad creds"}"""));
        var client = new RommTokenClient(Client(handler));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.LoginAsync("u", "p", CancellationToken.None));
    }

    private static HttpClient Client(HttpMessageHandler handler) =>
        new(handler) { BaseAddress = new Uri("http://romm:8080/") };

    private static HttpResponseMessage Json(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

        public List<(HttpMethod Method, string Path, string? Body)> Requests { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string? body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add((request.Method, request.RequestUri!.AbsolutePath, body));
            return _responder(request);
        }
    }
}

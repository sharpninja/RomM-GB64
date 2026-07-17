using System.Net;
using System.Text;
using CsdbBridge.Services;
using Xunit;

namespace CsdbBridge.Tests;

/// <summary>
/// The share endpoint ensures a per-Xbox-user RomM account exists before handing back the connection.
/// These cover the find-or-create provisioner: existing user -> no create; missing -> POST /api/users;
/// duplicate -> treated as success; and the synthesized email.
/// </summary>
public class RommUserProvisionerTests
{
    [Fact]
    public async Task EnsureUser_skips_create_when_user_exists()
    {
        var handler = new StubHandler(req =>
            req.RequestUri!.AbsolutePath == "/api/users" && req.Method == HttpMethod.Get
                ? Json("""[{"username":"alice"},{"username":"bob"}]""")
                : new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var provisioner = new RommUserProvisioner(Client(handler));

        await provisioner.EnsureUserAsync("alice", "rmm_tok", CancellationToken.None);

        Assert.DoesNotContain(handler.Requests, r => r.Method == HttpMethod.Post);
    }

    [Fact]
    public async Task EnsureUser_creates_when_missing()
    {
        var handler = new StubHandler(req =>
        {
            if (req.Method == HttpMethod.Get) return Json("[]");
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var provisioner = new RommUserProvisioner(Client(handler));

        await provisioner.EnsureUserAsync("xbox-user-1", "rmm_tok", CancellationToken.None);

        var post = Assert.Single(handler.Requests.Where(r => r.Method == HttpMethod.Post));
        Assert.Equal("/api/users", post.Path);
        Assert.Contains("xbox-user-1", post.Body);
        Assert.Contains("VIEWER", post.Body);
        Assert.Contains("rmm_tok", post.Body);
    }

    [Fact]
    public async Task EnsureUser_treats_duplicate_as_success()
    {
        var handler = new StubHandler(req =>
            req.Method == HttpMethod.Get ? Json("[]") : new HttpResponseMessage(HttpStatusCode.Conflict));
        var provisioner = new RommUserProvisioner(Client(handler));

        // Must not throw on a 409 duplicate.
        await provisioner.EnsureUserAsync("dup", "rmm_tok", CancellationToken.None);
    }

    [Theory]
    [InlineData("XUID:123/abc", "XUID123abc@xbox.local")]
    [InlineData("!!!", "user@xbox.local")]
    [InlineData("plain", "plain@xbox.local")]
    public void BuildEmail_sanitizes(string username, string expected)
        => Assert.Equal(expected, RommUserProvisioner.BuildEmail(username));

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

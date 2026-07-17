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
        Assert.Contains("EDITOR", post.Body);
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

    [Fact]
    public async Task EnsureUser_throws_when_create_rejected_and_user_absent()
    {
        // RomM rejects the create with a 400 validation error and the user genuinely does not exist:
        // this must surface, not be silently swallowed (which previously led to a later login 401).
        var handler = new StubHandler(req =>
            req.Method == HttpMethod.Get
                ? Json("[]")
                : new HttpResponseMessage(HttpStatusCode.BadRequest));
        var provisioner = new RommUserProvisioner(Client(handler));

        await Assert.ThrowsAsync<HttpRequestException>(
            () => provisioner.EnsureUserAsync("bad", "rmm_tok", CancellationToken.None));
    }

    [Fact]
    public async Task EnsureUser_treats_400_as_success_when_user_now_exists()
    {
        // A 400 can be RomM's duplicate guard racing a concurrent create; if the user exists on the
        // re-check GET it is idempotent success and must not throw.
        int gets = 0;
        var handler = new StubHandler(req =>
        {
            if (req.Method == HttpMethod.Get)
                return Json(gets++ == 0 ? "[]" : """[{"username":"dup400"}]""");
            return new HttpResponseMessage(HttpStatusCode.BadRequest);
        });
        var provisioner = new RommUserProvisioner(Client(handler));

        await provisioner.EnsureUserAsync("dup400", "rmm_tok", CancellationToken.None);
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

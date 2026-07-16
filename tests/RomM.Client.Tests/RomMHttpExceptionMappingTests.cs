using System.Net;
using RomM.Client.Auth;
using RomM.Client.Http;

namespace RomM.Client.Tests;

/// <summary>TEST-AUTH / TR-ERR-001: 401 and 403 map to typed exceptions; secrets stay out of messages.</summary>
public sealed class RomMHttpExceptionMappingTests
{
    [Fact]
    public async Task EnsureSuccess_maps_401_to_RomMAuthException()
    {
        using var http = new HttpClient(new FixedStatusHandler(HttpStatusCode.Unauthorized, "{\"error\":\"nope\"}"))
        {
            BaseAddress = new Uri("http://romm.test/"),
        };

        using var response = await http.GetAsync("/api/platforms");
        var ex = await Assert.ThrowsAsync<RomMAuthException>(
            () => RomMHttp.EnsureSuccessOrThrowAsync(response));

        Assert.Equal(401, ex.StatusCode);
        Assert.Equal("/api/platforms", ex.Path);
        Assert.Contains("nope", ex.ResponseBody, StringComparison.Ordinal);
        Assert.IsAssignableFrom<RomMApiException>(ex);
    }

    [Fact]
    public async Task EnsureSuccess_maps_403_to_RomMForbiddenException()
    {
        using var http = new HttpClient(new FixedStatusHandler(HttpStatusCode.Forbidden, "{\"error\":\"denied\"}"))
        {
            BaseAddress = new Uri("http://romm.test/"),
        };

        using var response = await http.GetAsync("/api/users");
        var ex = await Assert.ThrowsAsync<RomMForbiddenException>(
            () => RomMHttp.EnsureSuccessOrThrowAsync(response));

        Assert.Equal(403, ex.StatusCode);
        Assert.Equal("/api/users", ex.Path);
        Assert.Contains("denied", ex.ResponseBody, StringComparison.Ordinal);
        Assert.IsAssignableFrom<RomMApiException>(ex);
    }

    [Fact]
    public async Task EnsureSuccess_maps_other_errors_to_RomMApiException()
    {
        using var http = new HttpClient(new FixedStatusHandler(HttpStatusCode.InternalServerError, "boom"))
        {
            BaseAddress = new Uri("http://romm.test/"),
        };

        using var response = await http.GetAsync("/api/heartbeat");
        var ex = await Assert.ThrowsAsync<RomMApiException>(
            () => RomMHttp.EnsureSuccessOrThrowAsync(response));

        Assert.Equal(500, ex.StatusCode);
        Assert.Equal("/api/heartbeat", ex.Path);
        Assert.Equal("boom", ex.ResponseBody);
        Assert.IsNotType<RomMAuthException>(ex);
        Assert.IsNotType<RomMForbiddenException>(ex);
    }

    [Fact]
    public async Task EnsureSuccess_does_not_throw_on_2xx()
    {
        using var http = new HttpClient(new FixedStatusHandler(HttpStatusCode.OK, "{}"))
        {
            BaseAddress = new Uri("http://romm.test/"),
        };

        using var response = await http.GetAsync("/api/heartbeat");
        await RomMHttp.EnsureSuccessOrThrowAsync(response);
    }

    [Fact]
    public void RomMApiException_message_does_not_include_caller_secrets()
    {
        const string secret = "rmm_should_not_leak";
        var ex = new RomMAuthException(
            statusCode: 401,
            path: "/api/token",
            responseBody: "{\"detail\":\"invalid\"}",
            message: "Authentication failed.");

        Assert.DoesNotContain(secret, ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(secret, ex.ToString(), StringComparison.Ordinal);
        Assert.Equal(401, ex.StatusCode);
        Assert.Equal("/api/token", ex.Path);
    }

    [Fact]
    public void RomMScopes_constants_match_OpenApi_password_flow_scopes()
    {
        Assert.Equal("me.read", RomMScopes.MeRead);
        Assert.Equal("roms.read", RomMScopes.RomsRead);
        Assert.Equal("roms.write", RomMScopes.RomsWrite);
        Assert.Equal("platforms.read", RomMScopes.PlatformsRead);
        Assert.Equal("platforms.write", RomMScopes.PlatformsWrite);
        Assert.Equal("assets.read", RomMScopes.AssetsRead);
        Assert.Equal("assets.write", RomMScopes.AssetsWrite);
        Assert.Equal("devices.read", RomMScopes.DevicesRead);
        Assert.Equal("devices.write", RomMScopes.DevicesWrite);
        Assert.Equal("firmware.read", RomMScopes.FirmwareRead);
        Assert.Equal("firmware.write", RomMScopes.FirmwareWrite);
        Assert.Equal("roms.user.read", RomMScopes.RomsUserRead);
        Assert.Equal("roms.user.write", RomMScopes.RomsUserWrite);
        Assert.Equal("collections.read", RomMScopes.CollectionsRead);
        Assert.Equal("collections.write", RomMScopes.CollectionsWrite);
        Assert.Equal("me.write", RomMScopes.MeWrite);
        Assert.Equal("users.read", RomMScopes.UsersRead);
        Assert.Equal("users.write", RomMScopes.UsersWrite);
        Assert.Equal("tasks.run", RomMScopes.TasksRun);
        Assert.Equal("logs.read", RomMScopes.LogsRead);
    }
}

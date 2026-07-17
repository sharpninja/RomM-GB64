using CsdbBridge.Services;
using Xunit;

namespace CsdbBridge.Tests;

/// <summary>
/// The share endpoint receives an opaque client id (the Xbox/Windows NonRoamableId) that is too long and
/// contains characters RomM rejects in a username, so it maps the id to a stable, RomM-safe username used
/// for both provisioning and login. These cover that mapping: RomM-safe charset/length, determinism, and
/// distinctness.
/// </summary>
public class RommUsernameTests
{
    // A real Windows NonRoamableId is a long, opaque base64-ish string with characters RomM rejects.
    private const string NonRoamableId =
        "AgAAAABEsm/RtY0J8m3qZ1a+Xb9c/9k=AwAAAABEsm/RtY0J8m3qZ1a+Xb9c/9k=";

    [Fact]
    public void FromClientId_is_romm_safe()
    {
        string username = RommUsername.FromClientId(NonRoamableId);

        Assert.StartsWith("xbox-", username);
        Assert.True(username.Length <= 32, $"username too long: {username.Length}");
        Assert.All(username, c => Assert.True(
            (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-',
            $"unsafe char '{c}' in {username}"));
    }

    [Fact]
    public void FromClientId_is_deterministic()
        => Assert.Equal(RommUsername.FromClientId(NonRoamableId), RommUsername.FromClientId(NonRoamableId));

    [Fact]
    public void FromClientId_differs_for_different_ids()
        => Assert.NotEqual(RommUsername.FromClientId(NonRoamableId), RommUsername.FromClientId(NonRoamableId + "x"));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void FromClientId_rejects_blank(string? clientId)
        => Assert.ThrowsAny<ArgumentException>(() => RommUsername.FromClientId(clientId!));
}

using System.Net;
using CsdbBridge.Services;
using Xunit;

namespace CsdbBridge.Tests;

/// <summary>
/// The bridge hands the RomM URL + API token only to callers on a trusted LAN (so a client can
/// self-provision without a pairing code). These cover the pure gate that decides "same subnet".
/// </summary>
public class RommShareGateTests
{
    [Theory]
    [InlineData("192.168.1.77", "192.168.1.0/24", true)]
    [InlineData("192.168.1.77", "192.168.2.0/24", false)]
    [InlineData("10.1.2.3", "10.0.0.0/8", true)]
    [InlineData("172.20.5.5", "172.16.0.0/12", true)]
    [InlineData("8.8.8.8", "10.0.0.0/8", false)]
    [InlineData("192.168.1.1", "192.168.1.1/32", true)]
    [InlineData("192.168.1.2", "192.168.1.1/32", false)]
    public void InCidr_matches_network(string ip, string cidr, bool expected)
        => Assert.Equal(expected, RommShareGate.InCidr(IPAddress.Parse(ip), cidr));

    [Fact]
    public void IsAllowed_accepts_private_lan_by_default()
        => Assert.True(RommShareGate.IsAllowed(IPAddress.Parse("192.168.1.50"), null, RommShareGate.DefaultCidrs));

    [Fact]
    public void IsAllowed_rejects_public_ip_by_default()
        => Assert.False(RommShareGate.IsAllowed(IPAddress.Parse("203.0.113.9"), null, RommShareGate.DefaultCidrs));

    [Fact]
    public void IsAllowed_prefers_forwarded_for_real_client()
    {
        // Behind Docker NAT the remote is a docker-gateway IP; the real LAN client is in XFF.
        var dockerGateway = IPAddress.Parse("172.17.0.1");
        Assert.True(RommShareGate.IsAllowed(dockerGateway, "192.168.1.60", new[] { "192.168.1.0/24" }));
        Assert.False(RommShareGate.IsAllowed(dockerGateway, "203.0.113.9", new[] { "192.168.1.0/24" }));
    }

    [Fact]
    public void IsAllowed_takes_first_forwarded_hop()
        => Assert.True(RommShareGate.IsAllowed(
            IPAddress.Parse("172.17.0.1"),
            "192.168.1.60, 10.9.9.9",
            new[] { "192.168.1.0/24" }));

    [Fact]
    public void IsAllowed_false_when_no_client_ip()
        => Assert.False(RommShareGate.IsAllowed(null, null, RommShareGate.DefaultCidrs));
}

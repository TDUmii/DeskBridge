using System.Net;
using DeskBridge.Core.Actions;
using DeskBridge.Core.Downloads;
using DeskBridge.Core.Models;

namespace DeskBridge.Tests;

public sealed class DownloadSecurityTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.2.3.4")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.255")]
    [InlineData("192.168.1.1")]
    [InlineData("169.254.1.1")]
    [InlineData("::1")]
    [InlineData("fc00::1")]
    [InlineData("fe80::1")]
    public void BlocksPrivateAndReservedAddresses(string address) =>
        Assert.True(NetworkGuard.IsPrivateOrReserved(IPAddress.Parse(address)));

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("1.1.1.1")]
    [InlineData("2606:4700:4700::1111")]
    public void AllowsPublicAddresses(string address) => Assert.False(NetworkGuard.IsPrivateOrReserved(IPAddress.Parse(address)));

    [Fact]
    public async Task BlocksLocalhostResolution()
    {
        var error = await Assert.ThrowsAsync<DeskBridgeActionException>(() => new NetworkGuard().ResolvePublicAsync("localhost", CancellationToken.None));
        Assert.Equal(ErrorCodes.PrivateNetworkBlocked, error.Code);
    }

    [Fact]
    public void ValidatesContentTypeAndMagicBytes()
    {
        var png = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10, 0 };
        Assert.Equal("png", ImagePayloadValidator.Validate("image/png", png));
        var error = Assert.Throws<DeskBridgeActionException>(() => ImagePayloadValidator.Validate("text/html", "<html>"u8.ToArray()));
        Assert.Equal(ErrorCodes.InvalidContentType, error.Code);
        Assert.Throws<DeskBridgeActionException>(() => ImagePayloadValidator.Validate("image/png", "<html>"u8.ToArray()));
    }
}

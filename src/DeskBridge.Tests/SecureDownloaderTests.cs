using System.Net;
using System.Net.Http.Headers;
using DeskBridge.Core.Actions;
using DeskBridge.Core.Downloads;
using DeskBridge.Core.Models;

namespace DeskBridge.Tests;

public sealed class SecureDownloaderTests
{
    [Fact]
    public async Task DownloadsValidHttpsImage()
    {
        var png = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10, 0 };
        var downloader = CreateDownloader(_ => ImageResponse(png, "image/png"));
        var result = await downloader.DownloadAsync("https://example.com/image.bin", CancellationToken.None);
        Assert.Equal("png", result.Extension);
        Assert.Equal(png, result.Bytes);
    }

    [Theory]
    [InlineData("http://example.com/image.png", ErrorCodes.UnsupportedProtocol)]
    [InlineData("file:///c:/image.png", ErrorCodes.UnsupportedProtocol)]
    public async Task BlocksUnsupportedProtocols(string url, string expectedCode)
    {
        var error = await Assert.ThrowsAsync<DeskBridgeActionException>(() =>
            CreateDownloader(_ => ImageResponse([], "image/png")).DownloadAsync(url, CancellationToken.None));
        Assert.Equal(expectedCode, error.Code);
    }

    [Fact]
    public async Task RevalidatesPrivateRedirect()
    {
        var downloader = CreateDownloader(request => new HttpResponseMessage(HttpStatusCode.Redirect)
        { Headers = { Location = new Uri("https://127.0.0.1/private.png") }, RequestMessage = request });
        var error = await Assert.ThrowsAsync<DeskBridgeActionException>(() =>
            downloader.DownloadAsync("https://example.com/start", CancellationToken.None));
        Assert.Equal(ErrorCodes.PrivateNetworkBlocked, error.Code);
    }

    [Fact]
    public async Task BlocksOversizedAndInvalidContent()
    {
        var oversized = CreateDownloader(request =>
        {
            var response = ImageResponse([1], "image/png");
            response.RequestMessage = request;
            response.Content.Headers.ContentLength = SecureImageDownloader.MaxBytes + 1;
            return response;
        });
        var tooLarge = await Assert.ThrowsAsync<DeskBridgeActionException>(() => oversized.DownloadAsync("https://example.com/a", CancellationToken.None));
        Assert.Equal(ErrorCodes.DownloadTooLarge, tooLarge.Code);

        var invalid = CreateDownloader(_ => ImageResponse("<html>not image</html>"u8.ToArray(), "text/html"));
        var invalidType = await Assert.ThrowsAsync<DeskBridgeActionException>(() => invalid.DownloadAsync("https://example.com/a", CancellationToken.None));
        Assert.Equal(ErrorCodes.InvalidContentType, invalidType.Code);
    }

    private static SecureImageDownloader CreateDownloader(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
    {
        var handler = new StubHandler(responseFactory);
        return new SecureImageDownloader(new StubNetworkGuard(), () => new HttpClient(handler, disposeHandler: false));
    }

    private static HttpResponseMessage ImageResponse(byte[] bytes, string contentType)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return response;
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = responseFactory(request);
            response.RequestMessage ??= request;
            return Task.FromResult(response);
        }
    }

    private sealed class StubNetworkGuard : NetworkGuard
    {
        public override Task<IReadOnlyList<IPAddress>> ResolvePublicAsync(string host, CancellationToken cancellationToken)
        {
            if (IPAddress.TryParse(host, out var address) && IsPrivateOrReserved(address))
                throw new DeskBridgeActionException(ErrorCodes.PrivateNetworkBlocked, "private");
            return Task.FromResult<IReadOnlyList<IPAddress>>([IPAddress.Parse("93.184.216.34")]);
        }
    }
}

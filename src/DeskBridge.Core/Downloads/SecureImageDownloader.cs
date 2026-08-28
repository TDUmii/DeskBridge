using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using DeskBridge.Core.Actions;
using DeskBridge.Core.Models;

namespace DeskBridge.Core.Downloads;

public sealed record DownloadedImage(byte[] Bytes, string ContentType, Uri FinalUri, string Extension);

public sealed class SecureImageDownloader
{
    public const int MaxBytes = 20 * 1024 * 1024;
    private const int MaxRedirects = 5;
    private readonly NetworkGuard _networkGuard;
    private readonly Func<HttpClient> _clientFactory;

    public SecureImageDownloader(NetworkGuard networkGuard)
        : this(networkGuard, () => CreatePinnedClient(networkGuard)) { }

    public SecureImageDownloader(NetworkGuard networkGuard, Func<HttpClient> clientFactory)
    {
        _networkGuard = networkGuard;
        _clientFactory = clientFactory;
    }

    public async Task<DownloadedImage> DownloadAsync(string url, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var current))
        {
            throw new DeskBridgeActionException(ErrorCodes.InvalidUrl, "URL is invalid.");
        }

        for (var redirect = 0; redirect <= MaxRedirects; redirect++)
        {
            ValidateHttps(current);
            await _networkGuard.ResolvePublicAsync(current.Host, cancellationToken).ConfigureAwait(false);
            using var client = _clientFactory();
            using var request = new HttpRequestMessage(HttpMethod.Get, current);
            request.Headers.UserAgent.ParseAdd("DeskBridge/1.0");
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            if (IsRedirect(response.StatusCode))
            {
                var location = response.Headers.Location ??
                               throw new DeskBridgeActionException(ErrorCodes.InvalidUrl, "Redirect has no destination.");
                current = location.IsAbsoluteUri ? location : new Uri(current, location);
                continue;
            }

            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength is > MaxBytes)
            {
                throw new DeskBridgeActionException(ErrorCodes.DownloadTooLarge, "Download exceeds the 20 MB limit.");
            }

            var contentType = response.Content.Headers.ContentType?.MediaType?.ToLowerInvariant() ?? string.Empty;
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var memory = new MemoryStream();
            var buffer = new byte[81920];
            int read;
            while ((read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                if (memory.Length + read > MaxBytes)
                {
                    throw new DeskBridgeActionException(ErrorCodes.DownloadTooLarge, "Download exceeds the 20 MB limit.");
                }

                await memory.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            }

            var bytes = memory.ToArray();
            var extension = ImagePayloadValidator.Validate(contentType, bytes);
            return new DownloadedImage(bytes, contentType, current, extension);
        }

        throw new DeskBridgeActionException(ErrorCodes.InvalidUrl, "Too many redirects.");
    }

    private static HttpClient CreatePinnedClient(NetworkGuard guard)
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            ConnectTimeout = TimeSpan.FromSeconds(10),
            PooledConnectionLifetime = TimeSpan.Zero,
            ConnectCallback = async (context, cancellationToken) =>
            {
                var addresses = await guard.ResolvePublicAsync(context.DnsEndPoint.Host, cancellationToken).ConfigureAwait(false);
                Exception? lastError = null;
                foreach (var address in addresses)
                {
                    var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                    try
                    {
                        await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), cancellationToken)
                            .ConfigureAwait(false);
                        return new NetworkStream(socket, ownsSocket: true);
                    }
                    catch (Exception exception) when (exception is SocketException or OperationCanceledException)
                    {
                        socket.Dispose();
                        lastError = exception;
                    }
                }

                throw lastError ?? new SocketException((int)SocketError.HostUnreachable);
            }
        };
        return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
    }

    private static void ValidateHttps(Uri uri)
    {
        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new DeskBridgeActionException(ErrorCodes.UnsupportedProtocol, "Only HTTPS downloads are supported.");
        }

        if (string.IsNullOrWhiteSpace(uri.Host) || !string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new DeskBridgeActionException(ErrorCodes.InvalidUrl, "URL host is invalid and credentials are not allowed.");
        }
    }

    private static bool IsRedirect(HttpStatusCode statusCode) => statusCode is
        HttpStatusCode.Moved or HttpStatusCode.Redirect or HttpStatusCode.RedirectMethod or
        HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect;
}

public static class ImagePayloadValidator
{
    private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp", "image/gif", "image/svg+xml"
    };

    public static string Validate(string contentType, byte[] bytes)
    {
        if (!AllowedTypes.Contains(contentType))
        {
            throw new DeskBridgeActionException(ErrorCodes.InvalidContentType, $"Content-Type '{contentType}' is not a supported image.");
        }

        var detected = Detect(bytes);
        if (detected is null || (contentType == "image/jpeg" ? detected != "jpg" :
            contentType == "image/svg+xml" ? detected != "svg" : detected != contentType[6..]))
        {
            throw new DeskBridgeActionException(ErrorCodes.InvalidContentType, "Image magic bytes do not match Content-Type.");
        }

        return detected;
    }

    public static string? Detect(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF) return "jpg";
        if (bytes.Length >= 8 && bytes[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return "png";
        if (bytes.Length >= 12 && Encoding.ASCII.GetString(bytes[..4]) == "RIFF" && Encoding.ASCII.GetString(bytes.Slice(8, 4)) == "WEBP") return "webp";
        if (bytes.Length >= 6 && (Encoding.ASCII.GetString(bytes[..6]) is "GIF87a" or "GIF89a")) return "gif";
        if (bytes.Length > 0)
        {
            var prefix = Encoding.UTF8.GetString(bytes[..Math.Min(bytes.Length, 4096)]).TrimStart('\uFEFF', ' ', '\r', '\n', '\t');
            if ((prefix.StartsWith("<svg", StringComparison.OrdinalIgnoreCase) ||
                 (prefix.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase) && prefix.Contains("<svg", StringComparison.OrdinalIgnoreCase))) &&
                !prefix.Contains("<script", StringComparison.OrdinalIgnoreCase) &&
                !prefix.Contains("javascript:", StringComparison.OrdinalIgnoreCase) &&
                !prefix.Contains("onload=", StringComparison.OrdinalIgnoreCase) &&
                !prefix.Contains("<foreignObject", StringComparison.OrdinalIgnoreCase))
            {
                return "svg";
            }
        }

        return null;
    }
}

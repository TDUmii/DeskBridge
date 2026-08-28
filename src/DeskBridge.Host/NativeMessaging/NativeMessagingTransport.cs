using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using DeskBridge.Core.Models;

namespace DeskBridge.Host.NativeMessaging;

public sealed class NativeMessageException(string message) : Exception(message);

public sealed class NativeMessagingTransport(Stream input, Stream output)
{
    private const int MaxMessageBytes = 1024 * 1024;

    public async Task<string?> ReadAsync(CancellationToken cancellationToken)
    {
        var lengthBytes = new byte[4];
        var headerBytes = await ReadExactAsync(input, lengthBytes, cancellationToken).ConfigureAwait(false);
        if (headerBytes == 0) return null;
        if (headerBytes != 4) throw new NativeMessageException("Native message length prefix is incomplete.");
        var length = BinaryPrimitives.ReadInt32LittleEndian(lengthBytes);
        if (length <= 0 || length > MaxMessageBytes)
            throw new NativeMessageException("Native message length is invalid or exceeds 1 MB.");
        var payload = new byte[length];
        if (await ReadExactAsync(input, payload, cancellationToken).ConfigureAwait(false) != length)
            throw new NativeMessageException("Native message payload is incomplete.");
        return new UTF8Encoding(false, true).GetString(payload);
    }

    public async Task WriteAsync(ActionResponse response, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(response, DeskBridgeJson.Options);
        var header = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header, payload.Length);
        await output.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        await output.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        await output.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<int> ReadExactAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(total), cancellationToken).ConfigureAwait(false);
            if (read == 0) break;
            total += read;
        }
        return total;
    }
}

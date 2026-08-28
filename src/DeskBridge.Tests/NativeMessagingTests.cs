using System.Buffers.Binary;
using System.Text;
using DeskBridge.Core.Models;
using DeskBridge.Host.NativeMessaging;

namespace DeskBridge.Tests;

public sealed class NativeMessagingTests
{
    [Fact]
    public async Task ReadsLittleEndianUtf8Frame()
    {
        var json = "{\"version\":1}";
        var payload = Encoding.UTF8.GetBytes(json);
        var bytes = new byte[4 + payload.Length];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, payload.Length);
        payload.CopyTo(bytes, 4);
        await using var input = new MemoryStream(bytes);
        await using var output = new MemoryStream();
        var transport = new NativeMessagingTransport(input, output);
        Assert.Equal(json, await transport.ReadAsync(CancellationToken.None));
    }

    [Fact]
    public async Task RejectsOversizedFrame()
    {
        var header = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header, 1024 * 1024 + 1);
        await using var input = new MemoryStream(header);
        await using var output = new MemoryStream();
        await Assert.ThrowsAsync<NativeMessageException>(() =>
            new NativeMessagingTransport(input, output).ReadAsync(CancellationToken.None));
    }

    [Fact]
    public async Task WritesLengthPrefixedResponse()
    {
        await using var input = new MemoryStream();
        await using var output = new MemoryStream();
        var transport = new NativeMessagingTransport(input, output);
        await transport.WriteAsync(new ActionResponse(1, "id-1", true, new { value = 1 }, null), CancellationToken.None);
        var bytes = output.ToArray();
        var length = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(0, 4));
        Assert.Equal(bytes.Length - 4, length);
        Assert.Contains("\"id\":\"id-1\"", Encoding.UTF8.GetString(bytes, 4, length));
    }
}

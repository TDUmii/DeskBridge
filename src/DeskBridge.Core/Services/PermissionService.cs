using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using DeskBridge.Core.Models;

namespace DeskBridge.Core.Services;

public interface IPermissionService
{
    Task<bool> RequestAsync(PermissionRequest request, CancellationToken cancellationToken);
}

public sealed class DenyPermissionService : IPermissionService
{
    public Task<bool> RequestAsync(PermissionRequest request, CancellationToken cancellationToken) => Task.FromResult(false);
}

public sealed class AllowPermissionService : IPermissionService
{
    public Task<bool> RequestAsync(PermissionRequest request, CancellationToken cancellationToken) => Task.FromResult(true);
}

public sealed class NamedPipePermissionService(string pipeName = "DeskBridge.Permission") : IPermissionService
{
    public async Task<bool> RequestAsync(PermissionRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            await pipe.ConnectAsync(1500, cancellationToken).ConfigureAwait(false);
            await using var writer = new StreamWriter(pipe, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
            using var reader = new StreamReader(pipe, Encoding.UTF8, false, leaveOpen: true);
            await writer.WriteLineAsync(JsonSerializer.Serialize(request, DeskBridgeJson.Options)).ConfigureAwait(false);
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            var response = line is null ? null : JsonSerializer.Deserialize<PermissionResponse>(line, DeskBridgeJson.Options);
            return response?.Id == request.Id && response.Allowed;
        }
        catch (IOException)
        {
            return false;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }
}

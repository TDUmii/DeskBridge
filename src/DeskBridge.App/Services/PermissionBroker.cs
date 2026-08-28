using System.IO.Pipes;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using DeskBridge.App.Views;
using DeskBridge.Core.Models;

namespace DeskBridge.App.Services;

public sealed class PermissionBroker
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await using var pipe = new NamedPipeServerStream("DeskBridge.Permission", PipeDirection.InOut, 1,
                PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            try
            {
                await pipe.WaitForConnectionAsync(cancellationToken);
                using var reader = new StreamReader(pipe, Encoding.UTF8, false, leaveOpen: true);
                await using var writer = new StreamWriter(pipe, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
                var line = await reader.ReadLineAsync(cancellationToken);
                var request = line is null ? null : JsonSerializer.Deserialize<PermissionRequest>(line, DeskBridgeJson.Options);
                var allowed = false;
                if (request is not null)
                {
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        allowed = new PermissionDialog(request) { Owner = Application.Current.MainWindow }.ShowDialog() == true;
                    });
                    await writer.WriteLineAsync(JsonSerializer.Serialize(
                        new PermissionResponse(request.Id, allowed), DeskBridgeJson.Options));
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
            catch (IOException) { }
        }
    }
}

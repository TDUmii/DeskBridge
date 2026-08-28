using System.Text.Json;
using System.Windows.Forms;
using DeskBridge.Core.Models;

namespace DeskBridge.Core.Actions;

public sealed class GetClipboardAction : IDeskBridgeAction
{
    public string Name => "get_clipboard";

    public async Task<ActionResult> ExecuteAsync(JsonElement arguments, ActionContext context, CancellationToken cancellationToken)
    {
        var text = await ClipboardSta.RunAsync(() => Clipboard.ContainsText() ? Clipboard.GetText() : string.Empty,
            cancellationToken).ConfigureAwait(false);
        return ActionResult.Ok(new { text });
    }
}

public sealed class SetClipboardAction : IDeskBridgeAction
{
    public string Name => "set_clipboard";

    public async Task<ActionResult> ExecuteAsync(JsonElement arguments, ActionContext context, CancellationToken cancellationToken)
    {
        var text = arguments.TryGetProperty("text", out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : throw new DeskBridgeActionException(ErrorCodes.InvalidRequest, "'text' must be a string.");
        await ClipboardSta.RunAsync(() =>
        {
            Clipboard.SetText(text);
            return true;
        }, cancellationToken).ConfigureAwait(false);
        return ActionResult.Ok(new { length = text.Length });
    }
}

internal static class ClipboardSta
{
    public static Task<T> RunAsync<T>(Func<T> callback, CancellationToken cancellationToken)
    {
        var source = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                source.TrySetResult(callback());
            }
            catch (Exception exception)
            {
                source.TrySetException(exception);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        cancellationToken.Register(() => source.TrySetCanceled(cancellationToken));
        return source.Task;
    }
}

using System.Text.Json;
using DeskBridge.Core.Models;

namespace DeskBridge.Core.Services;

public sealed class ActivityLogger(string? path = null)
{
    private readonly string _path = path ?? DeskBridgePaths.ActivityFile;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task WriteAsync(ActivityEntry entry, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var json = JsonSerializer.Serialize(entry, DeskBridgeJson.Options);
            await File.AppendAllTextAsync(_path, json + Environment.NewLine, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<ActivityEntry>> ReadRecentAsync(int count = 100, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_path))
        {
            return [];
        }

        var lines = await File.ReadAllLinesAsync(_path, cancellationToken).ConfigureAwait(false);
        return lines.TakeLast(count).Reverse()
            .Select(line => JsonSerializer.Deserialize<ActivityEntry>(line, DeskBridgeJson.Options))
            .Where(entry => entry is not null).Cast<ActivityEntry>().ToArray();
    }

    public void Clear()
    {
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }
}

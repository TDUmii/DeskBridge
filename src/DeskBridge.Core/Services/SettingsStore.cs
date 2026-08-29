using System.Text.Json;
using DeskBridge.Core.Models;

namespace DeskBridge.Core.Services;

public static class DeskBridgePaths
{
    public static string DataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DeskBridge");
    public static string SettingsFile => Path.Combine(DataDirectory, "settings.json");
    public static string ActivityFile => Path.Combine(DataDirectory, "activity.jsonl");
    public static string LogDirectory => Path.Combine(DataDirectory, "logs");
    public static string WebAgentDirectory => Path.Combine(DataDirectory, "web-agent");
}

public sealed class SettingsStore(string? path = null)
{
    private readonly string _path = path ?? DeskBridgePaths.SettingsFile;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<DeskBridgeSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(_path))
            {
                return new DeskBridgeSettings();
            }

            await using var stream = File.OpenRead(_path);
            return await JsonSerializer.DeserializeAsync<DeskBridgeSettings>(stream, DeskBridgeJson.Options, cancellationToken)
                .ConfigureAwait(false) ?? new DeskBridgeSettings();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(DeskBridgeSettings settings, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var temporary = _path + ".tmp";
            await using (var stream = File.Create(temporary))
            {
                await JsonSerializer.SerializeAsync(stream, settings, DeskBridgeJson.Options, cancellationToken)
                    .ConfigureAwait(false);
            }

            File.Move(temporary, _path, true);
        }
        finally
        {
            _gate.Release();
        }
    }
}

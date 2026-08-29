using System.Security.Cryptography;
using System.Text;
using DeskBridge.Core.Services;

namespace DeskBridge.Core.Agent;

public interface IApiKeyStore
{
    bool HasKey { get; }
    Task SaveAsync(string apiKey, CancellationToken cancellationToken = default);
    Task<string?> LoadAsync(CancellationToken cancellationToken = default);
    void Delete();
}

public sealed class WindowsApiKeyStore(string? path = null) : IApiKeyStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("DeskBridge.OpenAI.ApiKey.v1");
    private readonly string _path = path ?? Path.Combine(DeskBridgePaths.SecretDirectory, "openai-api-key.bin");

    public bool HasKey => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OPENAI_API_KEY")) || File.Exists(_path);

    public async Task SaveAsync(string apiKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey) || !apiKey.Trim().StartsWith("sk-", StringComparison.Ordinal))
            throw new ArgumentException("Enter a valid OpenAI API key beginning with sk-.", nameof(apiKey));

        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var protectedBytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(apiKey.Trim()), Entropy, DataProtectionScope.CurrentUser);
        await File.WriteAllBytesAsync(_path, protectedBytes, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string?> LoadAsync(CancellationToken cancellationToken = default)
    {
        var environmentKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (!string.IsNullOrWhiteSpace(environmentKey)) return environmentKey.Trim();
        if (!File.Exists(_path)) return null;

        var protectedBytes = await File.ReadAllBytesAsync(_path, cancellationToken).ConfigureAwait(false);
        try
        {
            return Encoding.UTF8.GetString(ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser));
        }
        catch (CryptographicException exception)
        {
            throw new InvalidOperationException("The saved API key cannot be decrypted for this Windows account. Remove and save it again.", exception);
        }
    }

    public void Delete()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }
}

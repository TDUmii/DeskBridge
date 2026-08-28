using System.Text.Json;
using System.Text.Json.Serialization;

namespace DeskBridge.Core.Models;

public sealed record ActionRequest
{
    public int Version { get; init; }
    public string Id { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public JsonElement Arguments { get; init; }
}

public sealed record ActionResponse(
    int Version,
    string Id,
    bool Success,
    object? Data,
    DeskBridgeError? Error)
{
    public static ActionResponse FromResult(string id, ActionResult result) =>
        new(1, id, result.Success, result.Data, result.Error);
}

public sealed record DeskBridgeError(string Code, string Message);

public sealed record ActionResult(bool Success, object? Data, DeskBridgeError? Error)
{
    public static ActionResult Ok(object? data = null) => new(true, data ?? new { }, null);
    public static ActionResult Fail(string code, string message) =>
        new(false, null, new DeskBridgeError(code, message));
}

public static class ErrorCodes
{
    public const string InvalidRequest = "INVALID_REQUEST";
    public const string UnknownAction = "UNKNOWN_ACTION";
    public const string ActionNotAllowed = "ACTION_NOT_ALLOWED";
    public const string PermissionDenied = "PERMISSION_DENIED";
    public const string WorkspaceViolation = "WORKSPACE_VIOLATION";
    public const string FileNotFound = "FILE_NOT_FOUND";
    public const string FileAlreadyExists = "FILE_ALREADY_EXISTS";
    public const string AppNotFound = "APP_NOT_FOUND";
    public const string CommandNotAllowed = "COMMAND_NOT_ALLOWED";
    public const string CommandTimeout = "COMMAND_TIMEOUT";
    public const string ExecutionFailed = "EXECUTION_FAILED";
    public const string InternalError = "INTERNAL_ERROR";
    public const string InvalidUrl = "INVALID_URL";
    public const string UnsupportedProtocol = "UNSUPPORTED_PROTOCOL";
    public const string PrivateNetworkBlocked = "PRIVATE_NETWORK_BLOCKED";
    public const string DownloadTooLarge = "DOWNLOAD_TOO_LARGE";
    public const string DownloadTimeout = "DOWNLOAD_TIMEOUT";
    public const string InvalidContentType = "INVALID_CONTENT_TYPE";
    public const string UnsupportedImageFormat = "UNSUPPORTED_IMAGE_FORMAT";
    public const string ImageProcessingFailed = "IMAGE_PROCESSING_FAILED";
    public const string PatchTargetNotFound = "PATCH_TARGET_NOT_FOUND";
    public const string PatchTargetNotUnique = "PATCH_TARGET_NOT_UNIQUE";
    public const string ProjectPathInvalid = "PROJECT_PATH_INVALID";
    public const string AssetImportDenied = "ASSET_IMPORT_DENIED";
}

public static class DeskBridgeJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };
}

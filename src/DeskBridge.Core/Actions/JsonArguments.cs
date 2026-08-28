using System.Text.Json;
using DeskBridge.Core.Models;

namespace DeskBridge.Core.Actions;

internal static class JsonArguments
{
    public static string RequiredString(this JsonElement arguments, string name)
    {
        if (!arguments.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new DeskBridgeActionException(ErrorCodes.InvalidRequest, $"'{name}' must be a non-empty string.");
        }

        return value.GetString()!;
    }

    public static bool OptionalBool(this JsonElement arguments, string name, bool defaultValue)
    {
        if (!arguments.TryGetProperty(name, out var value))
        {
            return defaultValue;
        }

        if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            throw new DeskBridgeActionException(ErrorCodes.InvalidRequest, $"'{name}' must be a boolean.");
        }

        return value.GetBoolean();
    }

    public static int? OptionalInt(this JsonElement arguments, string name)
    {
        if (!arguments.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (!value.TryGetInt32(out var number))
        {
            throw new DeskBridgeActionException(ErrorCodes.InvalidRequest, $"'{name}' must be an integer.");
        }

        return number;
    }
}

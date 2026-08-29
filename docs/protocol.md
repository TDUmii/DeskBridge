# Protocol

Chrome Native Messaging frames every UTF-8 JSON message with a 4-byte unsigned little-endian payload length. DeskBridge limits a message to 1 MB.

## Request

```json
{
  "version": 1,
  "id": "e7bf0e17-c7e1-4a5d-953f-f603639bcd82",
  "action": "read_file",
  "arguments": {
    "path": "D:\\Projects\\Demo\\README.md"
  }
}
```

`version`, non-empty `id`, whitelisted `action`, and object `arguments` are required. Unknown fields may be ignored for forward compatibility, but action-specific fields are validated natively.

## Response

```json
{
  "version": 1,
  "id": "e7bf0e17-c7e1-4a5d-953f-f603639bcd82",
  "success": true,
  "data": {
    "content": "# Demo",
    "size": 6,
    "encoding": "utf-8"
  }
}
```

Errors use a stable code and a recoverable message:

```json
{
  "version": 1,
  "id": "e7bf0e17-c7e1-4a5d-953f-f603639bcd82",
  "success": false,
  "error": {
    "code": "WORKSPACE_VIOLATION",
    "message": "The target is outside the allowed workspace."
  }
}
```

## Actions

| Area | Actions |
|---|---|
| Control | `get_status`, `get_skill_profile`, `open_deskbridge` |
| Files | `read_file`, `write_file`, `create_file`, `create_folder`, `list_folder`, `patch_file` |
| Projects | `create_project`, `update_project` |
| Clipboard | `get_clipboard`, `set_clipboard` |
| Applications | `open_folder`, `open_app`, `open_project`, `open_in_browser`, `preview_web` |
| Commands | `run_command` |
| Windows | `capture_screen`, `get_active_window` |
| Assets | `download_asset`, `import_asset` |
| Images | `inspect_image`, `resize_image`, `compress_image`, `convert_image` |
| Documents | `convert_document_to_markdown` |

## Error codes

`INVALID_REQUEST`, `UNKNOWN_ACTION`, `ACTION_NOT_ALLOWED`, `PERMISSION_DENIED`, `WORKSPACE_VIOLATION`, `FILE_NOT_FOUND`, `FILE_ALREADY_EXISTS`, `APP_NOT_FOUND`, `COMMAND_NOT_ALLOWED`, `COMMAND_TIMEOUT`, `EXECUTION_FAILED`, `INTERNAL_ERROR`, `INVALID_URL`, `UNSUPPORTED_PROTOCOL`, `PRIVATE_NETWORK_BLOCKED`, `DOWNLOAD_TOO_LARGE`, `DOWNLOAD_TIMEOUT`, `INVALID_CONTENT_TYPE`, `UNSUPPORTED_IMAGE_FORMAT`, `IMAGE_PROCESSING_FAILED`, `PATCH_TARGET_NOT_FOUND`, `PATCH_TARGET_NOT_UNIQUE`, `PROJECT_PATH_INVALID`, `ASSET_IMPORT_DENIED`, `SKILL_DISABLED`, `SKILL_RUNTIME_UNAVAILABLE`, `UNSUPPORTED_DOCUMENT_FORMAT`, `DOCUMENT_CONVERSION_FAILED`, and `DOCUMENT_OCR_REQUIRED`.

## Compatibility

V1 is intentionally strict. Breaking protocol changes require a new integer version. Extension validation improves UX, but native validation is authoritative.

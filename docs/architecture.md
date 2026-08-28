# Architecture

DeskBridge separates product UI, local execution, browser integration, and the security boundary.

```text
ChatGPT Web
  -> Manifest V3 content script (DOM adapters + explicit buttons)
  -> extension service worker (schema validation + Native Messaging port)
  -> DeskBridge.Host (framing + native validation/dispatch)
  -> DeskBridge.Core (permission + workspace + action validation + execution)
  -> Windows/files/processes/network

DeskBridge.App
  -> settings.json (workspace and per-action policy)
  -> named pipe DeskBridge.Permission (Allow once / Cancel)
  -> activity.jsonl (metadata only)
```

## Projects

- `DeskBridge.Core` owns protocol models, registry, guards, actions, downloads, images, settings, permission client, and activity logging.
- `DeskBridge.Host` is a stdio-only Chrome Native Messaging process. It never writes diagnostics to stdout because stdout is the protocol stream.
- `DeskBridge.App` is the WPF control panel and permission broker. It owns user-visible workspace and policy changes.
- `extension` contains the service worker, isolated ChatGPT DOM/image adapters, content UX, popup, and shared TypeScript protocol validation.
- `DeskBridge.Tests` exercises the security boundary and core action behavior.

## Dispatch

`ActionRegistry` receives independent `IDeskBridgeAction` implementations. `Program.cs` only frames messages, loads the current workspace, and calls the registry; it does not contain an action switch. The registry applies permission before execution and writes only sanitized activity metadata afterward.

## Shared local state

`%LOCALAPPDATA%\DeskBridge\settings.json` stores the selected workspace and per-action policies. `%LOCALAPPDATA%\DeskBridge\activity.jsonl` stores timestamp, action, target, result, and duration. File contents, clipboard text, screenshot bytes, auth material, and action argument bodies are never logged.

## Permission broker

Confirm actions connect to a current-user-only named pipe. The desktop app displays one request and responds with a matching request ID. If the app is not running, the connection times out and the host denies the action. There is no global “allow everything” mode.

## Packaging

The package script publishes the App and Host into one directory so `open_deskbridge` can locate `DeskBridge.App.exe`. The extension remains an unpacked directory for V1. The generated native host manifest is stored under Local AppData and is never committed.

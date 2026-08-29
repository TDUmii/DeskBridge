# Security model

Every character rendered by ChatGPT Web is untrusted input. A user click expresses intent to inspect a request; it does not make the request safe.

## Security pipeline

```text
ChatGPT output
  -> extension schema and action whitelist
  -> native message framing and JSON validation
  -> native action registry
  -> permission policy / Allow once dialog
  -> normalized workspace boundary
  -> action-specific validation
  -> execution
```

The native host is the security boundary. The extension can be replaced or modified without gaining an action that the host does not register.

## WorkspaceGuard

All project destinations and normal file actions are normalized with `Path.GetFullPath`. Matching requires either the exact workspace root or a child beginning with the root plus a directory separator, so `D:\Projects\App-Evil` does not match `D:\Projects\App`. Project child paths must be relative, may not contain `.`/`..`, and must still resolve under their project root. Existing reparse points are resolved and blocked when their final target escapes the workspace.

## Permissions

Read/list/inspect/status actions default to Allowed. Side-effect actions default to Ask. Blocked denies immediately. Ask connects to the current-user permission pipe and fails closed if the WPF app is unavailable. Clipboard content and file bodies are not copied into the permission log.

## Commands

There is no arbitrary shell action. `powershell`, `pwsh`, `cmd`, WSH, and unknown executables are rejected. Allowed programs are launched directly with `ProcessStartInfo.ArgumentList`, never string concatenation. Git reset/clean/prune/reflog/gc and force flags are blocked. Output is captured, time is limited, and timeout kills the process tree.

The allowed interpreters (`python`, `py`, `node`, `npm`, and `dotnet`) can execute project code. They therefore remain confirmation actions. DeskBridge does not claim that a confirmed interpreter invocation is sandboxed.

## Skill adapters

Local Codex skill folders are not automatically executable APIs. DeskBridge registers each integration explicitly. The document adapter accepts only supported document extensions, requires source and destination inside the workspace, requires a `.md` destination, invokes a fixed package entrypoint with an argument list, has a five-minute limit, and defaults to Ask. It never passes user input through a shell. Hosted OCR is intentionally unavailable because it would upload document content to a third party.

Guidance profiles such as Impeccable only expose user-visible instruction text. They cannot run code or silently add instructions to ChatGPT messages.

## Download SSRF protection

Production downloads accept HTTPS only and reject embedded URL credentials. Every redirect is handled manually. The connection callback resolves the current hop, rejects loopback/private/link-local/carrier-grade NAT/benchmark/reserved IPv4 and local/link/site/unique-local IPv6, then connects the socket to a validated address. Redirects are revalidated up to five hops. The body is limited to 20 MB and a 30-second timeout.

The downloader requires an allowed image MIME type and matching JPG/PNG/WebP/GIF/SVG signature. SVG payloads containing common active-content markers (`script`, JavaScript URLs, `onload`, or `foreignObject`) are rejected. Downloads are never executed.

## Data minimization

DeskBridge does not access ChatGPT cookies, tokens, internal APIs, credentials, browser profiles, or form submission. Screenshots remain in `%TEMP%\DeskBridge`. Activity logs contain metadata only. Native Messaging exposes no listening network port.

## Explicitly unsupported in V1

Deletion, arbitrary PowerShell/CMD, registry editing actions, shutdown/restart, drive formatting, mouse/keyboard control, remote desktop, credential extraction, cookie/token extraction, and automatic ChatGPT message submission are not registered actions.

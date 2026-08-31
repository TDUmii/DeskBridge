# Architecture

DeskBridge separates the visible ChatGPT Web interaction from the local security and verification boundary.

```text
DeskBridge.App
  -> preserve selected source in workspace/.deskbridge/web-agent-runs
  -> create a file-backed local job
  -> open Chrome at https://chatgpt.com/?deskbridge-agent=1

Dedicated ChatGPT Web tab
  -> Manifest V3 content adapter verifies GPT-5.6 Sol + High (3/3)
  -> optional bounded workspace context requests
  -> visible file input and composer
  -> service worker watches the declared download
  -> Chrome Native Messaging
  -> DeskBridge.Host web-agent controller
  -> local candidate inspection and completion gate
  -> workspace/DeskBridge Results
```

## Projects

- `DeskBridge.Core` owns job persistence, prompts, source chunking, candidate-token validation, artifact inspection, settings, permissions, workspace guards, and existing local actions.
- `DeskBridge.Host` is the stdio-only Native Messaging boundary. It dispatches dedicated `web_agent_*` messages before the normal action registry.
- `DeskBridge.App` creates and monitors jobs and owns user-visible workspace and policy settings.
- `extension` contains the isolated ChatGPT DOM adapter, dedicated-tab loop, download watcher, explicit action UI, popup, and shared protocol.
- `DeskBridge.Tests` exercises both the action boundary and web-agent state store.

## Web-only state machine

A job moves through pending, claimed, ChatGPT Web work, local verification, optional revision, and one terminal state: completed, needs_review, failed, or cancelled. The content script claims work only when the URL/session marker identifies a dedicated DeskBridge tab. It verifies the checked GPT-5.6 Sol menu item and maximum reasoning slider before the initial prompt and every revision.

In workspace-context mode, ChatGPT may emit a structured read-only context request. The host permits at most four summary, list, bounded read, or bounded search operations per round and six rounds per run. It returns virtual `workspace:/` paths, skips sensitive and ignored content, and never exposes a write or command operation. In improve-file mode, the extension reconstructs only the preserved source copy from bounded native chunks and assigns it to ChatGPT's visible file input. ChatGPT must eventually attach a downloadable candidate and a structured assessment. The extension arms an exact-filename download watch before clicking. The host requires the unique per-run token and rejects a candidate that predates the run.

## Local completion gate

Every downloaded version is copied to the run evidence directory and inspected locally. Text metadata, document-to-Markdown extraction, image metadata, size, and SHA-256 are recorded as applicable. A candidate is accepted only at score 90 or greater with no declared remaining issue. Otherwise local evidence is incorporated into a follow-up prompt in the same ChatGPT Web conversation. The original source remains unchanged.

## Shared local state

`%LOCALAPPDATA%\DeskBridge\settings.json` stores workspace, fixed web transport settings, and per-action policies. `%LOCALAPPDATA%\DeskBridge\web-agent\runs` coordinates desktop and native host. Workspace-local evidence stores preserved originals and candidate versions. No API key store exists.

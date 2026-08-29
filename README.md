# DeskBridge

DeskBridge is a Windows-first ChatGPT Web file agent and local companion. Give its desktop Agent a normal request plus one workspace file: it opens a dedicated signed-in ChatGPT Web tab, requires GPT-5.6 Sol with High reasoning, uploads through the visible page, downloads and checks each candidate locally, requests revisions in the same web conversation, and saves an accepted result separately. Its Chrome bridge can also run explicit structured file, project, asset, image, application, screenshot, clipboard, and development-tool actions inside one selected workspace.

The Agent is ChatGPT Web-only. It never falls back to Codex, a Codex workspace/task, or the OpenAI Platform API. It requests no API key and never reads ChatGPT cookies, session tokens, browser profiles, or private APIs. DeskBridge exposes no local HTTP API; the local boundary is Chrome Native Messaging.

## Requirements

- Windows 10 or Windows 11 (x64 package target)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Node.js 20 or newer and npm (required by the optional document-to-Markdown adapter)
- Google Chrome
- Git

## Repository

```text
DeskBridge/
├── src/
│   ├── DeskBridge.App/       WPF control panel and permission broker
│   ├── DeskBridge.Core/      actions, security, downloads, images, settings
│   ├── DeskBridge.Host/      Chrome Native Messaging stdio host
│   └── DeskBridge.Tests/     security and action tests
├── extension/                Manifest V3 TypeScript extension
├── scripts/                  build, package, install and uninstall
└── docs/                     architecture, protocol, security and examples
```

## Quick start from source

1. Clone and enter the repository.

   ```powershell
   git clone <repository-url> DeskBridge
   Set-Location .\DeskBridge
   ```

2. Build and test everything.

   ```powershell
   .\scripts\build.ps1
   ```

3. Start the desktop app.

   ```powershell
   .\src\DeskBridge.App\bin\Release\net8.0-windows\DeskBridge.App.exe
   ```

4. Choose the one folder DeskBridge may access. Sign in to ChatGPT Web normally in Chrome. No API key is needed.

5. Install the Chrome extension/native bridge; these are required for the Web-only Agent and for optional action blocks. Keep the app running when an Ask policy should display a confirmation.

6. Open `chrome://extensions`, enable **Developer mode**, choose **Load unpacked**, and select `DeskBridge\extension\dist`.

7. Copy the 32-character extension ID shown by Chrome, then register the native host for that exact origin.

   ```powershell
   .\scripts\install-native-host.ps1 -ExtensionId abcdefghijklmnopabcdefghijklmnop
   ```

8. Reload the extension. Its popup should show **Connected** and the selected workspace. Return to Agent, choose a source file, describe the finished result, and click **Start in ChatGPT Web · uploads file**. The original is retained; accepted output appears in `DeskBridge Results`.

The generated manifest contains the local absolute host path and is written to `%LOCALAPPDATA%\DeskBridge\native-host\com.deskbridge.host.json`; it is intentionally not committed.

## Install the Windows release

1. Download and extract `DeskBridge-v1.1.2-win-x64.zip` to a stable folder.
2. Open `chrome://extensions`, enable **Developer mode**, choose **Load unpacked**, and select the extracted `extension` folder.
3. Double-click `Install-DeskBridge.cmd`. It registers the fixed extension ID and opens the app.

After the first setup, run `DeskBridge.App.exe` normally. The package is self-contained for Windows x64: no separate .NET installation or API key is required. Keep the extracted folder in place because Chrome points to its native host. Double-click `Uninstall-DeskBridge.cmd` to remove only the native registration.

## How it works

### Autonomous file agent

The Agent first preserves the selected source in `.deskbridge/web-agent-runs/<run-id>/original`, then opens `https://chatgpt.com/?deskbridge-agent=1`. Only that marked tab may claim the job. Before the first prompt and every revision, the extension verifies that GPT-5.6 Sol is checked and the reasoning slider is at High (3/3). If verification fails, it stops without sending and does not switch transport.

The selected source is reconstructed from bounded Native Messaging chunks and attached with ChatGPT Web's visible file input. Each response must provide a direct downloadable artifact whose filename contains a unique run token and a structured assessment. The service worker watches that exact download; the host rejects mismatched, stale, or tokenless files.

Candidates are copied into `.deskbridge/web-agent-runs/<run-id>/versions` and inspected locally. Completion requires score 90 or greater and no declared remaining issue. Otherwise DeskBridge sends local inspection evidence back to the same ChatGPT Web conversation for another bounded pass. Reaching the limit preserves the best version for review without claiming success.

### ChatGPT Web action blocks

Ask ChatGPT to output an action in a fenced `deskbridge` block:

````markdown
```deskbridge
{
  "version": 1,
  "id": "demo-read-1",
  "action": "read_file",
  "arguments": {
    "path": "D:\\Projects\\Demo\\README.md"
  }
}
```
````

The content script adds **Run with DeskBridge**. Nothing runs until you click. The extension validates the envelope, the service worker sends it to `com.deskbridge.host`, and the native registry applies permission, workspace, and action validation before execution. A success or stable error code appears below the block and can be copied.

Normal code blocks get **Save file**, which asks for an explicit absolute path inside the current workspace. ChatGPT images may get **Save to DeskBridge** when they expose a public/signed HTTPS URL; the button downloads through the SSRF-protected asset action. If the image URL requires ChatGPT authentication, download it normally and use `import_asset` - DeskBridge will not copy auth tokens or cookies.

## Supported actions

| Capability | Actions | Default policy |
|---|---|---|
| Host control | `get_status`, `open_deskbridge` | Allowed |
| Read/inspect | `read_file`, `list_folder`, `inspect_image`, `get_active_window` | Allowed |
| Files/projects | `write_file`, `create_file`, `create_folder`, `create_project`, `update_project`, `patch_file` | Ask |
| Clipboard | `get_clipboard`, `set_clipboard` | Ask except no logging of content |
| Applications | `open_folder`, `open_app`, `open_project`, `open_in_browser`, `preview_web` | Ask |
| Commands | `run_command` | Ask |
| Windows | `capture_screen` | Ask |
| Assets/images | `download_asset`, `import_asset`, `resize_image`, `compress_image`, `convert_image` | Ask |
| Skills | `get_skill_profile` | Allowed |
| Document conversion | `convert_document_to_markdown` | Ask |

V1 deliberately has no delete action, arbitrary shell, registry-edit action, shutdown/restart, mouse/keyboard control, credential extraction, browser profile access, ChatGPT token/cookie access, private ChatGPT API, or automatic form/message submission.

## Appearance and skill integrations

The Settings tab offers **System**, **Light**, and **Dark** appearance modes. System mode follows the Windows app-theme preference, and the Chrome extension follows Chrome's system color preference.

Skill integrations are deliberately typed:

- **Convert documents to Markdown** is an executable adapter. Enable it in Settings, leave its permission on Ask, and use `convert_document_to_markdown` with source and `.md` destination paths inside the selected workspace. DeskBridge invokes `@firecrawl/anydoc` through a known npm runner - never an arbitrary shell command. The first run may download the converter package. Hosted OCR is disabled, so an OCR-only document returns `DOCUMENT_OCR_REQUIRED` instead of uploading the file.
- **Impeccable** is a guidance profile. Enabling it exposes a reusable UI-quality instruction through `get_skill_profile`, and **Copy instruction** places that text on the clipboard. DeskBridge contains no local LLM and does not claim to execute the Codex skill itself.

The converter uses the bundled Codex Node runtime when available, otherwise `npx.cmd` from `PATH`. Set `DESKBRIDGE_NPX_PATH` to an explicit trusted `npx.cmd` if needed.

## Security

- The webpage is untrusted; native validation is authoritative.
- `WorkspaceGuard` normalizes paths, rejects outside/similar-prefix paths, blocks relative project traversal and absolute project child paths, and checks existing reparse points for escape.
- Side-effect actions default to Ask. The WPF app offers **Allow once** or **Cancel**; if it is unavailable, Ask fails closed.
- Commands use an executable whitelist and `ProcessStartInfo.ArgumentList`. PowerShell, pwsh, CMD, WSH, unknown executables, destructive Git subcommands, and force flags are blocked. Timeout terminates the process tree.
- Downloads accept HTTPS only, manually validate every redirect, pin connections to DNS addresses already checked as public, limit payloads to 20 MB, and require MIME/magic-byte agreement.
- Native Messaging uses stdin/stdout only. DeskBridge does not bind `0.0.0.0`, `127.0.0.1`, or any other listening socket in production.
- Logs contain timestamp, action, target, result, and duration only - never file/clipboard content, screenshot bytes, browser credentials, or tokens. Web-agent evidence contains the request, local paths, inspection metadata, hashes, and candidate files.

See [security.md](docs/security.md) for the threat model and honest interpreter limitation.

## Website workflow

For a basic site, ask ChatGPT for plain HTML/CSS/JavaScript and a single `create_project` action with relative child paths:

```text
DemoWebsite/
├── index.html
├── css/style.css
├── js/script.js
└── assets/images/   only when used
```

Use relative references in site code (`./css/style.css`, `./js/script.js`, `./assets/images/hero.webp`). Use `read_file` before a targeted edit, `patch_file` only when the old fragment is unique, or `update_project` for a coordinated batch. Batch updates back up existing touched files under `.deskbridge/backups` and retain ten versions. `preview_web` opens a static entry file in the default browser and does not silently start a server.

## Image workflow

1. `download_asset` for a public HTTPS image or `import_asset` for a local file chosen by the user.
2. `inspect_image` to read dimensions, format, bytes, and alpha use.
3. `resize_image` (no upscaling unless `allowUpscale: true`).
4. `compress_image` or `convert_image` to PNG, JPEG, or WebP with validated quality.
5. Reference the optimized relative path in the website.

Downloaded assets under an `assets` folder add/update `assets/sources.json` with source URL, time, and content type. Local imports intentionally do not add Internet-source metadata. Image processing uses ImageSharp 3.1.12; review [third-party notices](THIRD_PARTY_NOTICES.md) before distribution.

## Git workflow

Use `run_command` with `program: "git"`, an argument array, and a working directory inside the workspace. Common non-destructive operations such as status, init, add, commit, log, branch, remote, fetch, pull --rebase, and normal push are available. Force flags and destructive subcommands remain blocked.

```json
{
  "version": 1,
  "id": "git-status-1",
  "action": "run_command",
  "arguments": {
    "program": "git",
    "args": ["status"],
    "workingDirectory": "D:\\Projects\\Demo"
  }
}
```

## Build and package

```powershell
.\scripts\build.ps1
.\scripts\package.ps1
```

The package is written to `artifacts\DeskBridge-win-x64`. It is self-contained for Windows x64 and includes the App, Host, extension build, license, and third-party notices. Source control excludes packages, build outputs, Node modules, logs, generated manifests, and personal settings.

## Troubleshooting

### Popup says disconnected

- Start DeskBridge.App and select a workspace.
- Confirm `extension\dist` is the folder loaded by Chrome.
- Re-run `install-native-host.ps1` with the current extension ID; an unpacked extension ID can change when loaded from another path.
- Reload the extension after changing native registration.
- Verify `HKCU\Software\Google\Chrome\NativeMessagingHosts\com.deskbridge.host` points to the generated manifest and its `path` points to an existing `DeskBridge.Host.exe`.

### Permission denied immediately

An Ask action requires the desktop app's current-user named-pipe broker. Keep the app running and look for its permission dialog. A Blocked policy denies without prompting. Change policy on the Settings tab if that is intentional.

### Workspace violation

Choose a workspace that contains the target. Use absolute paths for top-level action arguments. In `create_project`/`update_project`, `files[].path` must be relative to `rootPath` and cannot contain `.` or `..` segments.

### Command rejected

Only `git`, `python`, `py`, `dotnet`, `node`, and `npm` (plus Windows executable variants) are allowed. Shells and force/destructive Git operations are intentionally unavailable. DeskBridge launches programs directly, so shell syntax such as pipes, redirects, `&&`, or environment expansion does not apply.

### ChatGPT image cannot be saved directly

Some generated-image URLs need a signed-in browser session. DeskBridge will not extract that session. Download the image using ChatGPT's normal UI, then run `import_asset` with explicit source and destination paths.

### ImageSharp licensing

DeskBridge is MIT licensed, but ImageSharp has its own Six Labors Split License. Read [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) and confirm your distribution/use qualifies or obtain the appropriate Six Labors license.

### Document converter is unavailable

Install Node.js 20+ with npm, restart DeskBridge, and confirm `npx.cmd` is available. Codex Desktop's bundled runtime is detected automatically on machines where it is installed. Conversion remains disabled until you enable **Convert documents to Markdown** in Settings.

## Documentation

- [Architecture](docs/architecture.md)
- [Protocol](docs/protocol.md)
- [Security](docs/security.md)
- [Development](docs/development.md)
- [Examples](docs/examples.md)

## Known V1 limitations

- Chrome integration depends on visible ChatGPT Web DOM. Agent selectors are isolated in `ChatGptWebAgentAdapter.ts`; a page change fails closed until the adapter is updated.
- Direct image saving works only for HTTPS URLs the native downloader can fetch without browser credentials; local import is the stable fallback.
- Static preview opens the entry file directly. DeskBridge does not auto-start a local HTTP server.
- Allowed interpreters can execute workspace code after confirmation; V1 is not an OS sandbox.
- The extension is loaded unpacked and native host installation is a PowerShell step; there is no signed installer or Chrome Web Store distribution.
- V1 captures only the primary monitor.
- The Agent requires a signed-in ChatGPT Web account where GPT-5.6 Sol and High are available. It will not silently substitute another model. Local inspection validates file structure and extractable content but cannot prove every subjective visual requirement.

## License

DeskBridge source is available under the [MIT License](LICENSE). Third-party components retain their own licenses.

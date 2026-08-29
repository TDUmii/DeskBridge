# Development

## Requirements

- Windows 10 or 11
- .NET 8 SDK
- Node.js 18 or newer and npm
- Google Chrome
- Git

## Build and test

```powershell
.\scripts\build.ps1
```

Equivalent commands:

```powershell
dotnet restore .\DeskBridge.sln
dotnet build .\DeskBridge.sln -c Release
dotnet test .\DeskBridge.sln -c Release --no-build
Set-Location .\extension
npm.cmd ci
npm.cmd run build
npm.cmd audit --audit-level=moderate
```

## Run from source

Start `src\DeskBridge.App\bin\Debug\net8.0-windows\DeskBridge.App.exe` and choose a workspace. No API key or environment variable is used. Sign in to ChatGPT Web normally in Chrome.

Load `extension\dist` as an unpacked extension, copy its ID from `chrome://extensions`, then register the native host:

```powershell
.\scripts\install-native-host.ps1 -ExtensionId <32-character-extension-id>
```

Reload the extension after registration. Chrome launches `DeskBridge.Host.exe` on demand; do not start it manually because stdin/stdout belong to Chrome. A web-agent run opens a new tab with `?deskbridge-agent=1`; only that dedicated tab may claim the job.

## Safe browser testing

Read-only inspection may verify the model menu and DOM selectors. A live end-to-end run sends a prompt and uploads a file to ChatGPT Web, so use a synthetic non-sensitive fixture and obtain the appropriate action-time approval before submitting during development.

## Package

```powershell
.\scripts\package.ps1
```

The self-contained Windows x64 output is `artifacts\DeskBridge-win-x64` and includes both executables, dependencies, extension build, license, and notices.

## Debug native messages

Never print debug text to host stdout. Use metadata-only file or stderr diagnostics. Native frames use a 4-byte little-endian length followed by exact UTF-8 JSON.

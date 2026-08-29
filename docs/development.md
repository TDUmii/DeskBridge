# Development

## Requirements

- Windows 10 or 11
- .NET 8 SDK
- Node.js 18 or newer and npm
- Chrome
- Git

## Build everything

```powershell
.\scripts\build.ps1
```

Equivalent manual commands:

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

Start `src\DeskBridge.App\bin\Debug\net8.0-windows\DeskBridge.App.exe` and choose a workspace. For Agent development, save a test Platform API key in Settings or set `OPENAI_API_KEY`; never add a key to repository files. Agent contract and orchestration tests use local fakes and do not spend API credits.

For optional browser integration, load `extension\dist` as an unpacked extension, copy its ID from `chrome://extensions`, then register the native host:

```powershell
.\scripts\install-native-host.ps1 -ExtensionId <32-character-extension-id>
```

Reload the extension after registration. Chrome launches `DeskBridge.Host.exe` on demand; do not start the host in a terminal because its stdin/stdout belong to Chrome.

## Package

```powershell
.\scripts\package.ps1
```

The self-contained Windows x64 output is `artifacts\DeskBridge-win-x64`. The folder contains both executables, dependencies, extension build, license, and notices.

## Debug native messages

Never print debug text to host stdout. Write metadata-only diagnostics to a file or stderr. A test client must send a 4-byte little-endian length and exact UTF-8 JSON payload, then read the response with the same framing.

## Uninstall host registration

```powershell
.\scripts\uninstall-native-host.ps1
```

This removes only DeskBridge's HKCU Chrome Native Messaging registration. It does not remove Chrome, the extension, workspace files, settings, or unrelated registry keys.

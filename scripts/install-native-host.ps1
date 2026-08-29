[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [ValidatePattern('^[a-p]{32}$')]
    [string]$ExtensionId = 'chhimbcahcjjpggdlahimdcaohaaehhm',
    [string]$HostPath
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($HostPath)) {
    $candidates = @(
        (Join-Path $repositoryRoot 'DeskBridge.Host.exe'),
        (Join-Path $repositoryRoot 'artifacts\DeskBridge-win-x64\DeskBridge.Host.exe'),
        (Join-Path $repositoryRoot 'src\DeskBridge.Host\bin\Release\net8.0-windows\DeskBridge.Host.exe'),
        (Join-Path $repositoryRoot 'src\DeskBridge.Host\bin\Debug\net8.0-windows\DeskBridge.Host.exe')
    )
    $HostPath = $candidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
}
if ([string]::IsNullOrWhiteSpace($HostPath) -or -not (Test-Path -LiteralPath $HostPath -PathType Leaf)) {
    throw 'DeskBridge.Host.exe was not found beside the release package or in a build output.'
}

$resolvedHost = (Resolve-Path -LiteralPath $HostPath).Path
if ([System.IO.Path]::GetFileName($resolvedHost) -ne 'DeskBridge.Host.exe') {
    throw 'HostPath must point to DeskBridge.Host.exe.'
}

$nativeDirectory = Join-Path $env:LOCALAPPDATA 'DeskBridge\native-host'
New-Item -ItemType Directory -Path $nativeDirectory -Force | Out-Null
$manifestPath = Join-Path $nativeDirectory 'com.deskbridge.host.json'
$manifest = [ordered]@{
    name = 'com.deskbridge.host'
    description = 'DeskBridge Native Messaging Host'
    path = $resolvedHost
    type = 'stdio'
    allowed_origins = @("chrome-extension://$ExtensionId/")
}
$manifestJson = $manifest | ConvertTo-Json -Depth 4
[System.IO.File]::WriteAllText($manifestPath, $manifestJson, [System.Text.UTF8Encoding]::new($false))

$registrySubKey = 'Software\Google\Chrome\NativeMessagingHosts\com.deskbridge.host'
foreach ($registryView in @([Microsoft.Win32.RegistryView]::Registry32, [Microsoft.Win32.RegistryView]::Registry64)) {
    $baseKey = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::CurrentUser, $registryView)
    try {
        $hostKey = $baseKey.CreateSubKey($registrySubKey, $true)
        try {
            $hostKey.SetValue('', $manifestPath, [Microsoft.Win32.RegistryValueKind]::String)
        }
        finally {
            $hostKey.Dispose()
        }
    }
    finally {
        $baseKey.Dispose()
    }
}

Write-Host 'DeskBridge Native Messaging host registered.'
Write-Host "Host: $resolvedHost"
Write-Host "Manifest: $manifestPath"
Write-Host "Extension origin: chrome-extension://$ExtensionId/"
Write-Host 'Registry views: 32-bit and 64-bit current user.'

[CmdletBinding()]
param(
    [switch]$RemoveInstalledFiles
)

$ErrorActionPreference = 'Stop'
$registrySubKey = 'Software\Google\Chrome\NativeMessagingHosts\com.deskbridge.host'
$removed = $false
foreach ($registryView in @([Microsoft.Win32.RegistryView]::Registry32, [Microsoft.Win32.RegistryView]::Registry64)) {
    $baseKey = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::CurrentUser, $registryView)
    try {
        if ($baseKey.OpenSubKey($registrySubKey)) {
            $baseKey.DeleteSubKeyTree($registrySubKey, $false)
            $removed = $true
        }
    }
    finally {
        $baseKey.Dispose()
    }
}

if ($removed) {
    Write-Host 'DeskBridge Native Messaging registration removed from 32-bit and 64-bit registry views.'
} else {
    Write-Host 'DeskBridge Native Messaging registration was not present.'
}

if ($RemoveInstalledFiles) {
    $installRoot = Join-Path $env:LOCALAPPDATA 'Programs\DeskBridge'
    $expectedRoot = [System.IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Programs')).TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    $resolvedInstallRoot = [System.IO.Path]::GetFullPath($installRoot)
    if (-not $resolvedInstallRoot.StartsWith($expectedRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw 'Refusing to remove an unexpected install path.'
    }

    $startMenuDirectory = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\DeskBridge'
    $desktopShortcut = Join-Path ([Environment]::GetFolderPath('Desktop')) 'DeskBridge.lnk'
    Remove-Item -LiteralPath $startMenuDirectory -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $desktopShortcut -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\DeskBridge' -Recurse -Force -ErrorAction SilentlyContinue
    $nativeDirectory = Join-Path $env:LOCALAPPDATA 'DeskBridge\native-host'
    Remove-Item -LiteralPath $nativeDirectory -Recurse -Force -ErrorAction SilentlyContinue

    $cleanupCommand = 'timeout /t 2 /nobreak >nul & rmdir /s /q "' + $resolvedInstallRoot + '"'
    Start-Process -FilePath $env:ComSpec -ArgumentList '/d', '/c', $cleanupCommand -WindowStyle Hidden
    Write-Host 'DeskBridge application files are scheduled for removal.'
}
